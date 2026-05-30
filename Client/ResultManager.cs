using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResultManager : MonoBehaviour
{
    public static ResultManager Instance;

    void Awake() { Instance = this; }

    public void ShowResults(ChartData[] charts, string filePath)
    {
        if (charts == null || charts.Length == 0) return;

        Canvas mainCanvas = Object.FindAnyObjectByType<Canvas>();
        if (mainCanvas == null) return;

        // Önceki kalıntıları temizle
        Transform oldScroll = mainCanvas.transform.Find("ScrollView");
        if (oldScroll != null) Destroy(oldScroll.gameObject);

        Transform oldPlusBtn = mainCanvas.transform.Find("PlusButton");
        if (oldPlusBtn != null) Destroy(oldPlusBtn.gameObject);

        // Ana ekranı temizle
        foreach (Transform child in mainCanvas.transform)
        {
            if (child.name != "Background" && child.name != "AppBar")
            {
                child.gameObject.SetActive(false);
            }
        }

        // Kaydırma Alanı (Scroll View) Oluştur
        GameObject scrollView = new GameObject("ScrollView");
        scrollView.transform.SetParent(mainCanvas.transform, false);
        RectTransform scrollRectTransform = scrollView.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0, 0);
        scrollRectTransform.anchorMax = new Vector2(1, 1);
        scrollRectTransform.offsetMin = new Vector2(0, 0);
        scrollRectTransform.offsetMax = new Vector2(0, -200);

        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollView.AddComponent<Image>().color = new Color(1, 1, 1, 0.01f);

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0, 0);
        viewportRect.anchorMax = new Vector2(1, 1);
        viewportRect.sizeDelta = Vector2.zero;
        viewport.AddComponent<Image>().color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(50, 50, 50, 250);
        vlg.spacing = 100;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = false;
        vlg.childControlWidth = false;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.MinSize;

        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;

        // --- KARTLARI ÇİZ ---
        for (int i = 0; i < charts.Length; i++)
        {
            ChartData chart = charts[i];

            GameObject card = new GameObject("ChartCard_" + i);
            card.transform.SetParent(content.transform, false);

            RectTransform cardRect = card.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(900, 1400);

            LayoutElement le = card.AddComponent<LayoutElement>();
            le.minHeight = 1400;
            le.minWidth = 900;

            Image cardImage = card.AddComponent<Image>();
            cardImage.color = Color.white;

            // Kart Başlığı
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(card.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(0, 150);
            titleRect.anchoredPosition = new Vector2(0, -50);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = (i + 1) + ". Öneri: " + chart.chart_type;
            titleText.fontSize = 65;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.35f, 0.15f, 0.65f);

            // Grafik Resmi
            GameObject imgObj = new GameObject("ChartImage");
            imgObj.transform.SetParent(card.transform, false);
            RectTransform imgRect = imgObj.AddComponent<RectTransform>();
            imgRect.anchorMin = new Vector2(0.5f, 0.5f);
            imgRect.anchorMax = new Vector2(0.5f, 0.5f);
            imgRect.pivot = new Vector2(0.5f, 0.5f);
            imgRect.sizeDelta = new Vector2(800, 600);
            imgRect.anchoredPosition = new Vector2(0, 180);

            if (!string.IsNullOrEmpty(chart.chart_image_base64))
            {
                Image chartImg = imgObj.AddComponent<Image>();
                byte[] imgBytes = System.Convert.FromBase64String(chart.chart_image_base64);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(imgBytes);
                chartImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                chartImg.preserveAspect = true;
            }

            // Bilgi Mesajı
            GameObject infoObj = new GameObject("InsightInfo");
            infoObj.transform.SetParent(card.transform, false);
            RectTransform infoRect = infoObj.AddComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0, 0);
            infoRect.anchorMax = new Vector2(1, 0);
            infoRect.pivot = new Vector2(0.5f, 0);
            infoRect.sizeDelta = new Vector2(-100, 280);
            infoRect.anchoredPosition = new Vector2(0, 210);

            string axisText = $"Eksenler: X({chart.axes.x}) - Y({chart.axes.y}) - Z({chart.axes.z})";
            if (!string.IsNullOrEmpty(chart.axes.color)) axisText += $"\nRenk: {chart.axes.color}";
            if (!string.IsNullOrEmpty(chart.axes.size)) axisText += $"\nBoyut: {chart.axes.size}";

            float percentScore = chart.score * 100f;
            string scoreText = $"İlişki Gücü: %{percentScore:F1}";

            TextMeshProUGUI infoText = infoObj.AddComponent<TextMeshProUGUI>();
            infoText.text = $"\n{axisText}\n{scoreText}";
            infoText.fontSize = 35;
            infoText.alignment = TextAlignmentOptions.Center;
            infoText.color = new Color(0.2f, 0.2f, 0.2f);
            infoText.textWrappingMode = TextWrappingModes.Normal;

            // --- DÜZELTİLEN AR İLE GÖSTER BUTONU ---
            GameObject btnObj = new GameObject("ARButton");
            btnObj.transform.SetParent(card.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0);
            btnRect.anchorMax = new Vector2(0.5f, 0);
            btnRect.pivot = new Vector2(0.5f, 0);
            btnRect.sizeDelta = new Vector2(600, 120);
            btnRect.anchoredPosition = new Vector2(0, 60);

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.35f, 0.15f, 0.65f);

            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "AR ile Göster";
            btnText.fontSize = 50;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            Button button = btnObj.AddComponent<Button>();
            string chartType = chart.chart_type;

            // Orijinal Kod Buraya Eksiksiz Geri Getirildi
            button.onClick.AddListener(() => {
                Debug.Log(chartType + " için AR Kamera Açılıyor...");
                if (ARManager.Instance != null)
                {
                    ARManager.Instance.ShowChartInAR(chart, filePath);
                }
                else
                {
                    Debug.LogError("Sahnede ARManager objesi yok!");
                }
            });
        }

        // --- YENİ: DİNAMİK GRAFİK İNCELEME KARTI VE BUTONU ---
        GameObject dynamicCard = new GameObject("DynamicCard");
        dynamicCard.transform.SetParent(content.transform, false);
        RectTransform dynCardRect = dynamicCard.AddComponent<RectTransform>();
        dynCardRect.sizeDelta = new Vector2(900, 400);
        LayoutElement dynLe = dynamicCard.AddComponent<LayoutElement>();
        dynLe.minHeight = 400;

        Image dynBg = dynamicCard.AddComponent<Image>();
        dynBg.color = new Color(0.9f, 0.9f, 0.95f);

        GameObject dynTextObj = new GameObject("InfoText");
        dynTextObj.transform.SetParent(dynamicCard.transform, false);
        RectTransform dynTextRect = dynTextObj.AddComponent<RectTransform>();
        dynTextRect.anchorMin = new Vector2(0, 0.5f);
        dynTextRect.anchorMax = new Vector2(1, 1);
        dynTextRect.offsetMin = new Vector2(20, 0);
        dynTextRect.offsetMax = new Vector2(-20, -20);

        TextMeshProUGUI dynText = dynTextObj.AddComponent<TextMeshProUGUI>();
        dynText.text = "3 Boyuttan daha fazlasını incelemek için Dinamik Grafiği başlatın.";
        dynText.fontSize = 40;
        dynText.color = Color.black;
        dynText.alignment = TextAlignmentOptions.Center;
        dynText.textWrappingMode = TextWrappingModes.Normal;

        GameObject dynBtnObj = new GameObject("DynamicButton");
        dynBtnObj.transform.SetParent(dynamicCard.transform, false);
        RectTransform dynBtnRect = dynBtnObj.AddComponent<RectTransform>();
        dynBtnRect.anchorMin = new Vector2(0.5f, 0);
        dynBtnRect.anchorMax = new Vector2(0.5f, 0);
        dynBtnRect.pivot = new Vector2(0.5f, 0);
        dynBtnRect.sizeDelta = new Vector2(700, 150);
        dynBtnRect.anchoredPosition = new Vector2(0, 50);
        dynBtnObj.AddComponent<Image>().color = new Color(0.1f, 0.6f, 0.3f);

        GameObject dBtnTextObj = new GameObject("Text");
        dBtnTextObj.transform.SetParent(dynBtnObj.transform, false);
        RectTransform dBtnTextRect = dBtnTextObj.AddComponent<RectTransform>();
        dBtnTextRect.anchorMin = Vector2.zero;
        dBtnTextRect.anchorMax = Vector2.one;
        dBtnTextRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI dBtnText = dBtnTextObj.AddComponent<TextMeshProUGUI>();
        dBtnText.text = "Dinamik İnceleme Yap";
        dBtnText.fontSize = 50;
        dBtnText.alignment = TextAlignmentOptions.Center;
        dBtnText.color = Color.white;

        Button dynButton = dynBtnObj.AddComponent<Button>();
        dynButton.onClick.AddListener(() => {
            Debug.Log("Dinamik AR Kurgusu Başlatılıyor...");
            mainCanvas.gameObject.SetActive(false);

            if (DynamicARManager.Instance != null)
            {
                // EKLENEN KISIM: Coroutine başlatmadan önce objenin aktif olduğundan emin ol
                if (!DynamicCSVPreprocessor.Instance.gameObject.activeInHierarchy)
                {
                    DynamicCSVPreprocessor.Instance.gameObject.SetActive(true);
                }

                DynamicCSVPreprocessor.Instance.PrepareAndStart(filePath);
            }
            else
            {
                Debug.LogError("Sahnede DynamicARManager objesi bulunamadı!");
            }
        });

        // --- YENİ DOSYA YÜKLEME (+) BUTONU ---
        GameObject plusBtnObj = new GameObject("PlusButton");
        plusBtnObj.transform.SetParent(mainCanvas.transform, false);

        RectTransform plusRect = plusBtnObj.AddComponent<RectTransform>();
        plusRect.anchorMin = new Vector2(1, 0);
        plusRect.anchorMax = new Vector2(1, 0);
        plusRect.pivot = new Vector2(1, 0);
        plusRect.sizeDelta = new Vector2(180, 180);
        plusRect.anchoredPosition = new Vector2(-60, 60);

        Image plusImage = plusBtnObj.AddComponent<Image>();
        plusImage.color = new Color(0.35f, 0.15f, 0.65f);

        GameObject plusTextObj = new GameObject("Text");
        plusTextObj.transform.SetParent(plusBtnObj.transform, false);
        RectTransform ptRect = plusTextObj.AddComponent<RectTransform>();
        ptRect.anchorMin = Vector2.zero;
        ptRect.anchorMax = Vector2.one;
        ptRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI plusText = plusTextObj.AddComponent<TextMeshProUGUI>();
        plusText.text = "+";
        plusText.fontSize = 120;
        plusText.alignment = TextAlignmentOptions.Center;
        plusText.color = Color.white;

        Button plusButton = plusBtnObj.AddComponent<Button>();
        plusButton.onClick.AddListener(() => {
            if (scrollView != null) Destroy(scrollView.gameObject);
            if (plusBtnObj != null) Destroy(plusBtnObj.gameObject);

            foreach (Transform child in mainCanvas.transform)
            {
                if (child != null)
                {
                    child.gameObject.SetActive(true);
                }
            }
        });
    }
}