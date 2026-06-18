using UnityEngine;
using UnityEngine.EventSystems;

public class MinimapClickHandler : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    [Header("References")]
    [SerializeField] private MapData mapData;
    [SerializeField] private RTSCameraController cameraController;
    [SerializeField] private RectTransform minimapRect;

    [Header("Input")]
    [SerializeField] private bool allowDrag = true;

    private void Awake()
    {
        if (mapData == null)
            mapData = MapData.Instance;

        if (minimapRect == null)
            minimapRect = GetComponent<RectTransform>();

        if (cameraController == null)
            cameraController = FindFirstObjectByType<RTSCameraController>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        MoveCameraToPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!allowDrag)
            return;

        MoveCameraToPointer(eventData);
    }

    private void MoveCameraToPointer(PointerEventData eventData)
    {
        if (mapData == null || cameraController == null || minimapRect == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                minimapRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Rect rect = minimapRect.rect;

        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        Vector2 normalizedPosition = new Vector2(
            Mathf.Clamp01(normalizedX),
            Mathf.Clamp01(normalizedY)
        );

        cameraController.MoveToNormalizedMapPosition(normalizedPosition);
    }
}