import pandas as pd
import numpy as np
import json
import warnings
import hashlib
import re
import os
import tempfile
import time
from google import genai
from google.genai import types

warnings.filterwarnings("ignore")

# ================= 0. AYARLAR & CACHE =================
API_KEY = "AIzaSyBjxtJ3XRxoCKIsDlC0YRvKRO3xX1bfxXA"
client = genai.Client(api_key=API_KEY)
GEMINI_CACHE = {}

MAX_ROWS = 2000        # Unity'de performans sınırı
MAX_CATEGORIES = 8     # AR Kalkanı: Metin taşmalarını önlemek için 15'ten 8'e düşürüldü

# ================= 1. DİNAMİK AR İÇİN CSV TEMİZLEME (Eski csv_preprocessor.py) =================
def clean_col_name(name: str) -> str:
    name = str(name).strip().replace('\ufeff', '').replace('\u200b', '')
    name = name.replace('"', '').replace("'", '').replace(',', '_')
    name = re.sub(r'[^\w\s]', '_', name)
    name = re.sub(r'\s+', '_', name).strip('_')
    return name if name else 'Column'

def clean_cell_value(val) -> str:
    s = str(val).strip().replace('"', '').replace("'", '')
    s = s.replace(',', '_').replace('\n', ' ').replace('\r', '')
    return s

def preprocess(input_path: str, output_path: str) -> str:
    df = None
    for enc in ['utf-8-sig', 'utf-8', 'latin-1', 'cp1252']:
        try:
            df = pd.read_csv(input_path, encoding=enc, low_memory=False)
            break
        except Exception:
            continue

    if df is None:
        raise ValueError(f"Dosya okunamadı: {input_path}")

    df.columns = [clean_col_name(c) for c in df.columns]
    
    seen = {}
    new_cols = []
    for c in df.columns:
        if c in seen:
            seen[c] += 1
            new_cols.append(f"{c}_{seen[c]}")
        else:
            seen[c] = 1
            new_cols.append(c)
    df.columns = new_cols

    df = df.dropna(axis=1, how='all').dropna(axis=0, how='all')

    for col in df.columns:
        converted = pd.to_numeric(df[col], errors='coerce')
        if (converted.notna().sum() / max(len(df), 1)) >= 0.5:
            df[col] = converted
            median_val = df[col].median()
            df[col] = df[col].fillna(median_val if not np.isnan(median_val) else 0).round(4)
        else:
            df[col] = df[col].astype(str).apply(clean_cell_value)
            df[col] = df[col].replace({'nan': '', 'None': '', 'NaN': ''})
            
            n_unique = df[col].nunique()
            if n_unique > MAX_CATEGORIES:
                top = df[col].value_counts().nlargest(MAX_CATEGORIES).index.tolist()
                df[col] = df[col].where(df[col].isin(top), other='Diger')

    if len(df) > MAX_ROWS:
        df = df.sample(n=MAX_ROWS, random_state=42).reset_index(drop=True)

    df.to_csv(output_path, index=False, encoding='utf-8', quoting=0)
    return output_path

def preprocess_bytes(raw_bytes: bytes, filename: str) -> tuple[bytes, str]:
    suffix = os.path.splitext(filename)[1] or '.csv'
    with tempfile.NamedTemporaryFile(suffix=suffix, delete=False) as tmp_in:
        tmp_in.write(raw_bytes)
        tmp_in_path = tmp_in.name

    tmp_out_path = tmp_in_path + '_clean.csv'
    try:
        preprocess(tmp_in_path, tmp_out_path)
        with open(tmp_out_path, 'rb') as f:
            result_bytes = f.read()
        out_name = os.path.splitext(filename)[0] + '_dynamic.csv'
        return result_bytes, out_name
    finally:
        if os.path.exists(tmp_in_path): os.remove(tmp_in_path)
        if os.path.exists(tmp_out_path): os.remove(tmp_out_path)

# ================= 2. GEMINİ API İÇİN VERİ HAZIRLIĞI =================
def prepare_data_for_gemini(df):
    df_clean = df.copy()
    limit = int(len(df_clean) * 0.3)
    df_clean = df_clean.dropna(axis=1, thresh=len(df_clean) - limit).dropna(axis=0, how='any')

    to_drop = [c for c in df_clean.columns if any(x in str(c).lower() for x in ['id', 'index', 'unnamed', 'guid', 'uuid', 'category','type 1', 'type 2', 'type 3','serving size','jobrole','beta'])]
    date_cols = [c for c in df_clean.columns if any(x in str(c).lower() for x in ['date', 'time', 'timestamp', 'year'])]
    if len(date_cols) > 1: to_drop.extend(date_cols[1:])
    df_clean = df_clean.drop(columns=to_drop, errors='ignore')

    schema_info = {}
    num_continuous = 0

    for col in df_clean.columns:
        n_uni = df_clean[col].nunique()
        is_num = pd.api.types.is_numeric_dtype(df_clean[col])
        is_seq = any(x in str(col).lower() for x in ['year', 'date', 'time', 'day'])
        is_fake_num = any(x in str(col).lower() for x in ['code', 'zip', 'postal', 'phone'])

        if n_uni <= 1: continue
        if not is_num and n_uni > 25 and n_uni > len(df_clean) * 0.9: continue

        if is_seq:
            ctype = f'Sequential (Zaman/Sıralı) - {n_uni} benzersiz değer'
            num_continuous += 1
        elif is_num and not is_fake_num and n_uni > 5:
            ctype = f'Continuous (Gerçek Sayısal) - {n_uni} benzersiz değer'
            num_continuous += 1
        else:
            ctype = f'Categorical (Kategori/Sınıf) - {n_uni} benzersiz değer'

        schema_info[col] = ctype

    df_sample = df_clean.sample(min(100, len(df_clean)), random_state=42)
    return df_sample.to_csv(index=False), schema_info, num_continuous

# ================= 3. GEMINI İŞ ZEKASI =================
def generate_business_charts(csv_data, schema_dict, num_cont):
    compact_schema = json.dumps(schema_dict, ensure_ascii=False, indent=2)
    total_cols = len(schema_dict)
    chart_count = 5 if num_cont >= 4 else 3
    has_time = any('Sequential' in str(v) for v in schema_dict.values())
    has_valid_network_cat = any(4 <= int(''.join(filter(str.isdigit, str(desc))) or 0) <= 8 for desc in schema_dict.values() if "Categorical" in str(desc))

    finance_keywords_strong = ['adj close', 'adj_close', 'market cap', 'pe ratio', 'ticker', 'enterprise value', 'dividends']
    finance_keywords_weak = ['open', 'high', 'low', 'close']
    is_finance = any(any(fk in str(col).lower() for fk in finance_keywords_strong) for col in schema_dict.keys()) or sum(1 for col in schema_dict.keys() if any(fw == str(col).lower().strip() for fw in finance_keywords_weak)) >= 2

    if is_finance:
        chart_pool, line_rule = '["3D Multi-Line Chart", "3D Voxel Density"]', "BORSA KURALI: SADECE Multi-Line ve Voxel kullan. Scatter, Bubble veya Network YASAK!"
    elif total_cols == 3:
        chart_pool, line_rule = '["3D Scatter Plot", "3D Bubble Chart", "3D Voxel Density"]', "3 KOLON KURALI: Veri setinde sadece 3 kolon var."
    elif num_cont >= 3:
        chart_pool = '["3D Scatter Plot", "3D Multi-Line Chart", "3D Voxel Density"]' if has_time else ('["3D Scatter Plot", "3D Bubble Chart", "3D Network Graph", "3D Voxel Density"]' if has_valid_network_cat else '["3D Scatter Plot", "3D Bubble Chart", "3D Voxel Density"]')
        line_rule = "ZAMAN SERİSİ KURALI: Tarih varsa X'e koy ve '3D Multi-Line Chart' kullan." if has_time else "Geçerli grafikleri kullan."
    elif num_cont == 2:
        chart_pool, line_rule = ('["3D Scatter Plot", "3D Bubble Chart", "3D Network Graph"]', "Network, Bubble ve Scatter.") if has_valid_network_cat else ('["3D Scatter Plot", "3D Bubble Chart"]', "Scatter ve Bubble.")
    else:
        chart_pool, line_rule = ('["3D Voxel Density", "3D Network Graph"]', "Voxel veya Network.") if has_valid_network_cat else ('["3D Voxel Density", "3D Bubble Chart"]', "NETWORK YASAK.")

    prompt = f"""
    Sen dünyanın en zeki Kıdemli Veri Analistisin. Şema: {compact_schema} Veri: {csv_data}
    KURALLAR:
    1. Grafik havuzu: {chart_pool}
    2. Borsa/Finans için 'Open', 'High', 'Low', 'Close' kolonlarından SADECE 1 TANESİNİ KULLAN!
    3. {line_rule}
    4. Eksenlere (X,Y,Z) SADECE Continuous veya Sequential koy. 
    5. 'color' parametresini Voxel için BOŞ BIRAK. Categorical kolon 8'den büyükse data_processing: 'top_8' YAP.
    6. 'size' SADECE Bubble Chart içindir. Diğerlerinde BOŞ bırak. "size1 asla X,Y,Z kolonlarından ve renk scalasını verme.
    7. Renk scalasını asla X,Y,Z kolonlarından verme. Categorical kolonlardan ver. Eğer yoksa BOŞ bırak. Her X,Y,Z ekseni farklı olsun
    8. Aynı X,Y,Z kombinasyonunu kullanan 2 grafik önerme. En fazla 1 tane olabilir. Farklı kombinasyonlar kullan.
    9. Weight ve height kullanılarak türetilen bmi gibi kolonları aynı grafikte kullanma. Farklı grafikte kullan.
    10. {chart_count} grafik tamamen farklı kolon kombinasyonları kullanmalı.
    Aşağıdaki JSON formatında dizi döndür:
    [{{ "dimension": 3, "score": 0.95, "chart_type": "...", "axes": {{ "x": "...", "y": "...", "z": "...", "color": "...", "size": "" }}, "data_processing": "none", "insight_message": "..." }}]
    """

    max_retries = 3 # EKLENDİ: Hata durumunda kaç kez tekrar denenecek
    
    for attempt in range(max_retries):
        try:
            response = client.models.generate_content(
                model='gemini-2.5-pro',
                contents=prompt,
                config=types.GenerateContentConfig(response_mime_type="application/json", temperature=0.10)
            )
            
            clean_text = response.text.replace("http://googleusercontent.com/immersive_entry_chip/0", "").split("```json")[-1].split("```")[0].strip()
            charts = json.loads(clean_text)

            seq_cols = [col for col, desc in schema_dict.items() if "Sequential" in desc]
            safe_cont_cols = [col for col, desc in schema_dict.items() if "Continuous" in desc]
            
            for chart in charts:
                axes = chart.get("axes", {})
                if seq_cols and "Line" in chart.get("chart_type", ""):
                    if axes.get("x") != seq_cols[0]: axes["x"] = seq_cols[0]
                if seq_cols and axes.get("x") == seq_cols[0] and "Line" not in chart.get("chart_type", ""):
                    chart["chart_type"] = "3D Multi-Line Chart"

                used_fin_axes = [ax for ax in ['x', 'y', 'z'] if str(axes.get(ax, "")).lower() in finance_keywords_weak]
                if len(used_fin_axes) > 1: axes[used_fin_axes[1]] = safe_cont_cols[0] if safe_cont_cols else axes[used_fin_axes[1]]

                final_color = str(axes.get("color", "")).strip()
                if final_color and final_color in schema_dict and "Categorical" in schema_dict[final_color]:
                    if int(''.join(filter(str.isdigit, schema_dict[final_color])) or 0) > 8:
                        chart["data_processing"] = "top_8"

            return charts
            
        except Exception as e:
            error_str = str(e)
            print(f"Gemini API deneme {attempt + 1}/{max_retries} başarısız: {error_str}")
            
            # EKLENDİ: Eğer hata 503 (Yoğunluk) veya 429 (Rate Limit) ise biraz bekle ve tekrar dene
            if "503" in error_str or "UNAVAILABLE" in error_str or "429" in error_str:
                if attempt < max_retries - 1:
                    time.sleep(2 ** attempt) # 1. denemede 1 sn, 2. denemede 2 sn bekler
                    continue
            
            # Eğer haklar bittiyse veya farklı bir koda (örn: Auth hatası) takıldıysa hatayı direkt fırlat
            raise Exception(f"Yapay zeka sunucularında bir sorun var: {error_str}")

# ================= 4. ANALİZ YÖNETİCİSİ =================
def analyze_data(df):
    global GEMINI_CACHE
    try:
        csv_data, schema, num_cont = prepare_data_for_gemini(df)
        if len(schema) < 3: return {"success": False, "error_message": "En az 3 kolon gerekli."}
        
        cache_key = hashlib.md5((csv_data + str(schema)).encode('utf-8')).hexdigest()
        if cache_key in GEMINI_CACHE: return GEMINI_CACHE[cache_key]

        # Artık generate_business_charts boş liste [] dönmek yerine kritik hatalarda Exception fırlatıyor.
        final_charts = generate_business_charts(csv_data, schema, num_cont)
        
        if not final_charts: 
            return {"success": False, "error_message": "Yapay zeka uygun bir grafik yapısı oluşturamadı."}

        result = {"success": True, "recommended_charts": sorted(final_charts, key=lambda k: k.get('score', 0.0), reverse=True)}
        GEMINI_CACHE[cache_key] = result
        return result
        
    except Exception as e:
        # EKLENDİ: Artık API çökünce "400: Grafik üretilemedi" demek yerine, doğrudan hatanın sebebini JSON içinde önyüze iletiyoruz.
        return {"success": False, "error_message": str(e)}