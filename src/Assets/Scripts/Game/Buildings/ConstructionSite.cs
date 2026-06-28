using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Building))]
[RequireComponent(typeof(BuildingData))]
public class ConstructionSite : NetworkBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float visualHeightScale = 1f;

    [Header("Collision")]
    [SerializeField] private BoxCollider boxCollider;
    [SerializeField] private NavMeshObstacle navMeshObstacle;
    [SerializeField] private float colliderHeight = 2f;
    [SerializeField] private float colliderCenterY = 1f;

    [Header("Construction")]
    [SerializeField] private float startingProgressPercent = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool debugConstruction = false;

    public NetworkVariable<FixedString64Bytes> ConstructedBuildingId = new NetworkVariable<FixedString64Bytes>(
        new FixedString64Bytes(""),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<FixedString64Bytes> ConstructedBuildingName = new NetworkVariable<FixedString64Bytes>(
        new FixedString64Bytes("Construction Site"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> BuildProgress = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> RequiredBuildWork = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<Vector2> FootprintSize = new NetworkVariable<Vector2>(
        new Vector2(6f, 6f),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Building building;
    private BuildingData buildingData;
    private GameObject finalBuildingPrefab;

    private bool initialized;
    private bool completed;

    public float Progress01
    {
        get
        {
            if (RequiredBuildWork.Value <= 0.01f)
                return 0f;

            return Mathf.Clamp01(BuildProgress.Value / RequiredBuildWork.Value);
        }
    }

    private void Awake()
    {
        building = GetComponent<Building>();
        buildingData = GetComponent<BuildingData>();

        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider>();

        if (navMeshObstacle == null)
            navMeshObstacle = GetComponent<NavMeshObstacle>();
    }

    private void Update()
    {
        if (!IsServer)
            return;

        ServerSyncProgressWithHealth();
    }

    public override void OnNetworkSpawn()
    {
        FootprintSize.OnValueChanged += HandleFootprintChanged;
        ApplyFootprintSize(FootprintSize.Value);
    }

    public override void OnNetworkDespawn()
    {
        FootprintSize.OnValueChanged -= HandleFootprintChanged;
    }

    private void HandleFootprintChanged(Vector2 oldValue, Vector2 newValue)
    {
        ApplyFootprintSize(newValue);
    }

    public void ServerInitialize(ulong ownerClientId, BuildableBuilding buildableBuilding)
    {
        if (!IsServer)
            return;

        if (buildableBuilding == null || buildableBuilding.FinalBuildingPrefab == null)
            return;

        finalBuildingPrefab = buildableBuilding.FinalBuildingPrefab;

        Vector2 footprint = buildableBuilding.FootprintSize;

        if (footprint.x <= 0f)
            footprint.x = 6f;

        if (footprint.y <= 0f)
            footprint.y = 6f;

        float finalMaxHealth = GetFinalBuildingMaxHealth(finalBuildingPrefab);
        float startPercent = Mathf.Clamp01(startingProgressPercent);

        building.PlayerClientId.Value = ownerClientId;
        building.MaxHealth.Value = finalMaxHealth;
        building.Health.Value = Mathf.Max(1f, finalMaxHealth * startPercent);

        ConstructedBuildingId.Value = new FixedString64Bytes(buildableBuilding.BuildingId);
        ConstructedBuildingName.Value = new FixedString64Bytes(buildableBuilding.DisplayName);

        RequiredBuildWork.Value = Mathf.Max(0.1f, buildableBuilding.BuildTime);
        BuildProgress.Value = RequiredBuildWork.Value * startPercent;
        FootprintSize.Value = footprint;

        if (buildingData != null)
        {
            buildingData.BuildingId = "construction_site";
            buildingData.DisplayName = "Construction Site";
            buildingData.MaxHealth = finalMaxHealth;
            buildingData.CanBuildUnits = false;
        }

        initialized = true;
        completed = false;

        ApplyFootprintSize(footprint);

        DebugConstruction($"Started construction: {buildableBuilding.DisplayName}");
    }

    public void ServerAddBuildWork(ulong builderOwnerClientId, float buildWorkAmount)
    {
        if (!IsServer)
            return;

        if (!initialized || completed || building == null)
            return;

        if (building.Health.Value <= 0f)
            return;

        if (builderOwnerClientId != building.PlayerClientId.Value)
            return;

        if (buildWorkAmount <= 0f)
            return;

        ServerSyncProgressWithHealth();

        float oldProgress = BuildProgress.Value;

        BuildProgress.Value = Mathf.Min(
            BuildProgress.Value + buildWorkAmount,
            RequiredBuildWork.Value
        );

        float addedProgress = BuildProgress.Value - oldProgress;

        float healthGain = 0f;

        if (RequiredBuildWork.Value > 0.01f)
            healthGain = building.MaxHealth.Value * (addedProgress / RequiredBuildWork.Value);

        building.Health.Value = Mathf.Min(
            building.Health.Value + healthGain,
            building.MaxHealth.Value
        );

        if (BuildProgress.Value >= RequiredBuildWork.Value &&
            building.Health.Value >= building.MaxHealth.Value - 0.5f)
        {
            ServerCompleteConstruction();
        }
    }

    private void ServerSyncProgressWithHealth()
    {
        if (!IsServer)
            return;

        if (!initialized || completed || building == null)
            return;

        if (building.MaxHealth.Value <= 0.01f)
            return;

        if (building.Health.Value <= 0f)
            return;

        float healthProgress01 = Mathf.Clamp01(building.Health.Value / building.MaxHealth.Value);
        float progressFromHealth = RequiredBuildWork.Value * healthProgress01;

        if (progressFromHealth < BuildProgress.Value)
            BuildProgress.Value = progressFromHealth;
    }

    private void ServerCompleteConstruction()
    {
        if (!IsServer)
            return;

        if (completed || finalBuildingPrefab == null || building == null)
            return;

        completed = true;

        GameObject finalBuildingObject = Instantiate(
            finalBuildingPrefab,
            transform.position,
            transform.rotation
        );

        Building finalBuilding = finalBuildingObject.GetComponent<Building>();
        NetworkObject finalNetworkObject = finalBuildingObject.GetComponent<NetworkObject>();

        if (finalBuilding == null || finalNetworkObject == null)
        {
            Destroy(finalBuildingObject);
            completed = false;
            return;
        }

        finalBuilding.PlayerClientId.Value = building.PlayerClientId.Value;

        BuildingData finalBuildingData = finalBuildingObject.GetComponent<BuildingData>();

        if (finalBuildingData != null)
        {
            finalBuilding.MaxHealth.Value = finalBuildingData.MaxHealth;
            finalBuilding.Health.Value = finalBuildingData.MaxHealth;
        }
        else
        {
            finalBuilding.MaxHealth.Value = building.MaxHealth.Value;
            finalBuilding.Health.Value = building.MaxHealth.Value;
        }

        finalNetworkObject.Spawn();
        DespawnConstructionSite();
    }

    private void DespawnConstructionSite()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();

        if (networkObject != null && networkObject.IsSpawned)
            networkObject.Despawn(true);
        else
            Destroy(gameObject);
    }

    private float GetFinalBuildingMaxHealth(GameObject prefab)
    {
        if (prefab == null)
            return 500f;

        BuildingData data = prefab.GetComponent<BuildingData>();

        if (data != null && data.MaxHealth > 0f)
            return data.MaxHealth;

        return 500f;
    }

    private void ApplyFootprintSize(Vector2 footprint)
    {
        if (visualRoot != null)
            visualRoot.localScale = new Vector3(footprint.x, visualHeightScale, footprint.y);

        if (boxCollider != null)
        {
            boxCollider.size = new Vector3(footprint.x, colliderHeight, footprint.y);
            boxCollider.center = new Vector3(0f, colliderCenterY, 0f);
        }

        if (navMeshObstacle != null)
        {
            navMeshObstacle.shape = NavMeshObstacleShape.Box;
            navMeshObstacle.size = new Vector3(footprint.x, colliderHeight, footprint.y);
            navMeshObstacle.center = new Vector3(0f, colliderCenterY, 0f);
        }
    }

    private void DebugConstruction(string message)
    {
        if (!debugConstruction)
            return;

        Debug.Log($"[{gameObject.name}] {message}");
    }
}