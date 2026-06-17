using UnityEngine;
using UnityEngine.EventSystems;

public class RTSCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private MapData mapData;

    [Header("Start")]
    [SerializeField] private bool startAtMapCenter = true;
    [SerializeField] private float pivotHeight = 0f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 35f;
    [SerializeField] private float fastMoveMultiplier = 2f;
    [SerializeField] private float boundsPadding = 5f;

    [Header("Rotation")]
    [SerializeField] private KeyCode rotationKey = KeyCode.LeftAlt;
    [SerializeField] private float yawSpeed = 4f;
    [SerializeField] private float pitchSpeed = 3f;
    [SerializeField] private float minPitch = 25f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 15f;
    [SerializeField] private float minZoom = 15f;
    [SerializeField] private float maxZoom = 70f;

    [Header("Optional Edge Scroll")]
    [SerializeField] private bool useEdgeScroll = false;
    [SerializeField] private float edgeScrollSize = 15f;

    [Header("UI")]
    [SerializeField] private bool ignoreMouseInputOverUI = true;

    private float yaw;
    private float pitch;
    private float zoom;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponentInChildren<Camera>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (mapData == null)
            mapData = MapData.Instance;
    }

    private void Start()
    {
        if (mapData == null)
            mapData = MapData.Instance;

        yaw = transform.eulerAngles.y;

        if (targetCamera != null)
        {
            Vector3 localEuler = targetCamera.transform.localEulerAngles;
            pitch = NormalizeAngle(localEuler.x);

            if (pitch < minPitch || pitch > maxPitch)
                pitch = 45f;

            zoom = targetCamera.transform.localPosition.magnitude;

            if (zoom < minZoom || zoom > maxZoom)
                zoom = 35f;
        }
        else
        {
            pitch = 45f;
            zoom = 35f;
        }

        if (startAtMapCenter && mapData != null)
        {
            transform.position = mapData.GetMapCenterWorld(pivotHeight);
        }

        ClampToMap();
        ApplyCameraTransform();
    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();

        ClampToMap();
        ApplyCameraTransform();
    }

    private void HandleMovement()
    {
        Vector3 input = Vector3.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            input.z += 1f;

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            input.z -= 1f;

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            input.x += 1f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            input.x -= 1f;

        if (useEdgeScroll && !IsPointerOverUI())
        {
            Vector3 mousePosition = Input.mousePosition;

            if (mousePosition.x <= edgeScrollSize)
                input.x -= 1f;
            else if (mousePosition.x >= Screen.width - edgeScrollSize)
                input.x += 1f;

            if (mousePosition.y <= edgeScrollSize)
                input.z -= 1f;
            else if (mousePosition.y >= Screen.height - edgeScrollSize)
                input.z += 1f;
        }

        if (input.sqrMagnitude < 0.01f)
            return;

        input.Normalize();

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 moveDirection = forward * input.z + right * input.x;

        float speed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= fastMoveMultiplier;

        transform.position += moveDirection * speed * Time.deltaTime;
    }

    private void HandleRotation()
    {
        if (!Input.GetKey(rotationKey))
            return;

        if (ignoreMouseInputOverUI && IsPointerOverUI())
            return;

        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        yaw += mouseX * yawSpeed;
        pitch -= mouseY * pitchSpeed;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void HandleZoom()
    {
        if (ignoreMouseInputOverUI && IsPointerOverUI())
            return;

        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        zoom -= scroll * zoomSpeed;
        zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
    }

    private void ApplyCameraTransform()
    {
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (targetCamera == null)
            return;

        Quaternion pitchRotation = Quaternion.Euler(pitch, 0f, 0f);

        targetCamera.transform.localRotation = pitchRotation;
        targetCamera.transform.localPosition = pitchRotation * new Vector3(0f, 0f, -zoom);
    }

    private void ClampToMap()
    {
        if (mapData == null)
            return;

        Vector3 position = transform.position;
        position.y = pivotHeight;

        position = mapData.ClampWorldPosition(position, boundsPadding);

        transform.position = position;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }

    public Vector3 GetPivotPosition()
    {
        return transform.position;
    }

    public float GetYaw()
    {
        return yaw;
    }

    public float GetPitch()
    {
        return pitch;
    }

    public float GetZoom()
    {
        return zoom;
    }
}