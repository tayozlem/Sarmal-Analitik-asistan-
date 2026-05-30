using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

public class DynamicARManager : MonoBehaviour
{
    public static DynamicARManager Instance;

    public Material pcpLineMaterial;
    public Gradient riskColorScale;

    private List<ARAxisController> allAxes = new List<ARAxisController>();
    private List<ARAxisController> activeAxes = new List<ARAxisController>();
    private List<LineRenderer> pcpLines = new List<LineRenderer>();

    // ── Renk Skalası (Lejant) Sistemi ──
    private ARAxisController activeColorAxis = null;
    private Dictionary<int, Color> activeCategoryColors = new Dictionary<int, Color>();
    private GameObject legendPanel;

    // ── UI ──
    private GameObject arCanvas;
    private GameObject contextMenuPanel;
    private TextMeshProUGUI contextMenuTitle;
    private ARAxisController menuTargetAxis;
    private GameObject bottomPanel;
    private GameObject exitButtonObj;

    // ── Input Yönetimi ──
    private ARAxisController dragAxis = null;
    private ARAxisController touchedAxis = null;
    private float dragZDistance = 4.0f;
    private Vector2 touchDownPos;
    private bool dragStarted = false;
    private const float DRAG_THRESHOLD = 14f;

    void Awake()
    {
        Instance = this;
        SetupColorScale();
    }

    void SetupColorScale()
    {
        riskColorScale = new Gradient();
        GradientColorKey[] gck = new GradientColorKey[3];
        gck[0].color = new Color(0.2f, 0.8f, 0.2f); gck[0].time = 0.0f;
        gck[1].color = new Color(1f, 0.8f, 0.1f); gck[1].time = 0.5f;
        gck[2].color = new Color(0.9f, 0.1f, 0.1f); gck[2].time = 1.0f;

        GradientAlphaKey[] gak = new GradientAlphaKey[2];
        gak[0].alpha = 0.7f; gak[0].time = 0.0f;
        gak[1].alpha = 0.7f; gak[1].time = 1.0f;

        riskColorScale.SetKeys(gck, gak);
    }

    public void StartDynamicSession(string csvFilePath)
    {
        foreach (var a in allAxes) if (a) Destroy(a.gameObject);
        allAxes.Clear();
        activeAxes.Clear();
        ClearLines();

        activeColorAxis = null;

        dragAxis = null; touchedAxis = null; dragStarted = false;

        string[] lines = File.ReadAllLines(csvFilePath);
        string[] headers = lines[0].Split(',');

        int colCount = headers.Length;
        for (int i = 0; i < colCount; i++)
        {
            var axis = SpawnAxis(headers[i], i, lines, colCount);
            axis.gameObject.SetActive(false);
            allAxes.Add(axis);
        }

        CreateUI();
    }

    ARAxisController SpawnAxis(string colName, int index, string[] lines, int totalCount)
    {
        GameObject rootObj = new GameObject("Axis_" + colName);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.transform.SetParent(rootObj.transform, false);
        visual.transform.localScale = new Vector3(0.015f, 0.5f, 0.015f);
        Renderer cylRend = visual.GetComponent<Renderer>();
        Material cylMat = new Material(Shader.Find("Sprites/Default"));
        cylMat.color = new Color(0.7f, 0.7f, 0.7f);
        cylMat.renderQueue = 3500;
        cylRend.material = cylMat;
        Destroy(visual.GetComponent<CapsuleCollider>());

        BoxCollider col = rootObj.AddComponent<BoxCollider>();
        col.size = new Vector3(0.6f, 1.2f, 0.2f);

        List<string> rawStrings = new List<string>();
        int floatCount = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            string v = cols.Length > index ? cols[index].Trim() : "";
            rawStrings.Add(v);
            if (!string.IsNullOrEmpty(v) && float.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                floatCount++;
        }

        bool isCategorical = floatCount < rawStrings.Count * 0.5f;
        List<float> rawDataList = new List<float>();
        List<string> uniqueCats = new List<string>();

        if (isCategorical)
        {
            uniqueCats = rawStrings.Distinct().Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (uniqueCats.Count == 0) uniqueCats.Add("Veri Yok");

            foreach (string s in rawStrings)
            {
                int ci = uniqueCats.IndexOf(s);
                rawDataList.Add(ci >= 0 ? ci : 0);
            }
        }
        else
        {
            foreach (string s in rawStrings)
            {
                float val = 0f;
                if (!string.IsNullOrEmpty(s)) float.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val);
                rawDataList.Add(val);
            }
        }

        CreateTextLabelWithBackground(rootObj.transform, colName, new Vector3(0, 0.65f, -0.05f), true);

        if (isCategorical)
        {
            int cc = uniqueCats.Count;
            for (int j = 0; j < cc; j++)
            {
                float t = cc > 1 ? (float)j / (cc - 1) : 0.5f;
                float yPos = Mathf.Lerp(-0.5f, 0.5f, t);
                CreateTextLabelWithBackground(rootObj.transform, uniqueCats[j], new Vector3(0.05f, yPos, -0.05f), false);
                CreateTick(rootObj.transform, yPos);
            }
        }
        else
        {
            float minVal = rawDataList.Count > 0 ? rawDataList.Min() : 0;
            float maxVal = rawDataList.Count > 0 ? rawDataList.Max() : 0;
            for (int j = 0; j <= 5; j++)
            {
                float t = j / 5f;
                float yPos = Mathf.Lerp(-0.5f, 0.5f, t);

                float actualValue = Mathf.Lerp(minVal, maxVal, t);
                string labelText = Mathf.RoundToInt(actualValue).ToString();

                CreateTextLabelWithBackground(rootObj.transform, labelText, new Vector3(0.05f, yPos, -0.05f), false);
                CreateTick(rootObj.transform, yPos);
            }
        }

        ARAxisController ctrl = rootObj.AddComponent<ARAxisController>();
        ctrl.columnName = colName;
        ctrl.rawData = rawDataList.ToArray();
        ctrl.normalizedData = NormalizeData(ctrl.rawData);

        ctrl.isCategorical = isCategorical;
        if (isCategorical) ctrl.categories = uniqueCats.ToArray();

        return ctrl;
    }

    void CreateTick(Transform parent, float yPos)
    {
        GameObject t = GameObject.CreatePrimitive(PrimitiveType.Cube);
        t.transform.SetParent(parent, false);
        t.transform.localPosition = new Vector3(0, yPos, 0);
        t.transform.localScale = new Vector3(0.04f, 0.004f, 0.004f);
        Renderer r = t.GetComponent<Renderer>();
        Material tickMat = new Material(Shader.Find("Sprites/Default"));
        tickMat.color = Color.black;
        tickMat.renderQueue = 3500;
        r.material = tickMat;
        Destroy(t.GetComponent<BoxCollider>());
    }

    void CreateTextLabelWithBackground(Transform parent, string text, Vector3 localPos, bool isHeader)
    {
        GameObject obj = new GameObject("Label_" + text);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);

        GameObject bgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bgQuad.transform.SetParent(obj.transform, false);
        Destroy(bgQuad.GetComponent<MeshCollider>());

        Renderer bgRend = bgQuad.GetComponent<Renderer>();
        Color bgColor = isHeader ? new Color(0.2f, 0.2f, 0.2f, 0.9f) : new Color(0.9f, 0.9f, 0.9f, 0.95f);
        Material bgMat = new Material(Shader.Find("Sprites/Default"));
        bgMat.color = bgColor;
        bgMat.renderQueue = 4000;
        bgRend.material = bgMat;
        bgQuad.transform.localPosition = new Vector3(0, 0, 0.015f);

        TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 20f; // Mobil için font büyütüldü
        tmp.color = isHeader ? Color.white : Color.black;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 10;
        if (isHeader) tmp.fontStyle = FontStyles.Bold;
        tmp.ForceMeshUpdate();

        tmp.fontMaterial.renderQueue = 4001;
        tmp.renderer.sortingOrder = 10;

        Vector2 textSize = tmp.GetRenderedValues(false);
        // Arka plan kutusu mobil ekranlarda daha rahat seçilsin diye genişletildi
        bgQuad.transform.localScale = new Vector3(textSize.x + 2.0f, textSize.y + 1.0f, 1f);

        obj.AddComponent<FaceCamera>();
    }

    float[] NormalizeData(float[] data)
    {
        if (data.Length == 0) return data;
        float min = data.Min(), max = data.Max();
        float[] n = new float[data.Length];
        for (int i = 0; i < data.Length; i++)
            n[i] = Mathf.Approximately(max, min) ? 0.5f : (data[i] - min) / (max - min);
        return n;
    }

    void Update()
    {
        HandleGlobalInput();
        DrawVisualization();
    }

    void HandleGlobalInput()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        bool usingTouch = Input.touchCount > 0;
        bool justReleased = usingTouch ? (Input.GetTouch(0).phase == TouchPhase.Ended || Input.GetTouch(0).phase == TouchPhase.Canceled) : Input.GetMouseButtonUp(0);

        if (justReleased)
        {
            if (dragStarted && dragAxis != null) dragAxis.SetDragging(false);
            dragAxis = null; touchedAxis = null; dragStarted = false;
            return;
        }

        if (!dragStarted && IsPointerOverUI(usingTouch)) return;

        Vector2 screenPos = usingTouch ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        bool justPressed = usingTouch ? Input.GetTouch(0).phase == TouchPhase.Began : Input.GetMouseButtonDown(0);

        if (justPressed)
        {
            touchedAxis = null; dragStarted = false; touchDownPos = screenPos;
            Ray ray = cam.ScreenPointToRay(screenPos);
            ARAxisController best = null;
            float bestDist = float.MaxValue;

            foreach (var axis in activeAxes)
            {
                if (axis.GetCollider().Raycast(ray, out RaycastHit hit, 20f))
                {
                    if (hit.distance < bestDist)
                    {
                        bestDist = hit.distance;
                        best = axis;
                    }
                }
            }

            if (best != null)
            {
                touchedAxis = best;
                dragZDistance = Vector3.Dot(best.transform.position - cam.transform.position, cam.transform.forward);
                best.isSelected = true;
                OpenContextMenuForAxis(best, screenPos);
            }
            else CloseContextMenu();

            return;
        }

        if (touchedAxis == null) return;

        float moved = Vector2.Distance(screenPos, touchDownPos);
        if (!dragStarted && moved > DRAG_THRESHOLD)
        {
            dragStarted = true;
            dragAxis = touchedAxis;
            dragAxis.SetDragging(true);
            CloseContextMenu();
        }

        if (dragStarted && dragAxis != null)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            float dist = dragZDistance / Vector3.Dot(ray.direction, cam.transform.forward);
            Vector3 targetWorld = cam.transform.position + ray.direction * dist;
            Vector3 targetScreen = cam.WorldToScreenPoint(targetWorld);

            float marginX = 80f;
            float marginTop = 150f;
            float marginBottom = 280f;

            targetScreen.x = Mathf.Clamp(targetScreen.x, marginX, Screen.width - marginX);
            targetScreen.y = Mathf.Clamp(targetScreen.y, marginBottom, Screen.height - marginTop);

            Vector3 clampedWorld = cam.ScreenToWorldPoint(targetScreen);
            clampedWorld.y = dragAxis.GetDefaultPos().y;

            dragAxis.transform.position = Vector3.Lerp(dragAxis.transform.position, clampedWorld, Time.deltaTime * 20f);

            Quaternion targetRot = Quaternion.LookRotation(new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z), Vector3.up);
            dragAxis.transform.rotation = Quaternion.Slerp(dragAxis.transform.rotation, targetRot, Time.deltaTime * 15f);
        }
    }

    bool IsPointerOverUI(bool usingTouch)
    {
        if (EventSystem.current == null) return false;

        // Mobil ve PC için %100 güvenilir ortak UI Raycast yöntemi
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = usingTouch ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0;
    }

    // ── RENKLENDİRME METOTLARI ──
    public void SetColorAxis(ARAxisController axis)
    {
        activeColorAxis = axis;
        activeCategoryColors.Clear();

        // Eksen renk skalası yapıldığında ekrandan gizle
        axis.gameObject.SetActive(false);
        if (activeAxes.Contains(axis)) activeAxes.Remove(axis);

        if (axis.isCategorical && axis.categories != null)
        {
            int count = axis.categories.Length;
            for (int i = 0; i < count; i++)
            {
                float hue = (float)i / count;
                activeCategoryColors[i] = Color.HSVToRGB(hue, 0.85f, 0.95f);
            }
        }
        UpdateLegendUI();
    }

    public void ClearColorAxis()
    {
        if (activeColorAxis != null)
        {
            activeColorAxis.gameObject.SetActive(true);
            if (!activeAxes.Contains(activeColorAxis)) activeAxes.Add(activeColorAxis);
            activeColorAxis = null;
        }
        UpdateLegendUI();
    }

    void DrawVisualization()
    {
        ClearLines();

        var selected = activeAxes.Where(a => a.isSelected).ToList();
        if (selected.Count < 2) return;

        selected.Sort((a, b) =>
        {
            float dA = Vector3.Dot(a.transform.position, Camera.main.transform.right);
            float dB = Vector3.Dot(b.transform.position, Camera.main.transform.right);
            return dA.CompareTo(dB);
        });

        int pcpIdx = 0;
        for (int i = 0; i < selected.Count - 1; i++)
        {
            pcpIdx = DrawPCP(selected[i], selected[i + 1], pcpIdx, selected);
        }
    }

    int DrawPCP(ARAxisController a, ARAxisController b, int idx, List<ARAxisController> allSelected)
    {
        int count = Mathf.Min(a.normalizedData.Length, b.normalizedData.Length, 300);
        Vector3 pushBackOffset = Camera.main.transform.forward * 0.15f;

        for (int i = 0; i < count; i++)
        {
            LineRenderer lr;
            if (idx >= pcpLines.Count)
            {
                GameObject lo = new GameObject("PCP_Line");
                lr = lo.AddComponent<LineRenderer>();
                lr.startWidth = lr.endWidth = 0.004f;
                if (pcpLineMaterial != null) lr.material = pcpLineMaterial;
                pcpLines.Add(lr);
            }
            else lr = pcpLines[idx];

            lr.positionCount = 2;
            Vector3 pA = a.transform.position - a.transform.up * 0.5f + a.transform.up * a.normalizedData[i] + pushBackOffset;
            Vector3 pB = b.transform.position - b.transform.up * 0.5f + b.transform.up * b.normalizedData[i] + pushBackOffset;
            lr.SetPosition(0, pA);
            lr.SetPosition(1, pB);

            // Renk skalası kapalıysa varsayılan (Magenta/Pembe) kullan. Açıksa atanan renkleri kullan.
            Color c = Color.magenta;

            if (activeColorAxis != null && activeColorAxis.isCategorical)
            {
                int catIndex = Mathf.RoundToInt(activeColorAxis.rawData[i]);
                if (activeCategoryColors.TryGetValue(catIndex, out Color mappedColor))
                {
                    c = mappedColor;
                }
            }

            lr.startColor = lr.endColor = c;
            idx++;
        }
        return idx;
    }

    void ClearLines()
    {
        foreach (var lr in pcpLines) lr.positionCount = 0;
    }

    // ── UI OLUŞTURMA KODLARI ──
    void CreateUI()
    {
        if (arCanvas != null) Destroy(arCanvas);

        arCanvas = new GameObject("AR_Canvas");
        Canvas cv = arCanvas.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 20;
        var scaler = arCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        arCanvas.AddComponent<GraphicRaycaster>();

        CreateContextMenuBase();
        CreateLegendUIBase();
        CreateBottomScrollUI();
        CreateExitButton();
    }

    void CreateExitButton()
    {
        exitButtonObj = new GameObject("ExitButton");
        exitButtonObj.transform.SetParent(arCanvas.transform, false);

        RectTransform rt = exitButtonObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        // Üst çentik (Safe Area) payı bırakıldı
        rt.anchoredPosition = new Vector2(40, -80);
        rt.sizeDelta = new Vector2(200, 90);

        Image img = exitButtonObj.AddComponent<Image>();
        img.color = new Color(0.8f, 0.1f, 0.1f, 0.95f);

        Button btn = exitButtonObj.AddComponent<Button>();
        btn.onClick.AddListener(ExitDynamicAR);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(exitButtonObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "Kapat";
        tmp.fontSize = 32;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    public void ExitDynamicAR()
    {
        foreach (var a in allAxes) if (a) Destroy(a.gameObject);
        allAxes.Clear();
        activeAxes.Clear();
        ClearLines();
        if (arCanvas) Destroy(arCanvas);

        // -- DÜZELTME: Kapatılan ana Canvas'ları tekrar aktif et --
        // Unity 6'ya uygun yeni arama metodu (gizli objeleri de bulur)
        Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in allCanvases)
        {
            // Eğer sildiğimiz AR_Canvas değilse, ana Canvas'ı tekrar görünür yap
            if (c != null && c.gameObject.name != "AR_Canvas")
            {
                c.gameObject.SetActive(true);
            }
        }

        var resultManagerObj = Object.FindAnyObjectByType<ResultManager>(FindObjectsInactive.Include);
        if (resultManagerObj != null)
        {
            resultManagerObj.gameObject.SetActive(true);
        }

        this.gameObject.SetActive(false);
    }

    void CreateBottomScrollUI()
    {
        bottomPanel = new GameObject("BottomScrollPanel");
        bottomPanel.transform.SetParent(arCanvas.transform, false);

        RectTransform bpRect = bottomPanel.AddComponent<RectTransform>();
        bpRect.anchorMin = new Vector2(0, 0);
        bpRect.anchorMax = new Vector2(1, 0);
        bpRect.pivot = new Vector2(0.5f, 0);
        bpRect.sizeDelta = new Vector2(0, 280); // Yükseklik mobilde rahatlık için artırıldı

        bottomPanel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.95f);

        ScrollRect scrollRect = bottomPanel.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(bottomPanel.transform, false);
        RectTransform vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        scrollRect.viewport = vpRect;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0); contentRect.anchorMax = new Vector2(0, 1);
        contentRect.pivot = new Vector2(0, 0.5f);
        contentRect.sizeDelta = new Vector2(0, 0);

        HorizontalLayoutGroup hlg = content.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(40, 40, 40, 40);
        hlg.spacing = 25;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlHeight = true;
        hlg.childControlWidth = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRect;

        foreach (var axis in allAxes) CreateColumnToggleButton(content.transform, axis);
    }

    void CreateColumnToggleButton(Transform parent, ARAxisController axis)
    {
        GameObject btnObj = new GameObject("Btn_" + axis.columnName);
        btnObj.transform.SetParent(parent, false);
        LayoutElement le = btnObj.AddComponent<LayoutElement>();
        le.preferredWidth = 300; // Mobilde genişletildi

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f);

        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = axis.columnName;
        // Metin sığmazsa otomatik küçülmesi sağlandı
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 20;
        tmp.fontSizeMax = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;

        btn.onClick.AddListener(() => {
            if (activeColorAxis == axis)
            {
                ClearColorAxis();
                img.color = new Color(0.1f, 0.6f, 0.3f);
                return;
            }

            bool willBeActive = !axis.gameObject.activeSelf;
            axis.gameObject.SetActive(willBeActive);
            img.color = willBeActive ? new Color(0.1f, 0.6f, 0.3f) : new Color(0.2f, 0.2f, 0.2f);

            if (willBeActive)
            {
                if (!activeAxes.Contains(axis)) activeAxes.Add(axis);
                axis.isSelected = true;

                Camera cam = Camera.main;
                float xOffset = ((activeAxes.Count - 1) * 0.7f) - 1.0f;
                Vector3 spawnPos = cam.transform.position + cam.transform.forward * 4.0f + cam.transform.right * xOffset;

                axis.transform.position = spawnPos;
                axis.transform.rotation = Quaternion.LookRotation(new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z), Vector3.up);
                axis.SetDefaultPos(spawnPos, axis.transform.rotation);
            }
            else
            {
                activeAxes.Remove(axis);
                axis.isSelected = false;
                if (menuTargetAxis == axis) CloseContextMenu();
                if (dragAxis == axis) { dragStarted = false; dragAxis = null; }
            }
        });
    }

    void CreateLegendUIBase()
    {
        legendPanel = new GameObject("LegendPanel");
        legendPanel.transform.SetParent(arCanvas.transform, false);

        RectTransform rt = legendPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        // Panelin hemen üstüne konumlandırıldı
        rt.anchoredPosition = new Vector2(-20, 300);
        rt.sizeDelta = new Vector2(300, 0);

        legendPanel.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        VerticalLayoutGroup vlg = legendPanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(15, 15, 15, 15);
        vlg.spacing = 10;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;

        ContentSizeFitter csf = legendPanel.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        legendPanel.SetActive(false);
    }

    void UpdateLegendUI()
    {
        if (activeColorAxis == null || !activeColorAxis.isCategorical)
        {
            legendPanel.SetActive(false);
            return;
        }

        foreach (Transform child in legendPanel.transform) Destroy(child.gameObject);

        var headerRow = new GameObject("HeaderRow");
        headerRow.transform.SetParent(legendPanel.transform, false);
        var headerHlg = headerRow.AddComponent<HorizontalLayoutGroup>();
        headerHlg.childAlignment = TextAnchor.MiddleCenter;
        var headerLe = headerRow.AddComponent<LayoutElement>();
        headerLe.minHeight = 45;

        var titleGo = new GameObject("LegendTitle");
        titleGo.transform.SetParent(headerRow.transform, false);
        var txt = titleGo.AddComponent<TextMeshProUGUI>();
        txt.text = activeColorAxis.columnName;
        txt.fontSize = 24;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Left;

        var closeBtnObj = new GameObject("CloseBtn");
        closeBtnObj.transform.SetParent(headerRow.transform, false);
        var closeLe = closeBtnObj.AddComponent<LayoutElement>();
        closeLe.minWidth = closeLe.preferredWidth = 45; // Çarpı butonu genişletildi
        closeLe.minHeight = closeLe.preferredHeight = 45;
        var closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.color = new Color(0.8f, 0.2f, 0.2f);
        var closeBtn = closeBtnObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(ClearColorAxis);

        var closeTxtObj = new GameObject("Txt");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        var closeTxtRt = closeTxtObj.AddComponent<RectTransform>();
        closeTxtRt.anchorMin = Vector2.zero; closeTxtRt.anchorMax = Vector2.one;
        closeTxtRt.sizeDelta = Vector2.zero;
        var closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "X";
        closeTxt.fontSize = 26;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.fontStyle = FontStyles.Bold;

        var sep = new GameObject("Sep");
        sep.transform.SetParent(legendPanel.transform, false);
        sep.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 2);
        sep.AddComponent<Image>().color = new Color(1, 1, 1, 0.3f);
        var le = sep.AddComponent<LayoutElement>();
        le.minHeight = 2;

        for (int i = 0; i < activeColorAxis.categories.Length; i++)
        {
            var row = new GameObject("Row_" + i);
            row.transform.SetParent(legendPanel.transform, false);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            var colorBox = new GameObject("Color");
            colorBox.transform.SetParent(row.transform, false);
            var boxRt = colorBox.AddComponent<RectTransform>();
            boxRt.sizeDelta = new Vector2(35, 35);
            colorBox.AddComponent<Image>().color = activeCategoryColors[i];
            var boxLe = colorBox.AddComponent<LayoutElement>();
            boxLe.minWidth = boxLe.minHeight = boxLe.preferredWidth = boxLe.preferredHeight = 35;

            var label = new GameObject("Label");
            label.transform.SetParent(row.transform, false);
            var lTxt = label.AddComponent<TextMeshProUGUI>();
            lTxt.text = activeColorAxis.categories[i];
            lTxt.fontSize = 22;
            lTxt.color = Color.white;
            lTxt.alignment = TextAlignmentOptions.Left;
        }

        legendPanel.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(legendPanel.GetComponent<RectTransform>());
    }

    void CreateContextMenuBase()
    {
        contextMenuPanel = new GameObject("ContextMenu");
        contextMenuPanel.transform.SetParent(arCanvas.transform, false);

        RectTransform pr = contextMenuPanel.AddComponent<RectTransform>();
        pr.pivot = new Vector2(0f, 1f);
        pr.anchorMin = pr.anchorMax = Vector2.zero;
        pr.sizeDelta = new Vector2(420, 0); // Menü genişliği artırıldı
        contextMenuPanel.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.07f, 0.96f);

        var vlg = contextMenuPanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.spacing = 12;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;

        var csf = contextMenuPanel.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        contextMenuPanel.SetActive(false);
    }

    public void OpenContextMenuForAxis(ARAxisController axis, Vector2 screenPos)
    {
        menuTargetAxis = axis;
        contextMenuPanel.SetActive(true);

        foreach (Transform child in contextMenuPanel.transform) Destroy(child.gameObject);

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(contextMenuPanel.transform, false);
        titleGO.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 50);
        contextMenuTitle = titleGO.AddComponent<TextMeshProUGUI>();
        contextMenuTitle.fontSize = 34; // Başlık büyütüldü
        contextMenuTitle.color = new Color(0.4f, 0.85f, 1f);
        contextMenuTitle.alignment = TextAlignmentOptions.Center;
        contextMenuTitle.fontStyle = FontStyles.Bold;
        contextMenuTitle.text = axis.columnName;

        AddSeparator();

        if (axis.isCategorical && axis.categories != null && activeColorAxis != axis)
        {
            AddMenuBtn("Renk Skalası Olarak Seç", new Color(0.5f, 0.2f, 0.6f), () => {
                SetColorAxis(axis);
                CloseContextMenu();
            });
            AddSeparator();
        }

        AddMenuBtn("<- Seçimi Bırak", new Color(0.55f, 0.15f, 0.15f), () => { menuTargetAxis?.ResetToDefault(); CloseContextMenu(); });
        AddMenuBtn("X Kapat", new Color(0.15f, 0.15f, 0.15f), () => CloseContextMenu());

        LayoutRebuilder.ForceRebuildLayoutImmediate(contextMenuPanel.GetComponent<RectTransform>());

        RectTransform pr = contextMenuPanel.GetComponent<RectTransform>();
        float pw = pr.sizeDelta.x;
        float ph = pr.rect.height > 0 ? pr.rect.height : 300f;

        float x = Mathf.Clamp(screenPos.x + 20f, 0, Screen.width - pw - 10f);
        float y = Mathf.Clamp(screenPos.y + 20f, ph + 10f, Screen.height - 210f);
        pr.anchoredPosition = new Vector2(x, y);
    }

    void AddSeparator()
    {
        var go = new GameObject("Sep");
        go.transform.SetParent(contextMenuPanel.transform, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(0, 4);
        go.AddComponent<Image>().color = new Color(1, 1, 1, 0.2f);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 4;
    }

    void AddMenuBtn(string label, Color col, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject("Btn_" + label);
        go.transform.SetParent(contextMenuPanel.transform, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 90; // Buton yüksekliği artırıldı

        var img = go.AddComponent<Image>();
        img.color = col;

        var btn = go.AddComponent<Button>();
        var cb = btn.colors;
        cb.highlightedColor = new Color(Mathf.Min(col.r + 0.2f, 1f), Mathf.Min(col.g + 0.2f, 1f), Mathf.Min(col.b + 0.2f, 1f));
        cb.pressedColor = new Color(col.r * 0.7f, col.g * 0.7f, col.b * 0.7f);
        btn.colors = cb;
        btn.onClick.AddListener(action);

        var tGO = new GameObject("Lbl");
        tGO.transform.SetParent(go.transform, false);
        var tr = tGO.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(15, 0); tr.offsetMax = new Vector2(-15, 0);
        var tmp = tGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18;
        tmp.fontSizeMax = 28;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    public void CloseContextMenu()
    {
        contextMenuPanel?.SetActive(false);
        menuTargetAxis = null;
    }
}

public class FaceCamera : MonoBehaviour
{
    Transform cam;
    void Start() { if (Camera.main) cam = Camera.main.transform; }
    void LateUpdate()
    {
        if (cam) transform.LookAt(transform.position + cam.rotation * Vector3.forward, cam.rotation * Vector3.up);
    }
}