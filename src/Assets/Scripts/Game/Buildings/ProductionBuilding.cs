using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class ProductionBuilding : NetworkBehaviour
{
    private static readonly Dictionary<ulong, HashSet<string>> completedUpgradesByPlayer = new Dictionary<ulong, HashSet<string>>();

    [SerializeField] private Transform spawnPoint;
    [SerializeField] private int maxQueueSize = 5;

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
        List<BuildableUpgrade> availableUpgrades = new List<BuildableUpgrade>();

        if (data == null || data.BuildableUpgrades == null)
            return availableUpgrades;

        if (PlayerEconomyManager.Instance == null)
            return availableUpgrades;

        if (NetworkManager.Singleton == null)
            return availableUpgrades;

        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        foreach (BuildableUpgrade upgrade in data.BuildableUpgrades)
        {
            if (upgrade == null)
                continue;

            if (IsUpgradeCompleted(localClientId, upgrade))
                continue;

            if (IsUpgradeQueued(upgrade.UpgradeId))
                continue;

            availableUpgrades.Add(upgrade);
        }

        return availableUpgrades;
    }

    public void RequestBuildUnit(string unitId)
    {
        if (building == null)
            building = GetComponent<Building>();

        if (building == null)
            return;

        if (!building.BelongsToLocalPlayer())
            return;

        RequestBuildUnitServerRpc(unitId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBuildUnitServerRpc(string unitId, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (!ServerValidateOwner(senderClientId))
            return;

        BuildableUnit buildableUnit = FindBuildableUnit(unitId);

        if (buildableUnit == null)
        {
            Debug.LogWarning($"{gameObject.name} ne može da pravi unit: {unitId}");
            return;
        }

        if (PlayerEconomyManager.Instance == null)
            return;

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
            ItemType = BuildQueueItemNet.TypeUnit,
            UnitId = new FixedString64Bytes(buildableUnit.UnitId),
            DisplayName = new FixedString64Bytes(buildableUnit.DisplayName),
            MineralCost = buildableUnit.MineralCost,
            PowerUpkeep = buildableUnit.PowerUpkeep,
            BuildTime = buildableUnit.BuildTime,
            RemainingTime = buildableUnit.BuildTime
        });
    }

    public void RequestBuildUpgrade(string upgradeId)
    {
        if (building == null)
            building = GetComponent<Building>();

        if (building == null)
            return;

        if (!building.BelongsToLocalPlayer())
            return;

        RequestBuildUpgradeServerRpc(upgradeId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBuildUpgradeServerRpc(string upgradeId, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (!ServerValidateOwner(senderClientId))
            return;

        if (IsQueueFull())
        {
            Debug.LogWarning($"{gameObject.name} build queue je pun.");
            return;
        }

        BuildableUpgrade upgrade = FindBuildableUpgrade(upgradeId);

        if (upgrade == null)
        {
            Debug.LogWarning($"{gameObject.name} ne može da pravi upgrade: {upgradeId}");
            return;
        }

        if (ServerHasCompletedUpgrade(senderClientId, upgrade.UpgradeId))
        {
            Debug.LogWarning($"Upgrade je već završen: {upgrade.DisplayName}");
            return;
        }

        if (ServerHasUpgradeQueued(upgrade.UpgradeId))
        {
            Debug.LogWarning($"Upgrade je već u queue-u: {upgrade.DisplayName}");
            return;
        }

        if (PlayerEconomyManager.Instance == null)
            return;

        if (!PlayerEconomyManager.Instance.TryGetPlayerState(senderClientId, out PlayerGameData economyData))
            return;

        if (economyData.TechTier < upgrade.RequiredTechTier)
        {
            Debug.LogWarning($"Nemaš potreban tech tier za {upgrade.DisplayName}.");
            return;
        }

        if (!ServerHasRequiredCompletedUpgrades(senderClientId, upgrade))
        {
            Debug.LogWarning($"Nisu ispunjeni prerequisite upgradeovi za {upgrade.DisplayName}.");
            return;
        }

        if (!PlayerEconomyManager.Instance.CanAfford(
                senderClientId,
                upgrade.MineralCost,
                upgrade.RequiredFreePower))
        {
            Debug.LogWarning($"Nemaš resurse za {upgrade.DisplayName}.");
            return;
        }

        if (!PlayerEconomyManager.Instance.TrySpendResourcesAndReservePower(
        senderClientId,
        upgrade.MineralCost,
        upgrade.RequiredFreePower))
        {
            return;
        }

        BuildQueue.Add(new BuildQueueItemNet
        {
            ItemType = BuildQueueItemNet.TypeUpgrade,
            UnitId = upgrade.UpgradeId,
            DisplayName = upgrade.DisplayName,
            MineralCost = upgrade.MineralCost,
            PowerUpkeep = upgrade.RequiredFreePower,
            BuildTime = upgrade.ResearchTime,
            RemainingTime = upgrade.ResearchTime
        });
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

        if (!ServerValidateOwner(senderClientId))
            return;

        if (index < 0 || index >= BuildQueue.Count)
            return;

        BuildQueueItemNet item = BuildQueue[index];

        int refund = CalculateCancelRefund(item);

        if (PlayerEconomyManager.Instance != null && refund > 0)
            PlayerEconomyManager.Instance.AddMinerals(senderClientId, refund);

        if (item.ItemType == BuildQueueItemNet.TypeUpgrade && item.PowerUpkeep > 0)
        {
            PlayerEconomyManager.Instance.AddPowerUsed(senderClientId, -item.PowerUpkeep);
        }

        BuildQueue.RemoveAt(index);
    }

    private int CalculateCancelRefund(BuildQueueItemNet item)
    {
        if (item.BuildTime <= 0.01f)
            return item.MineralCost;

        float remainingPercent = Mathf.Clamp01(item.RemainingTime / item.BuildTime);

        return Mathf.RoundToInt(item.MineralCost * remainingPercent);
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

        BuildQueue.RemoveAt(0);

        if (currentItem.ItemType == BuildQueueItemNet.TypeUnit)
        {
            if (PlayerEconomyManager.Instance != null)
            {
                if (!PlayerEconomyManager.Instance.CanAfford(ownerClientId, 0, currentItem.PowerUpkeep))
                {
                    currentItem.RemainingTime = 0.1f;
                    BuildQueue.Insert(0, currentItem);
                    return;
                }
            }

            SpawnCompletedUnit(currentItem.UnitId.ToString(), currentItem.PowerUpkeep);
            return;
        }

        if (currentItem.ItemType == BuildQueueItemNet.TypeUpgrade)
        {
            if (currentItem.PowerUpkeep > 0)
                PlayerEconomyManager.Instance.AddPowerUsed(ownerClientId, -currentItem.PowerUpkeep);

            CompleteUpgrade(ownerClientId, currentItem.UnitId.ToString());
            return;
        }
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

    private void CompleteUpgrade(ulong ownerClientId, string upgradeId)
    {
        BuildableUpgrade upgrade = FindBuildableUpgrade(upgradeId);

        if (upgrade == null)
        {
            Debug.LogError($"Ne mogu da završim upgrade: {upgradeId}");
            return;
        }

        ServerMarkUpgradeCompleted(ownerClientId, upgrade.UpgradeId);

        if (upgrade.SetTechTierOnComplete > 0 &&
            PlayerEconomyManager.Instance != null)
        {
            if (PlayerEconomyManager.Instance.TryGetPlayerState(ownerClientId, out PlayerGameData economyData))
            {
                if (economyData.TechTier < upgrade.SetTechTierOnComplete)
                {
                    PlayerEconomyManager.Instance.SetTechTier(ownerClientId, upgrade.SetTechTierOnComplete);
                }
            }
        }

        Debug.Log($"Completed upgrade: {upgrade.DisplayName}");
    }

    private bool ServerValidateOwner(ulong senderClientId)
    {
        if (building == null)
            building = GetComponent<Building>();

        if (building == null)
            return false;

        return building.PlayerClientId.Value == senderClientId;
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

    private BuildableUpgrade FindBuildableUpgrade(string upgradeId)
    {
        if (data == null)
            data = GetComponent<BuildingData>();

        if (data == null || data.BuildableUpgrades == null)
            return null;

        foreach (BuildableUpgrade upgrade in data.BuildableUpgrades)
        {
            if (upgrade.UpgradeId == upgradeId)
                return upgrade;
        }

        return null;
    }

    public Sprite GetIconForQueueItem(BuildQueueItemNet item)
    {
        if (item.ItemType == BuildQueueItemNet.TypeUnit)
        {
            BuildableUnit unit = FindBuildableUnit(item.UnitId.ToString());
            return unit != null ? unit.Icon : null;
        }

        if (item.ItemType == BuildQueueItemNet.TypeUpgrade)
        {
            BuildableUpgrade upgrade = FindBuildableUpgrade(item.UnitId.ToString());
            return upgrade != null ? upgrade.Icon : null;
        }

        return null;
    }

    private bool ServerHasRequiredCompletedUpgrades(ulong ownerClientId, BuildableUpgrade upgrade)
    {
        if (upgrade.RequiredCompletedUpgrades == null || upgrade.RequiredCompletedUpgrades.Count == 0)
            return true;

        foreach (string requiredUpgradeId in upgrade.RequiredCompletedUpgrades)
        {
            if (string.IsNullOrWhiteSpace(requiredUpgradeId))
                continue;

            if (!ServerHasCompletedUpgrade(ownerClientId, requiredUpgradeId))
                return false;
        }

        return true;
    }

    private bool ServerHasCompletedUpgrade(ulong ownerClientId, string upgradeId)
    {
        if (completedUpgradesByPlayer.TryGetValue(ownerClientId, out HashSet<string> completed))
            return completed.Contains(upgradeId);

        return false;
    }

    private void ServerMarkUpgradeCompleted(ulong ownerClientId, string upgradeId)
    {
        if (!completedUpgradesByPlayer.TryGetValue(ownerClientId, out HashSet<string> completed))
        {
            completed = new HashSet<string>();
            completedUpgradesByPlayer[ownerClientId] = completed;
        }

        completed.Add(upgradeId);
    }

    private bool ServerHasUpgradeQueued(string upgradeId)
    {
        for (int i = 0; i < BuildQueue.Count; i++)
        {
            BuildQueueItemNet item = BuildQueue[i];

            if (item.ItemType != BuildQueueItemNet.TypeUpgrade)
                continue;

            if (item.UnitId.ToString() == upgradeId)
                return true;
        }

        return false;
    }

    private bool IsQueueFull()
    {
        return BuildQueue != null && BuildQueue.Count >= maxQueueSize;
    }

    private bool IsUpgradeQueued(string upgradeId)
    {
        for (int i = 0; i < BuildQueue.Count; i++)
        {
            BuildQueueItemNet item = BuildQueue[i];

            if (item.ItemType != BuildQueueItemNet.TypeUpgrade)
                continue;

            if (string.Equals(
                    item.UnitId.ToString(),
                    upgradeId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public bool CanStartUpgradeLocally(BuildableUpgrade upgrade)
    {
        if (upgrade == null)
            return false;

        if (PlayerEconomyManager.Instance == null)
            return false;

        if (NetworkManager.Singleton == null)
            return false;

        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (IsUpgradeCompleted(localClientId, upgrade))
            return false;

        if (IsUpgradeQueued(upgrade.UpgradeId))
            return false;

        return PlayerEconomyManager.Instance.CanResearchUpgrade(localClientId, upgrade);
    }

    private bool IsUpgradeCompleted(ulong clientId, BuildableUpgrade upgrade)
    {
        if (upgrade == null)
            return true;

        if (upgrade.SetTechTierOnComplete <= 0)
            return false;

        if (PlayerEconomyManager.Instance == null)
            return false;

        if (!PlayerEconomyManager.Instance.TryGetPlayerState(clientId, out PlayerGameData data))
            return false;

        return data.TechTier >= upgrade.SetTechTierOnComplete;
    }
}