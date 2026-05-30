using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModernUIBuilder : MonoBehaviour
{
    void Start()
    {
        // 1. Ana Arka Planı Oluştur
        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(transform, false);
        bgObj.GetComponent<Image>().color = new Color(0.96f, 0.96f, 0.98f); // Kırık beyaz
        SetStretch(bgObj.GetComponent<RectTransform>());

        // 2. App Bar (Mor Üst Menü)
        GameObject appBarObj = new GameObject("AppBar", typeof(RectTransform), typeof(Image));
        appBarObj.transform.SetParent(transform, false);
        ColorUtility.TryParseHtmlString("#5E35B1", out Color morRenk);
        appBarObj.GetComponent<Image>().color = morRenk;
        RectTransform appRect = appBarObj.GetComponent<RectTransform>();
        appRect.anchorMin = new Vector2(0, 1); appRect.anchorMax = new Vector2(1, 1);
        appRect.pivot = new Vector2(0.5f, 1);
        appRect.sizeDelta = new Vector2(0, 200); // Üst menü yüksekliği
        appRect.anchoredPosition = Vector2.zero;

        // 3. Başlık Yazısı (Sola Yatık, Kibar ve Zarif)
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(appBarObj.transform, false);
        TextMeshProUGUI titleTxt = titleObj.GetComponent<TextMeshProUGUI>();
        titleTxt.text = "Sarmal Analitik Asistanı";
        titleTxt.color = Color.white;
        titleTxt.fontSize = 55;
        titleTxt.fontStyle = FontStyles.Normal;
        titleTxt.alignment = TextAlignmentOptions.Left;
        titleTxt.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        SetStretch(titleRect);
        titleRect.offsetMin = new Vector2(70, 0);
        titleRect.offsetMax = new Vector2(0, 0);

        // 4. Dosya Seç Butonu (Ortada ve YUKARIDA)
        GameObject btnObj = new GameObject("UploadButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(transform, false);
        btnObj.GetComponent<Image>().color = morRenk;

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        // 🚀 DÜZELTME: Buton yatayda ortada, dikeyde ise üstten aşağıya doğru konumlanacak şekilde ayarlandı.
        btnRect.anchorMin = new Vector2(0.5f, 1f);
        btnRect.anchorMax = new Vector2(0.5f, 1f);
        btnRect.pivot = new Vector2(0.5f, 1f);

        // 🚀 DÜZELTME: Buton biraz daha kibar bir boyuta getirildi ve Y ekseninde yukarı taşındı (-300 birim aşağıda).
        btnRect.sizeDelta = new Vector2(400, 120);
        btnRect.anchoredPosition = new Vector2(0, -350);

        // 5. Buton Yazısı
        GameObject btnTxtObj = new GameObject("BtnText", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI btnTxt = btnTxtObj.GetComponent<TextMeshProUGUI>();
        btnTxt.text = "Dosya Seç";
        btnTxt.color = Color.white;
        btnTxt.fontSize = 45; // 🚀 DÜZELTME: Yazı boyutu da butona uygun şekilde küçültüldü.
        btnTxt.fontStyle = FontStyles.Bold;
        btnTxt.alignment = TextAlignmentOptions.Center;
        SetStretch(btnTxtObj.GetComponent<RectTransform>());

        // 6. Python'a Veri Gönderen Kodu Butona Bağlama
        NetworkManager netManager = FindFirstObjectByType<NetworkManager>();
        if (netManager != null)
        {
            btnObj.GetComponent<Button>().onClick.AddListener(netManager.SelectAndUploadFile);
        }
    }

    // Objelerin içini tam doldurması için yardımcı fonksiyon
    void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}