using UnityEngine;
using UnityEngine.UI;

public class MinimapCameraViewUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MapData mapData;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RectTransform minimapRect;

    [Header("Ground Detection")]
    [SerializeField] private bool usePhysicsRaycast = true;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float fallbackGroundY = 0f;
    [SerializeField] private float rayDistance = 5000f;

    [Header("Line Visual")]
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private float lineThickness = 2f;

    private RectTransform[] lines = new RectTransform[4];
    private Image[] lineImages = new Image[4];

    private readonly Vector3[] viewportCorners = new Vector3[4]
    {
        new Vector3(0f, 0f, 0f), // bottom left
        new Vector3(0f, 1f, 0f), // top left
        new Vector3(1f, 1f, 0f), // top right
        new Vector3(1f, 0f, 0f)  // bottom right
    };

    private readonly Vector3[] worldCorners = new Vector3[4];
    private readonly Vector2[] minimapCorners = new Vector2[4];

    private void Awake()
    {
        if (mapData == null)
            mapData = MapData.Instance;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (minimapRect == null)
            minimapRect = transform.parent as RectTransform;

        CreateLinesIfNeeded();
    }

    private void LateUpdate()
    {
        UpdateCameraViewFrame();
    }

    private void CreateLinesIfNeeded()
    {
        for (int i = 0; i < lines.Length; i++)
        {
            GameObject lineObject = new GameObject($"CameraViewLine_{i}", typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(transform, false);

            RectTransform rectTransform = lineObject.GetComponent<RectTransform>();
            Image image = lineObject.GetComponent<Image>();

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            image.color = lineColor;
            image.raycastTarget = false;

            lines[i] = rectTransform;
            lineImages[i] = image;
        }
    }

    private void UpdateCameraViewFrame()
    {
        if (mapData == null || targetCamera == null || minimapRect == null)
            return;

        for (int i = 0; i < viewportCorners.Length; i++)
        {
            Ray ray = targetCamera.ViewportPointToRay(viewportCorners[i]);

            if (!TryGetGroundPoint(ray, out worldCorners[i]))
                return;

            Vector2 normalized = mapData.WorldToNormalizedMapPosition(worldCorners[i]);

            normalized.x = Mathf.Clamp01(normalized.x);
            normalized.y = Mathf.Clamp01(normalized.y);

            minimapCorners[i] = NormalizedToAnchoredPosition(normalized);
        }

        SetLine(lines[0], minimapCorners[0], minimapCorners[1]);
        SetLine(lines[1], minimapCorners[1], minimapCorners[2]);
        SetLine(lines[2], minimapCorners[2], minimapCorners[3]);
        SetLine(lines[3], minimapCorners[3], minimapCorners[0]);

        for (int i = 0; i < lineImages.Length; i++)
        {
            if (lineImages[i] != null)
                lineImages[i].color = lineColor;
        }
    }

    private bool TryGetGroundPoint(Ray ray, out Vector3 point)
    {
        if (usePhysicsRaycast)
        {
            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    rayDistance,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
        }

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, fallbackGroundY, 0f));

        if (groundPlane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private Vector2 NormalizedToAnchoredPosition(Vector2 normalized)
    {
        Rect rect = minimapRect.rect;
        Vector2 pivot = minimapRect.pivot;

        float x = (normalized.x - pivot.x) * rect.width;
        float y = (normalized.y - pivot.y) * rect.height;

        return new Vector2(x, y);
    }

    private void SetLine(RectTransform line, Vector2 start, Vector2 end)
    {
        if (line == null)
            return;

        Vector2 direction = end - start;
        float length = direction.magnitude;

        if (length <= 0.01f)
        {
            line.gameObject.SetActive(false);
            return;
        }

        line.gameObject.SetActive(true);

        Vector2 center = (start + end) * 0.5f;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        line.anchoredPosition = center;
        line.sizeDelta = new Vector2(length, lineThickness);
        line.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}