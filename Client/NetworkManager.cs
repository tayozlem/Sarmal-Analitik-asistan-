using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
#endif

// --- JSON KALIPLARIMIZ ---
[System.Serializable]
public class ServerResponse
{
    public string status;
    public string filename;
    public AnalysisData analysis;
}

[System.Serializable]
public class AnalysisData
{
    public bool success;
    public ChartData[] recommended_charts;
}

[System.Serializable]
public class ChartData
{
    public int dimension;
    public float score;
    public string chart_type;
    public AxesData axes;
    public string insight_message;
    public string chart_image_base64;

    public ScaleData scales;
}

[System.Serializable]
public class AxesData
{
    public string x;
    public string y;
    public string z;
    public string color;
    public string size;
}

[System.Serializable]
public class ScaleData
{
    public float[] x;
    public float[] y;
    public float[] z;
    public float[] c;
}
// -----------------------------------------------------------------------------

public class NetworkManager : MonoBehaviour
{
#if UNITY_EDITOR
    private string apiUrl = "http://127.0.0.1:8000/api/v1/upload";
#else
    private string apiUrl = "http://192.168.1.158:8000/api/v1/upload";
#endif

    public void SelectAndUploadFile()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Veri Dosyası Seç (CSV/Excel)", "", "csv,xlsx");

        if (path.Length != 0)
        {
            Debug.Log("Bilgisayardan Seçilen Dosya: " + path);
            StartCoroutine(UploadFileCoroutine(path));
        }
        else
        {
            Debug.LogWarning("Dosya seçimi iptal edildi.");
        }
#else
        Debug.Log("Orijinal Android dosya seçici başlatılıyor...");
        OpenMobileFilePicker(); 
#endif
    }

    // 🚀 YENİ: ORİJİNAL ANDROID DOSYA SEÇİCİSİ
    private void OpenMobileFilePicker()
    {
        // Android'in kendi dosya sistemini aç ve sadece CSV dosyalarını filtrele
        string fileType = NativeFilePicker.ConvertExtensionToFileType("csv");

        NativeFilePicker.PickFile((path) =>
        {
            if (path != null)
            {
                Debug.Log("Mobilden Seçilen Dosya: " + path);

                // Kullanıcı dosyayı seçtiğinde Python sunucusuna yollayan kodu başlat
                StartCoroutine(UploadFileCoroutine(path));
            }
            else
            {
                Debug.LogWarning("Mobil dosya seçimi iptal edildi.");
            }
        }, new string[] { fileType });
    }

    private IEnumerator UploadFileCoroutine(string filePath)
    {
        string csvText = "";

        // 🚀 DÜZELTME: Sessiz çökmeyi engellemek için Try-Catch eklendi
        try
        {
            csvText = File.ReadAllText(filePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"🚨 ANDROID DOSYA OKUMA HATASI: {e.Message}");
            // Coroutine'in sessizce çökmesini engeller, işlemi kontrollü bitirir.
            yield break;
        }

        if (string.IsNullOrEmpty(csvText))
        {
            Debug.LogError("🚨 HATA: Dosya okundu ancak içeriği tamamen boş!");
            yield break;
        }

        csvText = csvText.Trim('\uFEFF', '\u200B');

        string firstLine = csvText.Substring(0, Mathf.Min(csvText.Length, 250));
        int commaCount = firstLine.Split(',').Length - 1;
        int semiColonCount = firstLine.Split(';').Length - 1;

        if (semiColonCount > commaCount)
        {
            Debug.LogWarning("⚠️ Noktalı virgül (;) tespit edildi! Otomatik olarak evrensel virgül (,) formatına dönüştürülüyor...");
            csvText = Regex.Replace(csvText, @";(?=(?:[^""]*""[^""]*"")*(?![^""]*""))", ",");
        }

        byte[] fileData = System.Text.Encoding.UTF8.GetBytes(csvText);
        string fileName = Path.GetFileName(filePath);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", fileData, fileName, "text/csv");

        Debug.Log("Python sunucusuna gönderiliyor... Lütfen bekleyin.");

        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Hata oluştu: " + www.error);
            }
            else
            {
                string jsonText = www.downloadHandler.text;

                try
                {
                    string pattern = @"(?<=[:,\[]\s*)(-?NaN|-?Infinity|-?inf|null)(?=\s*[,\]}])";
                    jsonText = Regex.Replace(jsonText, pattern, "0.0", RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Regex temizliği sırasında ufak bir pürüz: " + ex.Message);
                }

                if (!string.IsNullOrEmpty(jsonText) && !jsonText.Trim().StartsWith("{"))
                {
                    Debug.LogError("<color=red><b>🚨 SUNUCU HATASI! Python JSON yerine HTML/Hata Metni döndürdü. Ham Cevap:</b></color>\n" + jsonText);
                    yield break;
                }

                try
                {
                    ServerResponse responseData = JsonUtility.FromJson<ServerResponse>(jsonText);

                    if (responseData != null && responseData.analysis != null && responseData.analysis.recommended_charts != null && responseData.analysis.recommended_charts.Length > 0)
                    {
                        Debug.Log($"<color=cyan><b>VERİ BAŞARIYLA PARÇALANDI! Toplam {responseData.analysis.recommended_charts.Length} grafik önerisi bulundu.</b></color>");

                        ResultManager.Instance.ShowResults(responseData.analysis.recommended_charts, filePath);

                        for (int i = 0; i < responseData.analysis.recommended_charts.Length; i++)
                        {
                            ChartData chart = responseData.analysis.recommended_charts[i];

                            string xAxis = chart.axes.x;
                            string yAxis = chart.axes.y;
                            string zAxis = chart.axes.z;
                            float percentScore = chart.score * 100f;

                            Debug.Log($"<color=yellow>{i + 1}. Öneri:</color> {chart.chart_type}");
                            Debug.Log($"<color=green>Eksenler -> X: {xAxis} | Y: {yAxis} | Z: {zAxis}</color>");
                            Debug.Log($"<color=orange>İlişki Gücü:</color> %{percentScore:F1}");
                            Debug.Log($"<color=white>Mesaj:</color> {chart.insight_message}");
                        }
                    }
                    else
                    {
                        Debug.LogError("JSON başarılı şekilde okundu ancak beklenen analiz (recommended_charts) dizisi boş geldi.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"<color=red><b>JSON Parçalama Hatası:</b></color> {ex.Message}\n<color=yellow>JSON Metni:</color> {jsonText.Substring(0, Mathf.Min(jsonText.Length, 300))}...");
                }
            }
        }
    }
}