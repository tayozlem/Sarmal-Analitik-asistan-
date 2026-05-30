using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// DynamicARManager'a göndermeden önce CSV'yi temizler.
/// 
/// KULLANIM — ResultManager.cs'deki dinamik buton onClick'inde:
///   DynamicCSVPreprocessor.Instance.PrepareAndStart(filePath);
///   
/// DynamicARManager.cs'e tek satır bile dokunmana gerek yok.
/// </summary>
public class DynamicCSVPreprocessor : MonoBehaviour
{
    public static DynamicCSVPreprocessor Instance;

    [Header("Sunucu Ayarları")]
#if UNITY_EDITOR
    [SerializeField] private string serverUrl = "http://127.0.0.1:8000/api/v1/preprocess";
#else
    [SerializeField] private string serverUrl = "https://ar-server-g8kw.onrender.com/api/v1/preprocess";
#endif

    [Header("Yerel Temizleme Ayarları")]
    [Tooltip("Sunucuya ulaşılamazsa Unity içinde temizlik yapılsın mı?")]
    [SerializeField] private bool fallbackToLocalCleaning = true;

    [Tooltip("Unity'de tutulacak maksimum satır sayısı")]
    [SerializeField] private int maxRows = 2000;

    [Tooltip("Bir kategorik kolonda tutulacak maksimum benzersiz değer")]
    [SerializeField] private int maxCategories = 15;

    void Awake()
    {
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────────
    //  ANA GİRİŞ NOKTASI
    //  ResultManager veya başka bir script buraya çağrı yapar.
    // ─────────────────────────────────────────────────────────────
    public void PrepareAndStart(string originalCsvPath)
    {
        StartCoroutine(PrepareCoroutine(originalCsvPath));
    }

    private IEnumerator PrepareCoroutine(string originalCsvPath)
    {
        if (!File.Exists(originalCsvPath))
        {
            Debug.LogError($"[DynamicPrep] Dosya bulunamadı: {originalCsvPath}");
            yield break;
        }

        // Temizlenmiş dosya için yol
        string cleanPath = GetCleanPath(originalCsvPath);

        // ── Önce sunucuya göndermeyi dene ──────────────────────────
        bool serverSuccess = false;
        yield return StartCoroutine(TryServerPreprocess(originalCsvPath, cleanPath,
            success => serverSuccess = success));

        if (!serverSuccess)
        {
            Debug.LogWarning("[DynamicPrep] Sunucu ulaşılamadı veya hata verdi.");

            if (fallbackToLocalCleaning)
            {
                Debug.Log("[DynamicPrep] Yerel temizleme başlatılıyor...");
                bool localOk = LocalClean(originalCsvPath, cleanPath);
                if (!localOk)
                {
                    Debug.LogWarning("[DynamicPrep] Yerel temizleme de başarısız. Ham dosya kullanılıyor.");
                    cleanPath = originalCsvPath;
                }
            }
            else
            {
                cleanPath = originalCsvPath;
            }
        }

        // ── DynamicARManager'ı başlat ──────────────────────────────
        if (DynamicARManager.Instance != null)
        {
            Debug.Log($"[DynamicPrep] DynamicARManager başlatılıyor: {cleanPath}");
            DynamicARManager.Instance.StartDynamicSession(cleanPath);
        }
        else
        {
            Debug.LogError("[DynamicPrep] DynamicARManager.Instance bulunamadı!");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  SUNUCU TARAFLI TEMİZLEME
    // ─────────────────────────────────────────────────────────────
    private IEnumerator TryServerPreprocess(string inputPath, string outputPath,
        Action<bool> callback)
    {
        byte[] fileData;
        try
        {
            fileData = File.ReadAllBytes(inputPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DynamicPrep] Dosya okunamadı: {e.Message}");
            callback(false);
            yield break;
        }

        string fileName = Path.GetFileName(inputPath);
        var form = new WWWForm();
        form.AddBinaryData("file", fileData, fileName, "text/csv");

        using var www = UnityWebRequest.Post(serverUrl, form);
        www.timeout = 30; // 30 saniye timeout
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[DynamicPrep] Sunucu hatası: {www.error}");
            callback(false);
            yield break;
        }

        try
        {
            byte[] cleanData = www.downloadHandler.data;
            File.WriteAllBytes(outputPath, cleanData);
            Debug.Log($"[DynamicPrep] Sunucu temizlemesi başarılı: {outputPath}");
            callback(true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DynamicPrep] Temizlenmiş dosya kaydedilemedi: {e.Message}");
            callback(false);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  YERELi TEMİZLEME — Sunucu yoksa Unity içinde çalışır
    //  Python'daki csv_preprocessor.py mantığının C# karşılığı
    // ─────────────────────────────────────────────────────────────
    private bool LocalClean(string inputPath, string outputPath)
    {
        try
        {
            // Encoding tespiti: BOM varsa utf-8-sig, yoksa utf-8
            string rawText = ReadFileWithBomDetection(inputPath);

            // Noktalı virgül → virgül (bazı Avrupa CSV'lerinde)
            string firstLine = rawText.Substring(0, Math.Min(rawText.Length, 300));
            int commas = firstLine.Split(',').Length - 1;
            int semis = firstLine.Split(';').Length - 1;
            if (semis > commas)
                rawText = Regex.Replace(rawText,
                    @";(?=(?:[^""]*""[^""]*"")*(?![^""]*""))", ",");

            string[] lines = rawText.Split(new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None);

            if (lines.Length < 2)
            {
                Debug.LogWarning("[DynamicPrep] CSV çok kısa, temizleme atlandı.");
                return false;
            }

            // ── Header temizleme ──────────────────────────────────
            string[] headers = ParseLine(lines[0]);
            for (int i = 0; i < headers.Length; i++)
                headers[i] = CleanColumnName(headers[i]);

            // Çakışan kolon adlarını düzelt
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                string h = headers[i];
                if (seen.ContainsKey(h))
                {
                    seen[h]++;
                    headers[i] = $"{h}_{seen[h]}";
                }
                else seen[h] = 1;
            }

            int colCount = headers.Length;

            // ── Veri satırlarını oku ──────────────────────────────
            var dataRows = new List<string[]>();
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cols = ParseLine(lines[i]);
                // Kolon sayısını header'a eşitle
                if (cols.Length < colCount)
                    Array.Resize(ref cols, colCount);
                dataRows.Add(cols);
            }

            // ── Her kolon için tip analizi ────────────────────────
            bool[] isNumeric = new bool[colCount];
            for (int c = 0; c < colCount; c++)
            {
                int numCount = 0;
                foreach (var row in dataRows)
                {
                    if (float.TryParse(row[c].Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                        numCount++;
                }
                isNumeric[c] = numCount >= dataRows.Count * 0.5f;
            }

            // ── Kategorik kolonlar için top-N limiti ──────────────
            var categoryMaps = new Dictionary<int, List<string>>(); // col → top cats
            for (int c = 0; c < colCount; c++)
            {
                if (isNumeric[c]) continue;
                var freq = new Dictionary<string, int>();
                foreach (var row in dataRows)
                {
                    string v = CleanCellValue(row[c]);
                    if (string.IsNullOrEmpty(v)) continue;
                    freq[v] = freq.ContainsKey(v) ? freq[v] + 1 : 1;
                }
                if (freq.Count > maxCategories)
                {
                    // Top N kategoriyi al
                    var sorted = new List<KeyValuePair<string, int>>(freq);
                    sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                    var top = new List<string>();
                    for (int k = 0; k < Math.Min(maxCategories, sorted.Count); k++)
                        top.Add(sorted[k].Key);
                    categoryMaps[c] = top;
                }
            }

            // ── Satır örnekleme ───────────────────────────────────
            List<string[]> sampledRows = dataRows;
            if (dataRows.Count > maxRows)
            {
                sampledRows = new List<string[]>();
                int step = dataRows.Count / maxRows;
                for (int i = 0; i < dataRows.Count; i += step)
                    sampledRows.Add(dataRows[i]);
                sampledRows = sampledRows.GetRange(0, Math.Min(sampledRows.Count, maxRows));
            }

            // ── CSV yaz ──────────────────────────────────────────
            var sb = new StringBuilder();

            // Header
            sb.AppendLine(string.Join(",", headers));

            // Satırlar
            foreach (var row in sampledRows)
            {
                string[] clean = new string[colCount];
                for (int c = 0; c < colCount; c++)
                {
                    if (c >= row.Length) { clean[c] = ""; continue; }

                    if (isNumeric[c])
                    {
                        // Sayısal: NaN → 0, float formatı
                        if (float.TryParse(row[c].Trim(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float fv))
                            clean[c] = fv.ToString("0.####",
                                System.Globalization.CultureInfo.InvariantCulture);
                        else
                            clean[c] = "0";
                    }
                    else
                    {
                        // Kategorik: virgül/tırnak temizle + kategori sınırı
                        string v = CleanCellValue(row[c]);
                        if (categoryMaps.ContainsKey(c) &&
                            !categoryMaps[c].Contains(v) &&
                            !string.IsNullOrEmpty(v))
                            v = "Diger";
                        clean[c] = v;
                    }
                }
                sb.AppendLine(string.Join(",", clean));
            }

            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[DynamicPrep] Yerel temizleme tamamlandı: " +
                      $"{sampledRows.Count} satır × {colCount} kolon → {outputPath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DynamicPrep] Yerel temizleme hatası: {e.Message}");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  YARDIMCI METODLAR
    // ─────────────────────────────────────────────────────────────

    private static string[] ParseLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        foreach (char ch in line)
        {
            if (ch == '"') { inQuotes = !inQuotes; }
            else if (ch == ',' && !inQuotes)
            { result.Add(current.ToString().Trim()); current.Clear(); }
            else { current.Append(ch); }
        }
        result.Add(current.ToString().Trim());
        return result.ToArray();
    }

    private static string CleanColumnName(string name)
    {
        name = name.Trim().Trim('"').Trim('\'');
        name = name.Replace('\uFEFF', ' ').Replace('\u200B', ' ');
        name = name.Replace(',', '_').Replace('"', ' ').Replace('\'', ' ');
        name = Regex.Replace(name, @"[^\w\s]", "_");
        name = Regex.Replace(name, @"\s+", "_").Trim('_');
        return string.IsNullOrEmpty(name) ? "Column" : name;
    }

    private static string CleanCellValue(string val)
    {
        val = val.Trim().Trim('"').Trim('\'');
        val = val.Replace(',', '_');
        val = val.Replace('\n', ' ').Replace('\r', ' ');
        return val.Trim();
    }

    private static string ReadFileWithBomDetection(string path)
    {
        byte[] raw = File.ReadAllBytes(path);
        // UTF-8 BOM kontrolü
        if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            return Encoding.UTF8.GetString(raw, 3, raw.Length - 3);
        return Encoding.UTF8.GetString(raw);
    }

    private static string GetCleanPath(string originalPath)
    {
        string dir = Path.GetDirectoryName(originalPath);
        string name = Path.GetFileNameWithoutExtension(originalPath);
        return Path.Combine(dir ?? "", name + "_dynamic.csv");
    }
}