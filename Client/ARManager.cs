using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System;
using System.Linq;

public class PointDataInfo : MonoBehaviour
{
    public string info;
}

public struct PointCoords
{
    public float x, y, z, c, s;
}

public class ARManager : MonoBehaviour
{
    public static ARManager Instance;

    private GameObject arUI;
    private GameObject currentChartRoot;
    private GameObject currentChartRotator;
    private List<Transform> billboardItems = new List<Transform>();

    private GameObject tooltipObj;
    private TextMeshPro tooltipText;

    private Vector2 lastMousePosition;
    private bool isDragging = false;

    // ============================================================
    // MOBİL GÜVENLİ SHADER SİSTEMİ
    // ============================================================

    private static Material _matOpaque;
    private static Material _matTransparent;
    private static Material _matUnlitColor;
    private static Material _matUnlitTexture;
    private static Material _matSprites;

    private static Material GetOpaqueMaterial()
    {
        if (_matOpaque == null)
            _matOpaque = CreateMaterialFromShaderChain(false,
                "Universal Render Pipeline/Lit",
                "Standard",
                "Mobile/Diffuse",
                "Diffuse");
        return _matOpaque;
    }

    private static Material GetTransparentMaterial()
    {
        if (_matTransparent == null)
        {
            _matTransparent = CreateMaterialFromShaderChain(true,
                "Universal Render Pipeline/Lit",
                "Standard",
                "Mobile/Diffuse",
                "Transparent/Diffuse",
                "Diffuse");
        }
        return _matTransparent;
    }

    private static Material GetUnlitColorMaterial()
    {
        if (_matUnlitColor == null)
            _matUnlitColor = CreateMaterialFromShaderChain(false,
                "Universal Render Pipeline/Unlit",
                "Unlit/Color",
                "Mobile/Unlit (Supports Lightmap)",
                "Sprites/Default");
        return _matUnlitColor;
    }

    private static Material GetUnlitTextureMaterial()
    {
        if (_matUnlitTexture == null)
            _matUnlitTexture = CreateMaterialFromShaderChain(false,
                "Universal Render Pipeline/Unlit",
                "Unlit/Texture",
                "Mobile/Unlit (Supports Lightmap)",
                "Sprites/Default");
        return _matUnlitTexture;
    }

    private static Material GetSpritesMaterial()
    {
        if (_matSprites == null)
            _matSprites = CreateMaterialFromShaderChain(false,
                "Sprites/Default",
                "Universal Render Pipeline/Unlit",
                "Unlit/Color",
                "Mobile/Unlit (Supports Lightmap)");
        return _matSprites;
    }

    private static Material CreateMaterialFromShaderChain(bool transparent, params string[] shaderNames)
    {
        Shader found = null;
        foreach (string name in shaderNames)
        {
            Shader s = Shader.Find(name);
            if (s != null) { found = s; break; }
        }

        if (found == null)
            found = Shader.Find("Sprites/Default");

        Material mat = new Material(found);

        bool isURP = found.name.Contains("Universal Render Pipeline") || found.name.Contains("URP");
        bool isStandard = found.name == "Standard";

        if (transparent)
        {
            if (isURP)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 1f);   // ZWrite açık → siyah artefakt önlenir
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 2999;
            }
            else if (isStandard)
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 1f);   // ZWrite açık → siyah artefakt önlenir
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 2999;         // Transparent queue, ZWrite ile uyumlu
            }
        }
        else
        {
            if (isURP)
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }
            else if (isStandard)
            {
                mat.SetFloat("_Mode", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = -1;
            }
        }

        return mat;
    }

    private static Material NewOpaqueMat(Color color)
    {
        Material m = new Material(GetOpaqueMaterial());
        m.color = color;
        return m;
    }

    private static Material NewTransparentMat(Color color)
    {
        Material m = new Material(GetTransparentMaterial());
        m.color = color;
        return m;
    }

    private static Material NewUnlitColorMat(Color color)
    {
        Material m = new Material(GetUnlitColorMaterial());
        m.color = color;
        return m;
    }

    private static Material NewUnlitTextureMat(Texture2D tex)
    {
        Material m = new Material(GetUnlitTextureMaterial());
        m.mainTexture = tex;
        return m;
    }

    private static Material NewSpritesMat(Color color)
    {
        Material m = new Material(GetSpritesMaterial());
        m.color = color;
        return m;
    }

    void Awake() { Instance = this; }

    void Update()
    {
        if (currentChartRotator != null)
        {
            bool isMobile = SystemInfo.deviceType == DeviceType.Handheld;

            // =====================================
            // 1. ZOOM KONTROLLERİ
            // =====================================
            if (isMobile && Input.touchCount == 2)
            {
                Touch touchZero = Input.GetTouch(0);
                Touch touchOne = Input.GetTouch(1);

                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;
                float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                Vector3 newScale = currentChartRoot.transform.localScale - new Vector3(1, 1, 1) * deltaMagnitudeDiff * 0.005f;
                newScale.x = Mathf.Clamp(newScale.x, 0.1f, 2.0f);
                newScale.y = Mathf.Clamp(newScale.y, 0.1f, 2.0f);
                newScale.z = Mathf.Clamp(newScale.z, 0.1f, 2.0f);

                currentChartRoot.transform.localScale = newScale;
                isDragging = false;
                HideTooltip();
            }
            else if (!isMobile)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    Vector3 newScale = currentChartRoot.transform.localScale + new Vector3(1, 1, 1) * scroll * 0.5f;
                    newScale.x = Mathf.Clamp(newScale.x, 0.5f, 3.0f);
                    newScale.y = Mathf.Clamp(newScale.y, 0.5f, 3.0f);
                    newScale.z = Mathf.Clamp(newScale.z, 0.5f, 3.0f);
                    currentChartRoot.transform.localScale = newScale;
                    HideTooltip();
                }
            }

            // =====================================
            // 2. DOKUNMA, TOOLTIP VE DÖNDÜRME
            // =====================================
            if (Input.touchCount < 2)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    lastMousePosition = Input.mousePosition;
                    isDragging = false; // Henüz drag değil
                }
                else if (Input.GetMouseButton(0))
                {
                    Vector2 mouseDelta = (Vector2)Input.mousePosition - lastMousePosition;
                    float dragThreshold = isMobile ? 18.0f : 4.0f;

                    if (!isDragging && mouseDelta.magnitude > dragThreshold)
                    {
                        isDragging = true;
                        HideTooltip();
                    }

                    if (isDragging)
                    {
                        float rSpeed = isMobile ? 0.2f : 0.5f;
                        float rotX = -((Vector2)Input.mousePosition - lastMousePosition).x * rSpeed;
                        currentChartRotator.transform.Rotate(Vector3.up, rotX, Space.World);
                        lastMousePosition = Input.mousePosition;
                    }
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    if (!isDragging && Camera.main != null)
                    {
                        // Sürükleme yok → tap/tıklama, tooltip göster
                        Ray ray = Camera.main.ScreenPointToRay(lastMousePosition);
                        float hitRadius = isMobile ? 0.12f : 0.02f;
                        if (Physics.SphereCast(ray, hitRadius, out RaycastHit hit, 50f))
                        {
                            PointDataInfo data = hit.collider.GetComponent<PointDataInfo>();
                            if (data != null) ShowTooltip(data.info, hit.collider.transform.position);
                            else HideTooltip();
                        }
                        else { HideTooltip(); }
                    }
                    isDragging = false;
                }
            }
        }

        // Billboard: Yazılar her zaman kameraya baksın
        if (Camera.main != null && billboardItems.Count > 0)
        {
            Quaternion camRotation = Camera.main.transform.rotation;
            foreach (Transform item in billboardItems)
            {
                if (item != null) item.rotation = camRotation;
            }
        }
    }

    public void ShowChartInAR(ChartData chart, string filePath)
    {
        Canvas mainCanvas = FindFirstObjectByType<Canvas>();
        if (mainCanvas != null) mainCanvas.gameObject.SetActive(false);

        if (currentChartRoot != null) Destroy(currentChartRoot);
        billboardItems.Clear();

        CreateARUI(mainCanvas);
        Generate3DChart(chart, filePath);
    }

    private void CreateARUI(Canvas mainCanvas)
    {
        if (arUI != null) Destroy(arUI);

        arUI = new GameObject("AR_Canvas");
        Canvas canvas = arUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        arUI.AddComponent<CanvasScaler>();
        arUI.AddComponent<GraphicRaycaster>();

        GameObject backBtn = new GameObject("BackButton");
        backBtn.transform.SetParent(arUI.transform, false);
        RectTransform btnRect = backBtn.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0, 1); btnRect.anchorMax = new Vector2(0, 1);
        btnRect.pivot = new Vector2(0, 1); btnRect.anchoredPosition = new Vector2(50, -50);
        btnRect.sizeDelta = new Vector2(300, 120);

        Image btnImage = backBtn.AddComponent<Image>();
        btnImage.color = new Color(0.8f, 0.2f, 0.2f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(backBtn.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI btnText = textObj.AddComponent<TextMeshProUGUI>();
        btnText.text = "X Kapat";
        btnText.fontSize = 50; btnText.alignment = TextAlignmentOptions.Center; btnText.color = Color.white;

        Button button = backBtn.AddComponent<Button>();
        button.onClick.AddListener(() => {
            if (currentChartRoot != null) Destroy(currentChartRoot);
            if (tooltipObj != null) Destroy(tooltipObj);
            billboardItems.Clear();
            Destroy(arUI);
            if (mainCanvas != null) mainCanvas.gameObject.SetActive(true);
        });
    }

    private void Generate3DChart(ChartData chart, string filePath)
    {
        currentChartRoot = new GameObject("3D_Chart_Root");
        Transform camTransform = Camera.main.transform;

        bool isMobile = SystemInfo.deviceType == DeviceType.Handheld;

        if (isMobile)
        {
            // MOBİL: Kameradan 2.0m önde — daha uzak = tanecikler daha küçük görünür
            // Scale = 0.5 → grafiğin fiziksel boyutu büyür, tanecikler daha seyrek görünür
            Vector3 flatForward = new Vector3(camTransform.forward.x, 0, camTransform.forward.z).normalized;
            if (flatForward == Vector3.zero) flatForward = Vector3.forward;

            currentChartRoot.transform.position = camTransform.position + flatForward * 2.0f + Vector3.down * 0.3f;
            currentChartRoot.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // FIX 1: 0.35 → 0.5
        }
        else
        {
            // PC: Orijinal ayarlar korunuyor
            currentChartRoot.transform.position = camTransform.position + camTransform.forward * 3.5f + Vector3.down * 0.5f;
            currentChartRoot.transform.localScale = Vector3.one;
        }

        currentChartRotator = new GameObject("Chart_Rotator");
        currentChartRotator.transform.SetParent(currentChartRoot.transform);
        currentChartRotator.transform.localPosition = Vector3.zero;

        // Base plate
        GameObject basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePlate.transform.SetParent(currentChartRotator.transform);
        basePlate.transform.localPosition = Vector3.zero;
        basePlate.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
        basePlate.GetComponent<Renderer>().material = NewOpaqueMat(new Color(0.15f, 0.15f, 0.15f, 1f));
        Destroy(basePlate.GetComponent<BoxCollider>());

        PlotRealData(chart, filePath);
        InitializeTooltip();
    }

    private string[] ParseCSVLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string currentVal = "";
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"') { inQuotes = !inQuotes; }
            else if (c == ',' && !inQuotes) { result.Add(currentVal.Trim()); currentVal = ""; }
            else { currentVal += c; }
        }
        result.Add(currentVal.Trim());
        return result.ToArray();
    }

    private float ParseAxisValue(string rawVal, Dictionary<float, string> catMap)
    {
        if (string.IsNullOrWhiteSpace(rawVal)) return 0f;
        if (float.TryParse(rawVal, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out float parsed)) return parsed;
        foreach (var kvp in catMap) { if (kvp.Value == rawVal.Trim()) return kvp.Key; }
        return 0f;
    }

    private string FormatAxisLabel(float val, Dictionary<float, string> catMap)
    {
        if (catMap.Count > 0)
        {
            int closest = Mathf.RoundToInt(val);
            if (Mathf.Abs(val - closest) < 0.1f && catMap.ContainsKey(closest))
            {
                string lbl = catMap[closest];
                if (System.DateTime.TryParse(lbl, out System.DateTime dt)) return dt.ToString("yyyy-MM-dd");
                if (lbl.Length > 10) return lbl.Substring(0, 10) + ".";
                return lbl;
            }
            return "";
        }
        float absVal = Mathf.Abs(val);
        if (absVal >= 1000000000f) return (val / 1000000000f).ToString("0.##") + "B";
        if (absVal >= 1000000f) return (val / 1000000f).ToString("0.##") + "M";
        if (absVal >= 1000f) return (val / 1000f).ToString("0.##") + "K";
        return val.ToString("0.##");
    }

    private Color GetPlasmaColor(float t)
    {
        t = Mathf.Clamp01(t);
        if (t <= 0.25f) return Color.Lerp(new Color(0.05f, 0f, 0.1f), new Color(0.7f, 0.1f, 0.45f), t * 4f);
        if (t <= 0.5f) return Color.Lerp(new Color(0.7f, 0.1f, 0.45f), new Color(0.9f, 0.5f, 0.1f), (t - 0.25f) * 4f);
        return Color.Lerp(new Color(0.9f, 0.5f, 0.1f), new Color(0.95f, 0.9f, 0.1f), (t - 0.5f) * 2f);
    }

    private static void CalculateNiceScale(float min, float max, out float niceMin, out float niceMax, out float niceStep)
    {
        float range = max - min;
        if (range == 0) { niceMin = min - 1; niceMax = max + 1; niceStep = 1; return; }

        float rawStep = range / 4f;
        float exponent = Mathf.Floor(Mathf.Log10(rawStep));
        float fraction = rawStep / Mathf.Pow(10, exponent);

        float niceFraction;
        if (fraction <= 1.0f) niceFraction = 1f;
        else if (fraction <= 2.0f) niceFraction = 2f;
        else if (fraction <= 2.5f) niceFraction = 2.5f;
        else if (fraction <= 5.0f) niceFraction = 5f;
        else niceFraction = 10f;

        niceStep = niceFraction * Mathf.Pow(10, exponent);
        niceMin = Mathf.Floor(min / niceStep) * niceStep;
        niceMax = Mathf.Ceil(max / niceStep) * niceStep;
    }

    private void PlotRealData(ChartData chart, string filePath)
    {
        if (!File.Exists(filePath)) return;

        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length <= 1) return;

        string[] headers = ParseCSVLine(lines[0]);
        for (int i = 0; i < headers.Length; i++) headers[i] = headers[i].Trim();

        int xIdx = Array.IndexOf(headers, chart.axes.x.Trim());
        int yIdx = Array.IndexOf(headers, chart.axes.y.Trim());
        int zIdx = Array.IndexOf(headers, chart.axes.z.Trim());
        int cIdx = string.IsNullOrEmpty(chart.axes.color) ? -1 : Array.IndexOf(headers, chart.axes.color.Trim());
        int sIdx = string.IsNullOrEmpty(chart.axes.size) ? -1 : Array.IndexOf(headers, chart.axes.size.Trim());

        if (xIdx == -1 || yIdx == -1 || zIdx == -1) return;

        List<PointCoords> rawPoints = new List<PointCoords>();
        List<string[]> rawRows = new List<string[]>();

        Dictionary<float, string> xCategories = new Dictionary<float, string>();
        Dictionary<float, string> yCategories = new Dictionary<float, string>();
        Dictionary<float, string> zCategories = new Dictionary<float, string>();
        Dictionary<float, string> sCategories = new Dictionary<float, string>();
        Dictionary<string, float> textColorMap = new Dictionary<string, float>();

        float dataMinX = float.MaxValue, dataMaxX = float.MinValue;
        float dataMinY = float.MaxValue, dataMaxY = float.MinValue;
        float dataMinZ = float.MaxValue, dataMaxZ = float.MinValue;
        float dataMinS = float.MaxValue, dataMaxS = float.MinValue;

        int pointLimit = 1000;
        int dataStep = Mathf.Max(1, (lines.Length - 1) / pointLimit);

        HashSet<string> xSet = new HashSet<string>();
        HashSet<string> ySet = new HashSet<string>();
        HashSet<string> zSet = new HashSet<string>();
        HashSet<string> cSet = new HashSet<string>();
        HashSet<string> sSet = new HashSet<string>();

        bool xIsStr = false, yIsStr = false, zIsStr = false, cIsStr = false, sIsStr = false;
        bool xIsDate = false, yIsDate = false, zIsDate = false;

        List<string[]> sampledRows = new List<string[]>();
        Dictionary<string, int> bubbleFrequencies = new Dictionary<string, int>();

        for (int i = 1; i < lines.Length; i += dataStep)
        {
            string[] cols = ParseCSVLine(lines[i]);
            if (cols.Length <= Mathf.Max(xIdx, Mathf.Max(yIdx, zIdx))) continue;
            sampledRows.Add(cols);

            string valX = cols[xIdx].Trim();
            if (!string.IsNullOrEmpty(valX) && !float.TryParse(valX, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            { xIsStr = true; xSet.Add(valX); if (System.DateTime.TryParse(valX, out _)) xIsDate = true; }

            string valY = cols[yIdx].Trim();
            if (!string.IsNullOrEmpty(valY) && !float.TryParse(valY, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            { yIsStr = true; ySet.Add(valY); if (System.DateTime.TryParse(valY, out _)) yIsDate = true; }

            string valZ = cols[zIdx].Trim();
            if (!string.IsNullOrEmpty(valZ) && !float.TryParse(valZ, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            { zIsStr = true; zSet.Add(valZ); if (System.DateTime.TryParse(valZ, out _)) zIsDate = true; }

            if (cIdx != -1 && cols.Length > cIdx)
            {
                string valC = cols[cIdx].Trim();
                if (!string.IsNullOrEmpty(valC) && !float.TryParse(valC, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                { cIsStr = true; cSet.Add(valC); }
            }
            if (sIdx != -1 && cols.Length > sIdx)
            {
                string valS = cols[sIdx].Trim();
                if (!string.IsNullOrEmpty(valS) && !float.TryParse(valS, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                { sIsStr = true; sSet.Add(valS); }
            }

            if (chart.chart_type.IndexOf("Bubble", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string posKey = $"{cols[xIdx]}_{cols[yIdx]}_{cols[zIdx]}";
                if (!bubbleFrequencies.ContainsKey(posKey)) bubbleFrequencies[posKey] = 0;
                bubbleFrequencies[posKey]++;
            }
        }

        void PopulateMap(HashSet<string> set, Dictionary<float, string> map, bool isDate = false)
        {
            List<string> list = set.ToList();
            if (isDate)
                list.Sort((a, b) => {
                    System.DateTime.TryParse(a, out System.DateTime da);
                    System.DateTime.TryParse(b, out System.DateTime db);
                    return da.CompareTo(db);
                });
            else
                list.Sort(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++) map[(float)i] = list[i];
        }

        if (xIsStr) PopulateMap(xSet, xCategories, xIsDate);
        if (yIsStr) PopulateMap(ySet, yCategories, yIsDate);
        if (zIsStr) PopulateMap(zSet, zCategories, zIsDate);
        if (sIsStr) PopulateMap(sSet, sCategories);

        if (cIsStr)
        {
            List<string> cList = cSet.ToList();
            cList.Sort(StringComparer.Ordinal);
            for (int i = 0; i < cList.Count; i++) textColorMap[cList[i]] = (float)i;
        }

        float maxFrequency = 1f;
        if (bubbleFrequencies.Count > 0) maxFrequency = bubbleFrequencies.Values.Max();

        HashSet<string> drawnBubbles = new HashSet<string>();
        bool isLine = chart.chart_type.IndexOf("Line", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isBubbleMode = chart.chart_type.IndexOf("Bubble", StringComparison.OrdinalIgnoreCase) >= 0;

        Dictionary<float, List<PointCoords>> lineGroups = new Dictionary<float, List<PointCoords>>();

        foreach (var cols in sampledRows)
        {
            float xVal = ParseAxisValue(cols[xIdx], xCategories);
            float yVal = ParseAxisValue(cols[yIdx], yCategories);
            float zVal = ParseAxisValue(cols[zIdx], zCategories);

            string posKey = $"{cols[xIdx]}_{cols[yIdx]}_{cols[zIdx]}";
            if (isBubbleMode) { if (drawnBubbles.Contains(posKey)) continue; drawnBubbles.Add(posKey); }

            float cVal = 0f;
            if (cIdx != -1 && cols.Length > cIdx)
            {
                string rawColorStr = cols[cIdx];
                if (float.TryParse(rawColorStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsedC)) cVal = parsedC;
                else if (!string.IsNullOrEmpty(rawColorStr) && textColorMap.ContainsKey(rawColorStr))
                    cVal = textColorMap[rawColorStr];
            }

            float sVal = 0f;
            if (sIdx != -1 && cols.Length > sIdx) sVal = ParseAxisValue(cols[sIdx], sCategories);
            else if (isBubbleMode) sVal = bubbleFrequencies[posKey];

            if (sVal < dataMinS) dataMinS = sVal;
            if (sVal > dataMaxS) dataMaxS = sVal;

            PointCoords newPoint = new PointCoords { x = xVal, y = yVal, z = zVal, c = cVal, s = sVal };
            rawPoints.Add(newPoint);
            rawRows.Add(cols);

            if (xVal < dataMinX) dataMinX = xVal; if (xVal > dataMaxX) dataMaxX = xVal;
            if (yVal < dataMinY) dataMinY = yVal; if (yVal > dataMaxY) dataMaxY = yVal;
            if (zVal < dataMinZ) dataMinZ = zVal; if (zVal > dataMaxZ) dataMaxZ = zVal;

            if (isLine)
            {
                if (!lineGroups.ContainsKey(cVal)) lineGroups[cVal] = new List<PointCoords>();
                lineGroups[cVal].Add(newPoint);
            }
        }

        if (rawPoints.Count == 0) return;

        float minC = float.MaxValue, maxC = float.MinValue;
        if (cIdx != -1 && rawPoints.Count > 0)
        {
            foreach (PointCoords p in rawPoints)
            {
                if (p.c < minC) minC = p.c;
                if (p.c > maxC) maxC = p.c;
            }
            if (minC == maxC) { maxC = minC + 1f; }
        }

        float boxMinX, boxMaxX, boxMinY, boxMaxY, boxMinZ, boxMaxZ;

        float[] xTicks = (chart.scales != null && chart.scales.x != null && chart.scales.x.Length >= 2) ? chart.scales.x : null;
        float[] yTicks = (chart.scales != null && chart.scales.y != null && chart.scales.y.Length >= 2) ? chart.scales.y : null;
        float[] zTicks = (chart.scales != null && chart.scales.z != null && chart.scales.z.Length >= 2) ? chart.scales.z : null;
        float[] cTicks = (chart.scales != null && chart.scales.c != null && chart.scales.c.Length >= 2) ? chart.scales.c : null;

        if (xCategories.Count > 0)
        { float padX = Mathf.Max(1f, xCategories.Count * 0.05f); boxMinX = -padX; boxMaxX = (xCategories.Count - 1) + padX; }
        else
        {
            float min = xTicks != null ? Mathf.Min(dataMinX, xTicks[0]) : dataMinX;
            float max = xTicks != null ? Mathf.Max(dataMaxX, xTicks[xTicks.Length - 1]) : dataMaxX;
            float pad = (max - min) * 0.05f; if (pad == 0) pad = 1f;
            boxMinX = min - pad; boxMaxX = max + pad;
        }

        if (yCategories.Count > 0)
        { float padY = Mathf.Max(1f, yCategories.Count * 0.05f); boxMinY = -padY; boxMaxY = (yCategories.Count - 1) + padY; }
        else
        {
            float min = yTicks != null ? Mathf.Min(dataMinY, yTicks[0]) : dataMinY;
            float max = yTicks != null ? Mathf.Max(dataMaxY, yTicks[yTicks.Length - 1]) : dataMaxY;
            float pad = (max - min) * 0.05f; if (pad == 0) pad = 1f;
            boxMinY = min - pad; boxMaxY = max + pad;
        }

        if (zCategories.Count > 0)
        { float padZ = Mathf.Max(1f, zCategories.Count * 0.05f); boxMinZ = -padZ; boxMaxZ = (zCategories.Count - 1) + padZ; }
        else
        {
            float min = zTicks != null ? Mathf.Min(dataMinZ, zTicks[0]) : dataMinZ;
            float max = zTicks != null ? Mathf.Max(dataMaxZ, zTicks[zTicks.Length - 1]) : dataMaxZ;
            min = Mathf.Min(0f, min);
            float range = max - min; if (range == 0) range = 1f;

            boxMinZ = min - (range * 0.10f);
            boxMaxZ = max + (range * 0.05f);
        }

        Color red = Color.red; Color green = Color.green; Color blue = new Color(0.2f, 0.5f, 1f);
        CreateAxis(currentChartRotator.transform, new Vector3(0, 0.02f, -0.5f), new Vector3(1f, 0.01f, 0.01f), red);
        CreateText(currentChartRotator.transform,
            "X: " + (chart.axes.x.Length > 12 ? chart.axes.x.Substring(0, 12) + ".." : chart.axes.x),
            new Vector3(0.55f, 0.02f, -0.65f), red, 0.75f);
        CreateAxis(currentChartRotator.transform, new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.01f, 1f, 0.01f), green);
        CreateText(currentChartRotator.transform,
            "Z: " + (chart.axes.z.Length > 12 ? chart.axes.z.Substring(0, 12) + ".." : chart.axes.z),
            new Vector3(-0.65f, 1.08f, -0.5f), green, 0.75f);
        CreateAxis(currentChartRotator.transform, new Vector3(-0.5f, 0.02f, 0), new Vector3(0.01f, 0.01f, 1f), blue);
        CreateText(currentChartRotator.transform,
            "Y: " + (chart.axes.y.Length > 12 ? chart.axes.y.Substring(0, 12) + ".." : chart.axes.y),
            new Vector3(-0.65f, 0.02f, 0.6f), blue, 0.75f);

        Color gridColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        float tickFontSize = 0.45f;

        // X ekseni tick'leri
        if (xCategories.Count > 0)
        {
            int maxTicksToShow = 5;
            int tickStep = Mathf.Max(1, xCategories.Count / maxTicksToShow);
            for (int i = 0; i < xCategories.Count; i += tickStep)
            {
                float t = Mathf.InverseLerp(boxMinX, boxMaxX, i);
                float posX = Mathf.Lerp(-0.5f, 0.5f, t);
                string lblX = xCategories[i];
                if (System.DateTime.TryParse(lblX, out System.DateTime dtX)) lblX = dtX.ToString("yyyy-MM");
                else if (lblX.Length > 10) lblX = lblX.Substring(0, 10) + ".";
                CreateText(currentChartRotator.transform, lblX, new Vector3(posX, 0.02f, -0.6f), Color.white, tickFontSize);
                CreateAxis(currentChartRotator.transform, new Vector3(posX, 0.02f, 0), new Vector3(0.002f, 0.002f, 1f), gridColor);
            }
        }
        else
        {
            if (xTicks != null && xTicks.Length > 0)
            {
                int maxTicksToShow = 5;
                int tickStep = Mathf.Max(1, xTicks.Length / maxTicksToShow);
                for (int tickIdx = 0; tickIdx < xTicks.Length; tickIdx += tickStep)
                {
                    float val = xTicks[tickIdx];
                    float t = Mathf.InverseLerp(boxMinX, boxMaxX, val);
                    float posX = Mathf.Lerp(-0.5f, 0.5f, t);
                    CreateText(currentChartRotator.transform, FormatAxisLabel(val, xCategories), new Vector3(posX, 0.02f, -0.6f), Color.white, tickFontSize);
                    CreateAxis(currentChartRotator.transform, new Vector3(posX, 0.02f, 0), new Vector3(0.002f, 0.002f, 1f), gridColor);
                }
            }
            else
            {
                CalculateNiceScale(dataMinX, dataMaxX, out float niceMinX, out float niceMaxX, out float niceStepX);
                for (float val = niceMinX; val <= niceMaxX + 0.001f; val += niceStepX)
                {
                    float t = Mathf.InverseLerp(boxMinX, boxMaxX, val);
                    float posX = Mathf.Lerp(-0.5f, 0.5f, t);
                    CreateText(currentChartRotator.transform, FormatAxisLabel(val, xCategories), new Vector3(posX, 0.02f, -0.6f), Color.white, tickFontSize);
                    CreateAxis(currentChartRotator.transform, new Vector3(posX, 0.02f, 0), new Vector3(0.002f, 0.002f, 1f), gridColor);
                }
            }
        }

        // Y ekseni tick'leri
        if (yCategories.Count > 0)
        {
            int maxTicksToShow = 5;
            int tickStep = Mathf.Max(1, yCategories.Count / maxTicksToShow);
            for (int i = 0; i < yCategories.Count; i += tickStep)
            {
                float t = Mathf.InverseLerp(boxMinY, boxMaxY, i);
                float posZ = Mathf.Lerp(-0.5f, 0.5f, t);
                string lblY = yCategories[i];
                if (System.DateTime.TryParse(lblY, out System.DateTime dtY)) lblY = dtY.ToString("yyyy-MM");
                else if (lblY.Length > 10) lblY = lblY.Substring(0, 10) + ".";
                CreateText(currentChartRotator.transform, lblY, new Vector3(-0.6f, 0.02f, posZ), Color.white, tickFontSize);
                CreateAxis(currentChartRotator.transform, new Vector3(0, 0.02f, posZ), new Vector3(1f, 0.002f, 0.002f), gridColor);
            }
        }
        else
        {
            if (yTicks != null && yTicks.Length > 0)
            {
                int maxTicksToShow = 5;
                int tickStep = Mathf.Max(1, yTicks.Length / maxTicksToShow);
                for (int tickIdx = 0; tickIdx < yTicks.Length; tickIdx += tickStep)
                {
                    float val = yTicks[tickIdx];
                    float t = Mathf.InverseLerp(boxMinY, boxMaxY, val);
                    float posZ = Mathf.Lerp(-0.5f, 0.5f, t);
                    CreateText(currentChartRotator.transform, FormatAxisLabel(val, yCategories), new Vector3(-0.6f, 0.02f, posZ), Color.white, tickFontSize);
                    CreateAxis(currentChartRotator.transform, new Vector3(0, 0.02f, posZ), new Vector3(1f, 0.002f, 0.002f), gridColor);
                }
            }
            else
            {
                CalculateNiceScale(dataMinY, dataMaxY, out float niceMinY, out float niceMaxY, out float niceStepY);
                for (float val = niceMinY; val <= niceMaxY + 0.001f; val += niceStepY)
                {
                    float t = Mathf.InverseLerp(boxMinY, boxMaxY, val);
                    float posZ = Mathf.Lerp(-0.5f, 0.5f, t);
                    CreateText(currentChartRotator.transform, FormatAxisLabel(val, yCategories), new Vector3(-0.6f, 0.02f, posZ), Color.white, tickFontSize);
                    CreateAxis(currentChartRotator.transform, new Vector3(0, 0.02f, posZ), new Vector3(1f, 0.002f, 0.002f), gridColor);
                }
            }
        }

        // Z ekseni tick'leri
        if (zCategories.Count > 0)
        {
            int maxTicksToShow = 5;
            int tickStep = Mathf.Max(1, zCategories.Count / maxTicksToShow);
            for (int i = 0; i < zCategories.Count; i += tickStep)
            {
                float t = Mathf.InverseLerp(boxMinZ, boxMaxZ, i);
                float posY = Mathf.Lerp(0.02f, 1.0f, t);
                string lblZ = zCategories[i];
                if (System.DateTime.TryParse(lblZ, out System.DateTime dtZ)) lblZ = dtZ.ToString("yyyy-MM");
                else if (lblZ.Length > 10) lblZ = lblZ.Substring(0, 10) + ".";
                CreateText(currentChartRotator.transform, lblZ, new Vector3(-0.6f, posY, -0.5f), Color.white, tickFontSize);
                CreateAxis(currentChartRotator.transform, new Vector3(0, posY, -0.5f), new Vector3(1f, 0.002f, 0.002f), gridColor);
                CreateAxis(currentChartRotator.transform, new Vector3(-0.5f, posY, 0), new Vector3(0.002f, 0.002f, 1f), gridColor);
            }
        }
        else
        {
            if (zTicks != null && zTicks.Length > 0)
            {
                int maxTicksToShow = 5;
                int tickStep = Mathf.Max(1, zTicks.Length / maxTicksToShow);
                for (int tickIdx = 0; tickIdx < zTicks.Length; tickIdx += tickStep)
                {
                    float val = zTicks[tickIdx];
                    float t = Mathf.InverseLerp(boxMinZ, boxMaxZ, val);
                    float posY = Mathf.Lerp(0.02f, 1.0f, t);
                    CreateText(currentChartRotator.transform, FormatAxisLabel(val, zCategories), new Vector3(-0.6f, posY, -0.5f), Color.white, tickFontSize);
                    CreateAxis(currentChartRotator.transform, new Vector3(0, posY, -0.5f), new Vector3(1f, 0.002f, 0.002f), gridColor);
                    CreateAxis(currentChartRotator.transform, new Vector3(-0.5f, posY, 0), new Vector3(0.002f, 0.002f, 1f), gridColor);
                }
            }
            else
            {
                CalculateNiceScale(dataMinZ, dataMaxZ, out float niceMinZ, out float niceMaxZ, out float niceStepZ);
                for (float val = niceMinZ; val <= niceMaxZ + 0.001f; val += niceStepZ)
                {
                    float t = Mathf.InverseLerp(boxMinZ, boxMaxZ, val);
                    float posY = Mathf.Lerp(0.02f, 1.0f, t);
                    CreateText(currentChartRotator.transform, FormatAxisLabel(val, zCategories), new Vector3(-0.6f, posY, -0.5f), Color.white, tickFontSize);
                    CreateAxis(currentChartRotator.transform, new Vector3(0, posY, -0.5f), new Vector3(1f, 0.002f, 0.002f), gridColor);
                    CreateAxis(currentChartRotator.transform, new Vector3(-0.5f, posY, 0), new Vector3(0.002f, 0.002f, 1f), gridColor);
                }
            }
        }

        bool isBar = chart.chart_type.IndexOf("Bar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     chart.chart_type.IndexOf("Column", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isVoxel = chart.chart_type.IndexOf("Voxel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       chart.chart_type.IndexOf("Density", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isNetwork = chart.chart_type.IndexOf("Network", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         chart.chart_type.IndexOf("Graph", StringComparison.OrdinalIgnoreCase) >= 0;

        if (isBar)
        {
            Dictionary<string, int> groupedBars = new Dictionary<string, int>();
            for (int i = 0; i < rawPoints.Count; i++)
            {
                string key = rawPoints[i].x + "_" + rawPoints[i].y;
                if (!groupedBars.ContainsKey(key)) groupedBars[key] = i;
                else if (rawPoints[i].z > rawPoints[groupedBars[key]].z) groupedBars[key] = i;
            }
            List<PointCoords> uniquePoints = new List<PointCoords>();
            List<string[]> uniqueRows = new List<string[]>();
            foreach (int index in groupedBars.Values) { uniquePoints.Add(rawPoints[index]); uniqueRows.Add(rawRows[index]); }
            rawPoints = uniquePoints;
            rawRows = uniqueRows;
        }

        Dictionary<Vector3, int> voxelDensities = new Dictionary<Vector3, int>();
        int maxDensity = 0;
        float voxelSize = 0.1f;

        List<Vector3> networkPositions = new List<Vector3>();
        List<Color> networkColors = new List<Color>();
        List<float> networkGroups = new List<float>();

        for (int i = 0; i < rawPoints.Count; i++)
        {
            float normX = Mathf.Lerp(-0.5f, 0.5f, Mathf.InverseLerp(boxMinX, boxMaxX, rawPoints[i].x));
            float normZ = Mathf.Lerp(-0.5f, 0.5f, Mathf.InverseLerp(boxMinY, boxMaxY, rawPoints[i].y));
            float normY = Mathf.Lerp(0.02f, 1.0f, Mathf.InverseLerp(boxMinZ, boxMaxZ, rawPoints[i].z));

            if (isVoxel)
            {
                // Küpleri çizgilerin üstüne değil, hücrelerin İÇİNE oturtmak için Floor ve Offset ekledik.
                float gridX = Mathf.Floor(normX / voxelSize) * voxelSize + (voxelSize / 2f);
                float gridY = Mathf.Floor(normY / voxelSize) * voxelSize + (voxelSize / 2f);
                float gridZ = Mathf.Floor(normZ / voxelSize) * voxelSize + (voxelSize / 2f);
                Vector3 gridPos = new Vector3(gridX, gridY, gridZ);
                if (!voxelDensities.ContainsKey(gridPos)) voxelDensities[gridPos] = 0;
                voxelDensities[gridPos]++;
                if (voxelDensities[gridPos] > maxDensity) maxDensity = voxelDensities[gridPos];
            }
        }

        float vMin = 1f;
        float vMax = maxDensity > 0 ? maxDensity : 1f;
        if (isVoxel && cTicks != null && cTicks.Length > 0) { vMin = cTicks[0]; vMax = cTicks[cTicks.Length - 1]; }

        HashSet<Vector3> drawnVoxels = new HashSet<Vector3>();

        if (isLine)
        {
            foreach (var group in lineGroups)
            {
                Dictionary<float, PointCoords> averagedPoints = new Dictionary<float, PointCoords>();
                Dictionary<float, int> countPerX = new Dictionary<float, int>();

                foreach (PointCoords p in group.Value)
                {
                    if (!averagedPoints.ContainsKey(p.x))
                    { averagedPoints[p.x] = new PointCoords { x = p.x, y = p.y, z = p.z, c = p.c, s = p.s }; countPerX[p.x] = 1; }
                    else
                    {
                        PointCoords temp = averagedPoints[p.x];
                        temp.y += p.y; temp.z += p.z;
                        averagedPoints[p.x] = temp; countPerX[p.x]++;
                    }
                }

                List<PointCoords> sortedGroup = new List<PointCoords>();
                foreach (var kvp in averagedPoints)
                {
                    PointCoords avgP = kvp.Value;
                    int count = countPerX[kvp.Key];
                    avgP.y /= count; avgP.z /= count;
                    sortedGroup.Add(avgP);
                }
                sortedGroup = sortedGroup.OrderBy(p => p.x).ToList();
                if (sortedGroup.Count < 2) continue;

                Color lineColor = new Color(0.45f, 0.15f, 0.6f);
                if (cIdx != -1)
                {
                    float colorT = (maxC > minC) ? Mathf.InverseLerp(minC, maxC, group.Key) : 0f;
                    lineColor = GetPlasmaColor(colorT);
                }

                GameObject lineObj = new GameObject("3D_Line");
                lineObj.transform.SetParent(currentChartRotator.transform);
                lineObj.transform.localPosition = Vector3.zero;
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.material = NewSpritesMat(lineColor);
                lr.startWidth = 0.018f; lr.endWidth = 0.018f;
                lr.startColor = lineColor; lr.endColor = lineColor;
                lr.useWorldSpace = false;
                lr.positionCount = sortedGroup.Count;

                GameObject ribbonObj = new GameObject("3D_Ribbon");
                ribbonObj.transform.SetParent(currentChartRotator.transform);
                ribbonObj.transform.localPosition = Vector3.zero;
                MeshFilter mf = ribbonObj.AddComponent<MeshFilter>();
                MeshRenderer mr = ribbonObj.AddComponent<MeshRenderer>();
                Color ribbonColor = lineColor; ribbonColor.a = 0.30f;
                mr.material = NewSpritesMat(ribbonColor);

                Mesh mesh = new Mesh();
                Vector3[] vertices = new Vector3[sortedGroup.Count * 2];
                int[] triangles = new int[(sortedGroup.Count - 1) * 6];

                for (int i = 0; i < sortedGroup.Count; i++)
                {
                    PointCoords p = sortedGroup[i];
                    float normX = Mathf.Lerp(-0.5f, 0.5f, Mathf.InverseLerp(boxMinX, boxMaxX, p.x));
                    float normZ = Mathf.Lerp(-0.5f, 0.5f, Mathf.InverseLerp(boxMinY, boxMaxY, p.y));
                    float normY = Mathf.Lerp(0.02f, 1.0f, Mathf.InverseLerp(boxMinZ, boxMaxZ, p.z));

                    Vector3 topPos = new Vector3(normX, normY, normZ);
                    Vector3 bottomPos = new Vector3(normX, 0.02f, normZ);
                    lr.SetPosition(i, topPos);
                    vertices[i * 2] = topPos;
                    vertices[i * 2 + 1] = bottomPos;

                    if (i < sortedGroup.Count - 1)
                    {
                        int tIdx = i * 6; int vIdx = i * 2;
                        triangles[tIdx] = vIdx; triangles[tIdx + 1] = vIdx + 1; triangles[tIdx + 2] = vIdx + 2;
                        triangles[tIdx + 3] = vIdx + 2; triangles[tIdx + 4] = vIdx + 1; triangles[tIdx + 5] = vIdx + 3;
                    }

                    GameObject clickNode = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    clickNode.transform.SetParent(currentChartRotator.transform);
                    clickNode.transform.localPosition = topPos;
                    clickNode.transform.localScale = Vector3.one * 0.04f;
                    clickNode.GetComponent<Renderer>().enabled = false;
                    PointDataInfo pData = clickNode.AddComponent<PointDataInfo>();
                    pData.info = $"<color=#FF5555>{chart.axes.x}:</color> {FormatAxisLabel(p.x, xCategories)}\n" +
                        $"<color=#55FF55>{chart.axes.y}:</color> {FormatAxisLabel(p.y, yCategories)}\n" +
                        $"<color=#5555FF>{chart.axes.z}:</color> {p.z:F2}";
                    if (cIdx != -1 && cIsStr && textColorMap.Count > 0)
                    {

                        string colorLabel = "";
                        foreach (var kvp in textColorMap)
                            if (Mathf.Approximately(kvp.Value, group.Key)) { colorLabel = kvp.Key; break; }
                        if (!string.IsNullOrEmpty(colorLabel))
                            pData.info += $"\n<color=#FFFF55>Renk ({chart.axes.color}):</color> {colorLabel}";
                    }
                    else if (cIdx != -1 && !cIsStr)
                    {
                        pData.info += $"\n<color=#FFFF55>Renk ({chart.axes.color}):</color> {group.Key:F2}";
                    }
                }

                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mf.mesh = mesh;
            }
        }
        else
        {
            for (int i = 0; i < rawPoints.Count; i++)
            {
                PointCoords p = rawPoints[i];
                string[] row = rawRows[i];

                float normX = Mathf.Lerp(-0.5f, 0.5f, Mathf.InverseLerp(boxMinX, boxMaxX, p.x));
                float normZ = Mathf.Lerp(-0.5f, 0.5f, Mathf.InverseLerp(boxMinY, boxMaxY, p.y));
                float normY = Mathf.Lerp(0.02f, 1.0f, Mathf.InverseLerp(boxMinZ, boxMaxZ, p.z));

                float jitterX = 0f, jitterZ = 0f;
                if (!isBar && !isVoxel && !isBubbleMode)
                { jitterX = UnityEngine.Random.Range(-0.005f, 0.005f); jitterZ = UnityEngine.Random.Range(-0.005f, 0.005f); }
                Vector3 pos = new Vector3(normX + jitterX, normY, normZ + jitterZ);

                GameObject dataPoint = null;

                if (isVoxel)
                {
                    // Küpleri çizgilerin üstüne değil, hücrelerin İÇİNE oturtmak için Floor ve Offset ekledik.
                    float gridX = Mathf.Floor(normX / voxelSize) * voxelSize + (voxelSize / 2f);
                    float gridY = Mathf.Floor(normY / voxelSize) * voxelSize + (voxelSize / 2f);
                    float gridZ = Mathf.Floor(normZ / voxelSize) * voxelSize + (voxelSize / 2f);
                    Vector3 gridPos = new Vector3(gridX, gridY, gridZ);
                    if (drawnVoxels.Contains(gridPos)) continue;
                    drawnVoxels.Add(gridPos);

                    dataPoint = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    dataPoint.transform.SetParent(currentChartRotator.transform);
                    dataPoint.transform.localScale = Vector3.one * (voxelSize * 0.9f);
                    dataPoint.transform.localPosition = gridPos;

                    int density = voxelDensities[gridPos];
                    float densityRatio = Mathf.Clamp01((float)density / vMax);
                    Color vColor = GetPlasmaColor(densityRatio);
                    vColor.a = 0.8f;
                    dataPoint.GetComponent<Renderer>().material = NewTransparentMat(vColor);

                    PointDataInfo pData = dataPoint.AddComponent<PointDataInfo>();
                    pData.info = $"<color=#FF5555>{chart.axes.x}:</color> {row[xIdx].Trim()}\n" +
                                 $"<color=#55FF55>{chart.axes.y}:</color> {row[yIdx].Trim()}\n" +
                                 $"<color=#5555FF>{chart.axes.z}:</color> {row[zIdx].Trim()}\n" +
                                 $"<color=#FFFF55>Frekans:</color> {density}";
                }
                else if (isBar)
                {
                    dataPoint = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    dataPoint.transform.SetParent(currentChartRotator.transform);
                    float floorY = 0.02f;
                    float barTop = normY;
                    float barHeight = Mathf.Max(0.005f, barTop - floorY);
                    dataPoint.transform.localScale = new Vector3(0.08f, barHeight, 0.08f);
                    dataPoint.transform.localPosition = new Vector3(normX, floorY + (barHeight / 2f), normZ);
                }
                else
                {
                    dataPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    dataPoint.transform.SetParent(currentChartRotator.transform);

                    float size = 0.035f;
                    if (isBubbleMode)
                    {
                        if (sIdx != -1 && dataMaxS > dataMinS)
                        { float scaleFactor = Mathf.InverseLerp(dataMinS, dataMaxS, p.s); size = Mathf.Lerp(0.02f, 0.15f, scaleFactor); }
                        else
                        { float scaleFactor = Mathf.InverseLerp(1, maxFrequency, p.s); size = Mathf.Lerp(0.02f, 0.1f, scaleFactor); }
                    }
                    else if (sIdx != -1 && dataMaxS > dataMinS)
                    { float scaleFactor = Mathf.InverseLerp(dataMinS, dataMaxS, p.s); size = Mathf.Lerp(0.01f, 0.08f, scaleFactor); }

                    dataPoint.transform.localScale = Vector3.one * size;
                    dataPoint.transform.localPosition = pos;

                    // Mobilde küçük sphere'lar zor tıklanıyor — collider'ı büyüt
                    SphereCollider sc = dataPoint.GetComponent<SphereCollider>();
                    if (sc != null) sc.radius = SystemInfo.deviceType == DeviceType.Handheld ? 2.0f : 0.6f;
                }

                if (!isVoxel && dataPoint != null)
                {
                    Color ptColor;
                    if (cIdx != -1)
                    { float colorT = (maxC > minC) ? Mathf.InverseLerp(minC, maxC, p.c) : 0f; ptColor = GetPlasmaColor(colorT); }
                    else
                    { ptColor = new Color(0.45f, 0.15f, 0.6f); }

                    if (isBar)
                    {
                        ptColor.a = 1.0f;
                        dataPoint.GetComponent<Renderer>().material = NewOpaqueMat(ptColor);
                    }
                    else
                    {
                        ptColor.a = isBubbleMode ? 0.6f : 0.8f;
                        dataPoint.GetComponent<Renderer>().material = NewTransparentMat(ptColor);
                    }

                    if (isNetwork)
                    {
                        networkPositions.Add(pos);
                        networkColors.Add(ptColor);
                        networkGroups.Add(p.c);
                    }

                    PointDataInfo pData = dataPoint.AddComponent<PointDataInfo>();
                    pData.info = $"<color=#FF5555>{chart.axes.x}:</color> {row[xIdx].Trim()}\n" +
                        $"<color=#55FF55>{chart.axes.y}:</color> {row[yIdx].Trim()}\n" +
                        $"<color=#5555FF>{chart.axes.z}:</color> {row[zIdx].Trim()}";

                    if (cIdx != -1 && row.Length > cIdx)
                        pData.info += $"\n<color=#FFFF55>{chart.axes.color}:</color> {row[cIdx].Trim()}";

                    if (sIdx != -1 && row.Length > sIdx)
                        pData.info += $"\n<color=#FFAA00>{chart.axes.size}:</color> {row[sIdx].Trim()}";
                    else if (isBubbleMode)
                        pData.info += $"\n<color=#FFAA00>Yoğunluk:</color> {p.s} Adet";
                }
            }
        }

        if (isNetwork && networkPositions.Count > 0)
        {
            DrawWireframeBox(currentChartRotator.transform, new Vector3(0, 0.51f, 0), new Vector3(1f, 0.98f, 1f), new Color(1f, 1f, 1f, 0.2f));

            Dictionary<float, Vector3> centroids = new Dictionary<float, Vector3>();
            Dictionary<float, int> counts = new Dictionary<float, int>();
            Dictionary<float, Color> cColors = new Dictionary<float, Color>();

            for (int i = 0; i < networkPositions.Count; i++)
            {
                float g = networkGroups[i];
                if (!centroids.ContainsKey(g)) { centroids[g] = Vector3.zero; counts[g] = 0; cColors[g] = networkColors[i]; }
                centroids[g] += networkPositions[i]; counts[g]++;
            }

            List<float> keys = new List<float>(centroids.Keys);
            foreach (float g in keys)
            {
                centroids[g] /= counts[g];
                GameObject hubObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hubObj.transform.SetParent(currentChartRotator.transform);
                hubObj.transform.localPosition = centroids[g];
                hubObj.transform.localScale = Vector3.one * 0.08f;
                hubObj.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
                hubObj.GetComponent<Renderer>().material = NewOpaqueMat(cColors[g]);
            }

            for (int i = 0; i < networkPositions.Count; i++)
            {
                Vector3 pNode = networkPositions[i];
                Vector3 pHub = centroids[networkGroups[i]];
                Color lineColor = networkColors[i]; lineColor.a = 0.35f;

                GameObject lineObj = new GameObject("NetworkSpoke");
                lineObj.transform.SetParent(currentChartRotator.transform);
                lineObj.transform.localPosition = Vector3.zero;
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.material = NewSpritesMat(lineColor);
                lr.startWidth = 0.006f; lr.endWidth = 0.001f;
                lr.startColor = lineColor; lr.endColor = lineColor;
                lr.useWorldSpace = false; lr.positionCount = 2;
                lr.SetPosition(0, pHub); lr.SetPosition(1, pNode);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                for (int j = i + 1; j < keys.Count; j++)
                {
                    Color bbColor = new Color(0.6f, 0.6f, 0.6f, 0.3f);
                    GameObject lineObj = new GameObject("NetworkBackbone");
                    lineObj.transform.SetParent(currentChartRotator.transform);
                    lineObj.transform.localPosition = Vector3.zero;
                    LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                    lr.material = NewSpritesMat(bbColor);
                    lr.startWidth = 0.004f; lr.endWidth = 0.004f;
                    lr.startColor = bbColor; lr.endColor = bbColor;
                    lr.useWorldSpace = false; lr.positionCount = 2;
                    lr.SetPosition(0, centroids[keys[i]]);
                    lr.SetPosition(1, centroids[keys[j]]);
                }
            }
        }

        if (isVoxel)
            Create3DLegend(currentChartRoot.transform, "Frequency", vMin, vMax, new Dictionary<string, float>(), cTicks);
        else if (cIdx != -1)
            Create3DLegend(currentChartRoot.transform, chart.axes.color, minC, maxC, textColorMap, cTicks);
    }

    private void DrawWireframeBox(Transform parent, Vector3 center, Vector3 size, Color color)
    {
        Vector3[] corners = new Vector3[8];
        float hw = size.x / 2f; float hh = size.y / 2f; float hd = size.z / 2f;
        corners[0] = center + new Vector3(-hw, -hh, -hd); corners[1] = center + new Vector3(hw, -hh, -hd);
        corners[2] = center + new Vector3(hw, -hh, hd); corners[3] = center + new Vector3(-hw, -hh, hd);
        corners[4] = center + new Vector3(-hw, hh, -hd); corners[5] = center + new Vector3(hw, hh, -hd);
        corners[6] = center + new Vector3(hw, hh, hd); corners[7] = center + new Vector3(-hw, hh, hd);

        int[,] edges = { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 }, { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };

        for (int i = 0; i < edges.GetLength(0); i++)
        {
            GameObject lineObj = new GameObject("WireframeEdge");
            lineObj.transform.SetParent(parent);
            lineObj.transform.localPosition = Vector3.zero;
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = NewSpritesMat(color);
            lr.startWidth = 0.001f; lr.endWidth = 0.001f;
            lr.startColor = color; lr.endColor = color;
            lr.useWorldSpace = false; lr.positionCount = 2;
            lr.SetPosition(0, corners[edges[i, 0]]);
            lr.SetPosition(1, corners[edges[i, 1]]);
        }
    }

    private void CreateAxis(Transform parent, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject axis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        axis.transform.SetParent(parent);
        axis.transform.localPosition = pos;
        axis.transform.localScale = scale;
        axis.GetComponent<Renderer>().material = NewOpaqueMat(color);
        Destroy(axis.GetComponent<BoxCollider>());
    }

    private void CreateText(Transform parent, string text, Vector3 pos, Color color, float size)
    {
        GameObject txtObj = new GameObject("AxisText");
        txtObj.transform.SetParent(parent);
        txtObj.transform.localPosition = pos;
        TextMeshPro tmpro = txtObj.AddComponent<TextMeshPro>();
        tmpro.text = text; tmpro.fontSize = size; tmpro.color = color;
        tmpro.alignment = TextAlignmentOptions.Center;
        billboardItems.Add(txtObj.transform);
    }

    // ============================================================
    // UI LEGEND — Screen Space Canvas, ekranın altında sabit
    // Grafik 3D alanına hiç dokunmaz, her çözünürlükte çalışır.
    // ============================================================
    private void Create3DLegend(Transform parent, string title, float minC, float maxC,
        Dictionary<string, float> textColorMap, float[] cTicks)
    {
        CreateUILegend(title, minC, maxC, textColorMap, cTicks);
    }

    private void CreateUILegend(string title, float minC, float maxC,
        Dictionary<string, float> textColorMap, float[] cTicks)
    {
        // arUI zaten ScreenSpaceOverlay Canvas — legend panelini oraya ekliyoruz
        if (arUI == null) return;

        // ── Panel (ekranın alt ortası) ──────────────────────────────
        GameObject legendPanel = new GameObject("UI_Legend");
        legendPanel.transform.SetParent(arUI.transform, false);

        RectTransform panelRect = legendPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 20f);   // tabandan 20px yukarı
        panelRect.sizeDelta = new Vector2(700f, 110f);

        Image panelBg = legendPanel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.55f);       // yarı saydam siyah arka plan

        // ── Başlık metni ────────────────────────────────────────────
        GameObject titleGo = new GameObject("Legend_Title");
        titleGo.transform.SetParent(legendPanel.transform, false);
        RectTransform titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.6f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(10f, 0f);
        titleRect.offsetMax = new Vector2(-10f, 0f);
        TextMeshProUGUI titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "Renk: " + title;
        titleTxt.fontSize = 22f;
        titleTxt.color = Color.white;
        titleTxt.alignment = TextAlignmentOptions.Center;

        // ── Gradient bar (RawImage + texture) ───────────────────────
        GameObject barGo = new GameObject("Legend_GradientBar");
        barGo.transform.SetParent(legendPanel.transform, false);
        RectTransform barRect = barGo.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.05f, 0.25f);
        barRect.anchorMax = new Vector2(0.95f, 0.58f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;

        RawImage barImg = barGo.AddComponent<RawImage>();
        Texture2D tex = new Texture2D(256, 1);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < 256; x++) tex.SetPixel(x, 0, GetPlasmaColor(x / 255f));
        tex.Apply();
        barImg.texture = tex;

        // ── Tick etiketleri ─────────────────────────────────────────
        bool isCategorical = textColorMap != null && textColorMap.Count > 0;
        List<(float t, string label)> ticks = new List<(float, string)>();

        if (isCategorical)
        {
            var sortedKeys = textColorMap.Keys.ToList();
            sortedKeys.Sort();
            int maxShow = 8;
            int step = Mathf.Max(1, sortedKeys.Count / maxShow);
            for (int i = 0; i < sortedKeys.Count && ticks.Count < maxShow; i += step)
            {
                string cat = sortedKeys[i];
                float val = textColorMap[cat];
                float t = (maxC > minC) ? Mathf.InverseLerp(minC, maxC, val) : 0.5f;
                string lbl = cat.Length > 8 ? cat.Substring(0, 8) + "." : cat;
                ticks.Add((t, lbl));
            }
        }
        else if (cTicks != null && cTicks.Length > 0)
        {
            foreach (float val in cTicks)
            {
                float t = Mathf.InverseLerp(minC, maxC, val);
                ticks.Add((t, val.ToString("0.##")));
            }
        }
        else
        {
            CalculateNiceScale(minC, maxC, out float nMin, out float nMax, out float nStep);
            if (nStep <= 0) nStep = 1;
            for (float val = nMin; val <= nMax + 0.001f; val += nStep)
                if (val >= minC && val <= maxC)
                    ticks.Add((Mathf.InverseLerp(minC, maxC, val), val.ToString("0.##")));
        }

        foreach (var (t, lbl) in ticks)
        {
            GameObject tickGo = new GameObject("Legend_Tick");
            tickGo.transform.SetParent(legendPanel.transform, false);
            RectTransform tickRect = tickGo.AddComponent<RectTransform>();
            float ancX = Mathf.Lerp(0.05f, 0.95f, t);
            tickRect.anchorMin = new Vector2(ancX - 0.06f, 0f);
            tickRect.anchorMax = new Vector2(ancX + 0.06f, 0.28f);
            tickRect.offsetMin = Vector2.zero;
            tickRect.offsetMax = Vector2.zero;
            TextMeshProUGUI tickTxt = tickGo.AddComponent<TextMeshProUGUI>();
            tickTxt.text = lbl;
            tickTxt.fontSize = 18f;
            tickTxt.color = Color.white;
            tickTxt.alignment = TextAlignmentOptions.Center;
        }
    }

    private void InitializeTooltip()
    {
        if (tooltipObj != null) Destroy(tooltipObj);

        tooltipObj = new GameObject("Tooltip_Panel");
        tooltipObj.transform.SetParent(currentChartRoot.transform);

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(bg.GetComponent<Collider>());
        bg.transform.SetParent(tooltipObj.transform);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(0.6f, 0.35f, 1f);
        bg.GetComponent<Renderer>().material = NewUnlitColorMat(new Color(0.2f, 0.2f, 0.2f, 1.0f));

        GameObject textObj = new GameObject("Tooltip_Text");
        textObj.transform.SetParent(tooltipObj.transform);
        textObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        tooltipText = textObj.AddComponent<TextMeshPro>();
        tooltipText.fontSize = 0.45f;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.Center;

        billboardItems.Add(tooltipObj.transform);
        tooltipObj.SetActive(false);
    }

    private void ShowTooltip(string info, Vector3 position)
    {
        if (tooltipObj == null) return;
        tooltipText.text = info;
        Vector3 forwardOffset = Camera.main != null ? Camera.main.transform.forward * 0.4f : Vector3.zero;
        tooltipObj.transform.position = position + Vector3.up * 0.1f + Vector3.left * 0.05f - forwardOffset;
        tooltipObj.SetActive(true);
    }

    private void HideTooltip()
    {
        if (tooltipObj != null) tooltipObj.SetActive(false);
    }
}