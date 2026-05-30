using UnityEngine;

/// <summary>
/// Kolonun state'ini, verilerini ve görsellerini yönetir.
/// </summary>
public class ARAxisController : MonoBehaviour
{
    public string columnName;
    public float[] rawData;
    public float[] normalizedData;

    // --- YENİ: Kategorik veriler için özellikler ---
    public bool isCategorical = false;
    public string[] categories;
    // -----------------------------------------------

    public bool isSelected = false;
    public bool isDragging = false;

    private Renderer visualRenderer;
    private Color originalColor = new Color(0.7f, 0.7f, 0.7f);
    private Color selectedColor = new Color(0.2f, 0.6f, 1f);

    private Vector3 defaultPosition;
    private Quaternion defaultRotation;
    private Collider axisCollider;

    void Start()
    {
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;
        visualRenderer = GetComponentInChildren<Renderer>();
        axisCollider = GetComponent<Collider>();
        if (visualRenderer) visualRenderer.material.color = originalColor;
    }

    void LateUpdate()
    {
        UpdateVisuals();
    }

    public void SetDragging(bool val) => isDragging = val;

    public Collider GetCollider() => axisCollider;

    public Vector3 GetDefaultPos() => defaultPosition;

    public void SetDefaultPos(Vector3 pos, Quaternion rot)
    {
        defaultPosition = pos;
        defaultRotation = rot;
    }

    void UpdateVisuals()
    {
        if (!visualRenderer) return;
        Color target = isSelected
            ? (isDragging ? new Color(1f, 0.75f, 0.1f) : selectedColor)
            : originalColor;
        visualRenderer.material.color = Color.Lerp(visualRenderer.material.color, target, Time.deltaTime * 12f);
    }

    public void ResetToDefault()
    {
        transform.position = defaultPosition;
        transform.rotation = defaultRotation;
        isSelected = false;
        isDragging = false;
    }
}