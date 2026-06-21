using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

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

    private void OnDestroy()
    {
        if (BuildQueue != null)
            BuildQueue.Dispose();
    }

    private void Update()
    {
        if (!IsServer)
            return;

        ServerUpdateQueue();
    }

    public List<BuildableUnit> GetBuildableUnits()
    {
        if (data == null)
            data = GetComponent<BuildingData>();

        if (data == null || data.BuildableUnits == null)
            return new List<BuildableUnit>();

        return data.BuildableUnits;
    }

    public List<BuildableUpgrade> GetBuildableUpgrades()
    {
        if (data == null)
            data = GetComponent<BuildingData>();

        if (data == null || data.BuildableUpgrades == null)
            return new List<BuildableUpgrade>();

        return data.BuildableUpgrades;
    }

    public void RequestBuildUnit(string unitId)
    {
        Debug.Log($"RequestBuildUnit pozvan za: {unitId}");

        if (building == null)
            building = GetComponent<Building>();

        if (building == null)
        {
            Debug.LogWarning("RequestBuildUnit failed: building je null.");
            return;
        }

        if (!building.BelongsToLocalPlayer())
        {
            Debug.LogWarning("RequestBuildUnit failed: building ne pripada lokalnom playeru.");
            return;
        }

        RequestBuildUnitServerRpc(unitId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBuildUnitServerRpc(string unitId, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (building == null)
            building = GetComponent<Building>();

        if (building == null)
            return;

        if (building.PlayerClientId.Value != senderClientId)
            return;

        BuildableUnit buildableUnit = FindBuildableUnit(unitId);

        if (buildableUnit == null)
        {
            Debug.LogWarning($"{gameObject.name} ne može da pravi unit: {unitId}");
            return;
        }

        if (PlayerEconomyManager.Instance == null)
        {
            Debug.LogWarning("PlayerEconomyManager ne postoji u sceni.");
            return;
        }

        if (!PlayerEconomyManager.Instance.TryGetPlayerState(senderClientId, out PlayerGameData economyData))
            return;

        if (economyData.TechTier < buildableUnit.RequiredTechTier)
        {
            Debug.LogWarning($"Nemaš potreban tech tier za {buildableUnit.DisplayName}.");
            return;
        }

        if (!PlayerEconomyManager.Instance.CanAfford(
                senderClientId,
                buildableUnit.MineralCost,
                buildableUnit.PowerUpkeep))
        {
            Debug.LogWarning($"Nemaš resurse za {buildableUnit.DisplayName}.");
            return;
        }

        if (!PlayerEconomyManager.Instance.TrySpendMinerals(senderClientId, buildableUnit.MineralCost))
            return;

        BuildQueue.Add(new BuildQueueItemNet
        {
            UnitId = new FixedString64Bytes(buildableUnit.UnitId),
            DisplayName = new FixedString64Bytes(buildableUnit.DisplayName),
            MineralCost = buildableUnit.MineralCost,
            PowerUpkeep = buildableUnit.PowerUpkeep,
            BuildTime = buildableUnit.BuildTime,
            RemainingTime = buildableUnit.BuildTime
        });

        Debug.Log($"{gameObject.name} added to queue: {buildableUnit.DisplayName}");
    }

    public void RequestCancelQueueItem(int index)
    {
        if (building == null)
            building = GetComponent<Building>();

        if (building == null)
            return;

        if (!building.BelongsToLocalPlayer())
            return;

        CancelQueueItemServerRpc(index);
    }

    [ServerRpc(RequireOwnership = false)]
    private void CancelQueueItemServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (building == null)
            building = GetComponent<Building>();

        if (building == null)
            return;

        if (building.PlayerClientId.Value != senderClientId)
            return;

        if (index < 0 || index >= BuildQueue.Count)
            return;

        BuildQueueItemNet item = BuildQueue[index];

        if (PlayerEconomyManager.Instance != null)
            PlayerEconomyManager.Instance.AddMinerals(senderClientId, item.MineralCost);

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

        ulong ownerClientId = building.PlayerClientId.Value;

        if (PlayerEconomyManager.Instance != null)
        {
            if (!PlayerEconomyManager.Instance.CanAfford(ownerClientId, 0, currentItem.PowerUpkeep))
            {
                currentItem.RemainingTime = 0.1f;
                BuildQueue[0] = currentItem;
                return;
            }
        }

        string completedUnitId = currentItem.UnitId.ToString();

        BuildQueue.RemoveAt(0);

        SpawnCompletedUnit(completedUnitId, currentItem.PowerUpkeep);
    }

    private void SpawnCompletedUnit(string unitId, int powerUpkeep)
    {
        BuildableUnit buildableUnit = FindBuildableUnit(unitId);

        if (buildableUnit == null || buildableUnit.UnitPrefab == null)
        {
            Debug.LogError($"Ne mogu da spawnujem unit: {unitId}");
            return;
        }

        Vector3 basePosition = spawnPoint != null
            ? spawnPoint.position
            : transform.position + transform.forward * 4f;

        Vector3 spawnPosition = basePosition;

        if (NavMesh.SamplePosition(basePosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }
        else
        {
            Debug.LogWarning("No NavMesh found near spawn point!");
            return;
        }

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

        PowerConsumer powerConsumer = unitObject.GetComponent<PowerConsumer>();
        if (powerConsumer != null)
            powerConsumer.SetRuntimePowerUpkeep(powerUpkeep);

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