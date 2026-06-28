using UnityEngine;
using UnityEngine.UI;

public class MinimapIconUI : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image iconImage;

    private void Awake()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (iconImage == null)
            iconImage = GetComponent<Image>();

        if (iconImage != null)
            iconImage.raycastTarget = false;
    }

    public void SetColor(Color color)
    {
        if (iconImage != null)
            iconImage.color = color;
    }

    public void SetAnchoredPosition(Vector2 anchoredPosition)
    {
        if (rectTransform != null)
            rectTransform.anchoredPosition = anchoredPosition;
    }

    public void SetSize(Vector2 size)
    {
        if (rectTransform != null)
            rectTransform.sizeDelta = size;
    }
}