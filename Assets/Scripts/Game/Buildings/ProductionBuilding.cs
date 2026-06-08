using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class ProductionBuilding : NetworkBehaviour
{
    [SerializeField] private Transform spawnPoint;

    private Building building;
    private BuildingData data;

    public NetworkList<BuildQueueItemNet> BuildQueue { get; private set; }

    private void Awake()
    {
        building = GetComponent<Building>();
        data = GetComponent<BuildingData>();

        BuildQueue = new NetworkList<BuildQueueItemNet>();
    }
    /*
    public override void OnNetworkDespawn()
    {
        BuildQueue.Dispose();
    }
    */

    private void Update()
    {
        if (!IsServer)
            return;

        if (Input.GetKeyDown(KeyCode.B) && building.BelongsToLocalPlayer())
        {
            RequestBuildUnit("worker");
        }

        ServerUpdateQueue();
    }

    public void RequestBuildUnit(string unitId)
    {
        if (building == null)
            building = GetComponent<Building>();

        if (!building.BelongsToLocalPlayer())
            return;

        RequestBuildUnitServerRpc(unitId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBuildUnitServerRpc(string unitId, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (building.PlayerClientId.Value != senderClientId)
            return;

        BuildableUnit buildableUnit = FindBuildableUnit(unitId);

        if (buildableUnit == null)
        {
            Debug.LogWarning($"{gameObject.name} ne može da builduje unit: {unitId}");
            return;
        }

        BuildQueue.Add(new BuildQueueItemNet
        {
            UnitId = new FixedString64Bytes(buildableUnit.UnitId),
            DisplayName = new FixedString64Bytes(buildableUnit.DisplayName),
            BuildTime = buildableUnit.BuildTime,
            RemainingTime = buildableUnit.BuildTime
        });

        Debug.Log($"{gameObject.name} added to queue: {buildableUnit.DisplayName}");
    }

    public void RequestCancelQueueItem(int index)
    {
        if (building == null)
            building = GetComponent<Building>();

        if (!building.BelongsToLocalPlayer())
            return;

        CancelQueueItemServerRpc(index);
    }

    [ServerRpc(RequireOwnership = false)]
    private void CancelQueueItemServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (building.PlayerClientId.Value != senderClientId)
            return;

        if (index < 0 || index >= BuildQueue.Count)
            return;

        BuildQueue.RemoveAt(index);
    }

    private void ServerUpdateQueue()
    {
        if (BuildQueue.Count == 0)
            return;

        BuildQueueItemNet currentItem = BuildQueue[0];

        currentItem.RemainingTime -= Time.deltaTime;

        if (currentItem.RemainingTime > 0f)
        {
            BuildQueue[0] = currentItem;
            return;
        }

        string completedUnitId = currentItem.UnitId.ToString();

        BuildQueue.RemoveAt(0);

        SpawnCompletedUnit(completedUnitId);
    }

    private void SpawnCompletedUnit(string unitId)
    {
        BuildableUnit buildableUnit = FindBuildableUnit(unitId);

        if (buildableUnit == null || buildableUnit.UnitPrefab == null)
        {
            Debug.LogError($"Ne mogu da spawnujem unit: {unitId}");
            return;
        }

        Vector3 spawnPosition = spawnPoint != null
            ? spawnPoint.position
            : transform.position + transform.forward * 4f;

        GameObject unitObject = Instantiate(
            buildableUnit.UnitPrefab,
            spawnPosition,
            Quaternion.identity
        );

        Unit unit = unitObject.GetComponent<Unit>();
        NetworkObject netObj = unitObject.GetComponent<NetworkObject>();

        if (unit == null || netObj == null)
        {
            Debug.LogError($"{buildableUnit.UnitPrefab.name} mora imati Unit i NetworkObject.");
            Destroy(unitObject);
            return;
        }

        unit.PlayerClientId.Value = building.PlayerClientId.Value;

        netObj.Spawn();

        Debug.Log($"Spawned completed unit: {unitId}");
    }

    private BuildableUnit FindBuildableUnit(string unitId)
    {
        if (data == null)
            data = GetComponent<BuildingData>();

        if (data == null || data.BuildableUnits == null)
            return null;

        foreach (BuildableUnit unit in data.BuildableUnits)
        {
            if (unit.UnitId == unitId)
                return unit;
        }

        return null;
    }
}