from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.responses import Response
from fastapi.middleware.cors import CORSMiddleware
import pandas as pd
import io
import os

# Tekilleştirilmiş modüllerimiz
from analyzer import analyze_data, preprocess_bytes
from chart_generator import generate_3d_chart_base64

app = FastAPI(
    title="AR Data Discovery API",
    description="Mobil AR destekli veri keşif sistemi için tekil backend servisi.",
    version="2.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

ALLOWED_EXTENSIONS = {".csv", ".xlsx", ".xls"}

@app.get("/")
async def root():
    return {"status": "success", "message": "API Tekil Yapıda Başarıyla Çalışıyor!"}

@app.post("/api/v1/upload")
async def upload_file(file: UploadFile = File(...)):
    ext = os.path.splitext(file.filename)[1].lower()
    if ext not in ALLOWED_EXTENSIONS:
        raise HTTPException(400, f"Sadece CSV veya Excel kabul edilir. Uzantı: {ext}")
        
    try:
        contents = await file.read()
        df = pd.read_csv(io.BytesIO(contents)) if ext == ".csv" else pd.read_excel(io.BytesIO(contents))
        
        if df.empty:
            raise HTTPException(400, "Dosya tamamen boş!")

        analysis_result = analyze_data(df)
        if not analysis_result.get("success"):
            raise HTTPException(400, analysis_result.get("error_message", "Bilinmeyen hata."))

        if "recommended_charts" in analysis_result:
            for chart in analysis_result["recommended_charts"]:
                axes_dict = chart["axes"] if isinstance(chart, dict) else chart.axes
                chart_type_str = chart["chart_type"] if isinstance(chart, dict) else chart.chart_type
                data_processing_str = chart.get("data_processing", "none") if isinstance(chart, dict) else getattr(chart, "data_processing", "none")
                
                chart_render_data = generate_3d_chart_base64(df, axes_dict, chart_type_str, data_processing_str)
                
                if isinstance(chart, dict):
                    chart["chart_image_base64"] = chart_render_data["chart_image_base64"]
                    chart["scales"] = chart_render_data["scales"]
                else:
                    chart.chart_image_base64 = chart_render_data["chart_image_base64"]
                    chart.scales = chart_render_data["scales"]

        return {
            "status": "success",
            "filename": file.filename,
            "rows": len(df),
            "columns": len(df.columns),
            "analysis": analysis_result
        }
    except Exception as e:
        print(f"--- SUNUCU HATASI DETAYI --- \n {str(e)}")
        raise HTTPException(500, f"Dosya işlenirken hata oluştu: {str(e)}")

@app.post("/api/v1/preprocess")
async def preprocess_csv(file: UploadFile = File(...)):
    filename = file.filename or "data.csv"
    raw = await file.read()
    if len(raw) == 0:
        raise HTTPException(400, "Dosya boş.")
        
    try:
        clean_bytes, out_name = preprocess_bytes(raw, filename)
        return Response(
            content=clean_bytes,
            media_type="text/csv",
            headers={"Content-Disposition": f'attachment; filename="{out_name}"'}
        )
    except Exception as e:
        raise HTTPException(422, f"İşleme hatası: {str(e)}")