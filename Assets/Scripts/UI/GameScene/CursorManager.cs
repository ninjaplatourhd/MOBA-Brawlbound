using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance;

    [SerializeField] private Texture2D normalCursor;
    [SerializeField] private Texture2D clickCursor;

    // piksel koji je klik
    [SerializeField] private Vector2 normalHotspot = Vector2.zero;
    [SerializeField] private Vector2 clickHotspot = Vector2.zero;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Cursor.SetCursor(normalCursor, normalHotspot, CursorMode.Auto);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Cursor.SetCursor(clickCursor, clickHotspot, CursorMode.Auto);
        }

        if (Input.GetMouseButtonUp(0))
        {
            Cursor.SetCursor(normalCursor, normalHotspot, CursorMode.Auto);
        }
    }
}