using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BuildingPlacementSystem : MonoBehaviour
{
    public static BuildingPlacementSystem Instance { get; private set; }

    [Header("Layers")]
    [SerializeField] private LayerMask buildableGroundMask;
    [SerializeField] private LayerMask blockingMask;

    [Header("Preview Materials")]
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    [Header("Placement")]
    [SerializeField] private float rayDistance = 1000f;
    [SerializeField] private float overlapCheckHeight = 4f;
    [SerializeField] private float placementYOffset = 0.1f;
    [SerializeField] private bool requireVisibleArea = true;

    private Camera mainCamera;

    private BuildableBuilding currentBuildableBuilding;
    private WorkerBuilder currentWorkerBuilder;

    private GameObject currentPreviewObject;
    private Renderer[] previewRenderers;

    private Vector3 currentPlacementPosition;
    private Quaternion currentPlacementRotation = Quaternion.identity;

    private bool currentPlacementValid;
    private float currentRotationY;

    public bool IsPlacing => currentBuildableBuilding != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        mainCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsPlacing)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentRotationY += 90f;
            currentPlacementRotation = Quaternion.Euler(0f, currentRotationY, 0f);
        }

        UpdatePreview();

        if (Input.GetMouseButtonDown(0))
        {
            TryConfirmPlacement();
        }
    }

    public void StartPlacement(BuildableBuilding buildableBuilding, WorkerBuilder workerBuilder)
    {
        if (buildableBuilding == null)
            return;

        if (workerBuilder == null)
            return;

        if (!workerBuilder.CanBuild(buildableBuilding))
        {
            Debug.LogWarning($"Ne možeš trenutno da gradiš: {buildableBuilding.DisplayName}");
            return;
        }

        CancelPlacement();

        currentBuildableBuilding = buildableBuilding;
        currentWorkerBuilder = workerBuilder;

        currentRotationY = 0f;
        currentPlacementRotation = Quaternion.identity;

        if (buildableBuilding.PreviewPrefab == null)
        {
            Debug.LogWarning($"{buildableBuilding.DisplayName} nema PreviewPrefab.");
            return;
        }

        currentPreviewObject = Instantiate(buildableBuilding.PreviewPrefab);
        previewRenderers = currentPreviewObject.GetComponentsInChildren<Renderer>(true);

        Debug.Log($"Placement mode started for: {buildableBuilding.DisplayName}");
    }

    public void CancelPlacement()
    {
        if (currentPreviewObject != null)
            Destroy(currentPreviewObject);

        currentPreviewObject = null;
        previewRenderers = null;

        currentBuildableBuilding = null;
        currentWorkerBuilder = null;

        currentPlacementValid = false;
    }

    private void UpdatePreview()
    {
        if (mainCamera == null)
            return;

        if (currentBuildableBuilding == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, buildableGroundMask))
        {
            currentPlacementValid = false;
            SetPreviewVisible(false);
            return;
        }

        currentPlacementPosition = hit.point;
        currentPlacementPosition.y += placementYOffset;

        currentPlacementValid = IsPlacementValid(
            currentPlacementPosition,
            currentPlacementRotation,
            currentBuildableBuilding
        );

        if (currentWorkerBuilder != null && !currentWorkerBuilder.CanBuild(currentBuildableBuilding))
        {
            currentPlacementValid = false;
        }

        if (currentPreviewObject != null)
        {
            currentPreviewObject.SetActive(true);
            currentPreviewObject.transform.position = currentPlacementPosition;
            currentPreviewObject.transform.rotation = currentPlacementRotation;

            SetPreviewMaterial(currentPlacementValid);
        }
    }

    private void TryConfirmPlacement()
    {
        if (InputBlocker.IsPointerOverUI())
            return;

        if (!currentPlacementValid)
        {
            Debug.LogWarning("Ne možeš tu da postaviš building.");
            return;
        }

        if (currentBuildableBuilding == null)
            return;

        if (currentWorkerBuilder == null)
            return;

        NetworkObjectReference[] selectedWorkerReferences = GetSelectedWorkerReferences();

        currentWorkerBuilder.RequestPlaceBuilding(
            currentBuildableBuilding.BuildingId,
            currentPlacementPosition,
            currentPlacementRotation,
            selectedWorkerReferences
        );

        CancelPlacement();
    }

    public void ServerTryPlaceBuilding(
     WorkerBuilder workerBuilder,
     ulong ownerClientId,
     string buildingId,
     Vector3 position,
     Quaternion rotation,
     NetworkObjectReference[] selectedWorkerReferences)
    {
        if (workerBuilder == null)
        {
            Debug.LogWarning("[SERVER] WorkerBuilder je null.");
            return;
        }

        if (PlayerEconomyManager.Instance == null)
        {
            Debug.LogWarning("[SERVER] PlayerEconomyManager.Instance ne postoji.");
            return;
        }

        BuildableBuilding buildableBuilding = workerBuilder.FindBuildableBuilding(buildingId);

        if (buildableBuilding == null)
        {
            Debug.LogWarning($"[SERVER] Nije pronađen BuildableBuilding za id: {buildingId}");
            return;
        }

        if (!ServerCanPlayerBuild(ownerClientId, buildableBuilding))
        {
            Debug.LogWarning("[SERVER] Igrač nema resurse, tech tier ili power za gradnju.");
            return;
        }

        if (!IsPlacementValid(position, rotation, buildableBuilding))
        {
            Debug.LogWarning("[SERVER] Placement nije validan na serveru.");
            return;
        }

        if (!PlayerEconomyManager.Instance.TrySpendMinerals(ownerClientId, buildableBuilding.MineralCost))
        {
            Debug.LogWarning("[SERVER] TrySpendMinerals nije prošao.");
            return;
        }

        ConstructionSite constructionSite = SpawnConstructionSite(
    ownerClientId,
    buildableBuilding,
    position,
    rotation
);

        if (constructionSite != null)
        {
            ServerOrderSelectedWorkersToBuild(
                selectedWorkerReferences,
                ownerClientId,
                constructionSite,
                workerBuilder
            );
        }
    }

    private bool ServerCanPlayerBuild(ulong clientId, BuildableBuilding buildableBuilding)
    {
        if (PlayerEconomyManager.Instance == null)
            return false;

        if (!PlayerEconomyManager.Instance.TryGetPlayerState(clientId, out PlayerGameData gammeData))
            return false;

        if (gammeData.TechTier < buildableBuilding.RequiredTechTier)
            return false;

        if (gammeData.Minerals < buildableBuilding.MineralCost)
            return false;

        if (gammeData.PowerAvailable < buildableBuilding.RequiredFreePower)
            return false;

        return true;
    }

    private ConstructionSite SpawnConstructionSite(
        ulong ownerClientId,
        BuildableBuilding buildableBuilding,
        Vector3 position,
        Quaternion rotation)
    {
        if (buildableBuilding.ConstructionSitePrefab == null)
        {
            Debug.LogError($"[SERVER] {buildableBuilding.DisplayName} nema ConstructionSitePrefab.");
            return null;
        }

        GameObject siteObject = Instantiate(
            buildableBuilding.ConstructionSitePrefab,
            position,
            rotation
        );

        ConstructionSite constructionSite = siteObject.GetComponent<ConstructionSite>();
        Building building = siteObject.GetComponent<Building>();
        Unity.Netcode.NetworkObject networkObject = siteObject.GetComponent<Unity.Netcode.NetworkObject>();

        if (constructionSite == null)
        {
            Debug.LogError("[SERVER] ConstructionSite prefab nema ConstructionSite skriptu.");
            Destroy(siteObject);
            return null;
        }

        if (building == null)
        {
            Debug.LogError("[SERVER] ConstructionSite prefab nema Building komponentu.");
            Destroy(siteObject);
            return null;
        }

        if (networkObject == null)
        {
            Debug.LogError("[SERVER] ConstructionSite prefab nema NetworkObject.");
            Destroy(siteObject);
            return null;
        }

        networkObject.Spawn();

        constructionSite.ServerInitialize(ownerClientId, buildableBuilding);

        Debug.Log($"[SERVER] Spawnovan construction site za: {buildableBuilding.DisplayName}");
        return constructionSite;
    }

    private bool IsPlacementValid(
        Vector3 position,
        Quaternion rotation,
        BuildableBuilding buildableBuilding)
    {
        if (buildableBuilding == null)
            return false;

        if (buildableBuilding.RequiresVisibleArea && requireVisibleArea)
        {
            if (FogOfWar.Instance != null && !FogOfWar.Instance.IsVisibleNow(position))
                return false;
        }

        Vector2 footprint = buildableBuilding.FootprintSize;

        if (footprint.x <= 0f)
            footprint.x = 6f;

        if (footprint.y <= 0f)
            footprint.y = 6f;

        Vector3 checkCenter = position + Vector3.up * (overlapCheckHeight * 0.5f);

        Vector3 halfExtents = new Vector3(
            footprint.x * 0.5f,
            overlapCheckHeight * 0.5f,
            footprint.y * 0.5f
        );

        Collider[] hits = Physics.OverlapBox(
            checkCenter,
            halfExtents,
            rotation,
            blockingMask,
            QueryTriggerInteraction.Ignore
        );

        return hits.Length == 0;
    }

    private void SetPreviewVisible(bool visible)
    {
        if (currentPreviewObject != null)
            currentPreviewObject.SetActive(visible);
    }

    private void SetPreviewMaterial(bool valid)
    {
        if (previewRenderers == null)
            return;

        Material material = valid ? validMaterial : invalidMaterial;

        if (material == null)
            return;

        for (int i = 0; i < previewRenderers.Length; i++)
        {
            if (previewRenderers[i] == null)
                continue;

            Material[] materials = previewRenderers[i].materials;

            for (int j = 0; j < materials.Length; j++)
            {
                materials[j] = material;
            }

            previewRenderers[i].materials = materials;
        }
    }

    private void ServerOrderSelectedWorkersToBuild(
    NetworkObjectReference[] selectedWorkerReferences,
    ulong ownerClientId,
    ConstructionSite constructionSite,
    WorkerBuilder fallbackWorkerBuilder)
    {
        HashSet<ulong> orderedWorkerIds = new HashSet<ulong>();

        if (selectedWorkerReferences != null)
        {
            foreach (NetworkObjectReference workerReference in selectedWorkerReferences)
            {
                if (!workerReference.TryGet(out NetworkObject workerNetworkObject))
                    continue;

                WorkerBuilder workerBuilder = workerNetworkObject.GetComponent<WorkerBuilder>();
                Unit unit = workerNetworkObject.GetComponent<Unit>();

                if (workerBuilder == null || unit == null)
                    continue;

                if (unit.PlayerClientId.Value != ownerClientId)
                    continue;

                workerBuilder.ServerStartBuilding(constructionSite);
                orderedWorkerIds.Add(workerNetworkObject.NetworkObjectId);
            }
        }

        if (fallbackWorkerBuilder != null)
        {
            NetworkObject fallbackNetworkObject = fallbackWorkerBuilder.GetComponent<NetworkObject>();

            if (fallbackNetworkObject != null &&
                !orderedWorkerIds.Contains(fallbackNetworkObject.NetworkObjectId))
            {
                fallbackWorkerBuilder.ServerStartBuilding(constructionSite);
            }
        }
    }

    private NetworkObjectReference[] GetSelectedWorkerReferences()
    {
        List<NetworkObjectReference> references = new List<NetworkObjectReference>();

        UnitManager unitManager = UnitManager.instance;

        if (unitManager == null)
            return references.ToArray();

        List<WorkerBuilder> selectedWorkers = unitManager.GetSelectedWorkerBuilders();

        foreach (WorkerBuilder workerBuilder in selectedWorkers)
        {
            if (workerBuilder == null)
                continue;

            NetworkObject networkObject = workerBuilder.GetComponent<NetworkObject>();

            if (networkObject == null)
                continue;

            references.Add(new NetworkObjectReference(networkObject));
        }

        return references.ToArray();
    }

    private void OnDrawGizmosSelected()
    {
        if (!IsPlacing || currentBuildableBuilding == null)
            return;

        Vector2 footprint = currentBuildableBuilding.FootprintSize;

        if (footprint.x <= 0f)
            footprint.x = 6f;

        if (footprint.y <= 0f)
            footprint.y = 6f;

        Gizmos.color = currentPlacementValid ? Color.green : Color.red;

        Gizmos.matrix = Matrix4x4.TRS(
            currentPlacementPosition + Vector3.up * (overlapCheckHeight * 0.5f),
            currentPlacementRotation,
            Vector3.one
        );

        Gizmos.DrawWireCube(
            Vector3.zero,
            new Vector3(footprint.x, overlapCheckHeight, footprint.y)
        );

        Gizmos.matrix = Matrix4x4.identity;
    }
}