using UnityEngine;

public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform rect;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
    }
}