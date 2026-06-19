using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class FogOfWarWorldOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MapData mapData;
    [SerializeField] private FogOfWar fogOfWarSystem;
    [SerializeField] private Material fogOverlayMaterial;

    [Header("Placement")]
    [SerializeField] private float overlayHeight = 0.15f;
    [SerializeField] private bool rebuildOnStart = true;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material runtimeMaterial;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (mapData == null)
            mapData = MapData.Instance;

        if (fogOfWarSystem == null)
            fogOfWarSystem = FogOfWar.Instance;
    }

    private void Start()
    {
        if (mapData == null)
            mapData = MapData.Instance;

        if (fogOfWarSystem == null)
            fogOfWarSystem = FogOfWar.Instance;

        SetupMaterial();

        if (rebuildOnStart)
            BuildOverlayMesh();

        ApplyFogTexture();
    }

    private void LateUpdate()
    {
        ApplyFogTexture();
    }

    private void SetupMaterial()
    {
        if (fogOverlayMaterial == null)
        {
            Debug.LogError($"{gameObject.name}: Fog Overlay Material nije povezan.");
            return;
        }

        runtimeMaterial = new Material(fogOverlayMaterial);
        meshRenderer.material = runtimeMaterial;

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void BuildOverlayMesh()
    {
        if (mapData == null)
        {
            Debug.LogError($"{gameObject.name}: MapData nije pronađen.");
            return;
        }

        Vector2 mapMin = mapData.MapMin;
        Vector2 mapSize = mapData.MapSize;

        Vector3[] vertices =
        {
            new Vector3(0f, overlayHeight, 0f),
            new Vector3(0f, overlayHeight, mapSize.y),
            new Vector3(mapSize.x, overlayHeight, mapSize.y),
            new Vector3(mapSize.x, overlayHeight, 0f)
        };

        Vector2[] uvs =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };

        int[] triangles =
        {
            0, 1, 2,
            0, 2, 3
        };

        Mesh mesh = new Mesh();
        mesh.name = "FogOfWarWorldOverlayMesh";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        meshFilter.sharedMesh = mesh;

        transform.position = new Vector3(mapMin.x, 0f, mapMin.y);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void ApplyFogTexture()
    {
        if (runtimeMaterial == null)
            return;

        if (fogOfWarSystem == null)
            fogOfWarSystem = FogOfWar.Instance;

        if (fogOfWarSystem == null || fogOfWarSystem.FogTexture == null)
            return;

        runtimeMaterial.SetTexture("_MainTex", fogOfWarSystem.FogTexture);
    }
}