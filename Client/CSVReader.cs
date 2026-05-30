using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Globalization;

public static class CSVReader
{
    // Dosyadan X ve Y kolonlarýný okuyup, arayüze sýðacak þekilde 0 ile 1 arasýna oranlar
    public static List<Vector2> ReadAndNormalize(string filePath, string xCol, string yCol, int maxPoints = 300)
    {
        List<Vector2> points = new List<Vector2>();
        if (!File.Exists(filePath)) return points;

        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return points; // Veri yoksa boþ döndür

        // 1. Baþlýklarý bul ve istenen kolonlarýn kaçýncý sýrada olduðunu tespit et
        string[] headers = lines[0].Split(',');
        int xIndex = -1, yIndex = -1;

        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i].Trim();
            if (header == xCol) xIndex = i;
            if (header == yCol) yIndex = i;
        }

        if (xIndex == -1 || yIndex == -1) return points; // Kolonlar bulunamadýysa çýk

        List<float> rawX = new List<float>();
        List<float> rawY = new List<float>();

        // 2. Satýrlarý atlayarak oku ki telefon kilitlenmesin
        int step = Mathf.Max(1, (lines.Length - 1) / maxPoints);

        for (int i = 1; i < lines.Length; i += step)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] cols = lines[i].Split(',');

            if (cols.Length > Mathf.Max(xIndex, yIndex))
            {
                // Sayýlarý C# formatýna güvenle çevir
                if (float.TryParse(cols[xIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out float xVal) &&
                    float.TryParse(cols[yIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out float yVal))
                {
                    rawX.Add(xVal);
                    rawY.Add(yVal);
                }
            }
            if (rawX.Count >= maxPoints) break; // Maksimum nokta sýnýrýnda dur
        }

        if (rawX.Count == 0) return points;

        // 3. Verileri 0 ile 1 arasýna sýkýþtýr (Normalizasyon)
        float minX = rawX.Min(), maxX = rawX.Max();
        float minY = rawY.Min(), maxY = rawY.Max();

        for (int i = 0; i < rawX.Count; i++)
        {
            float normX = (maxX - minX == 0) ? 0.5f : (rawX[i] - minX) / (maxX - minX);
            float normY = (maxY - minY == 0) ? 0.5f : (rawY[i] - minY) / (maxY - minY);
            points.Add(new Vector2(normX, normY));
        }

        return points;
    }
}
