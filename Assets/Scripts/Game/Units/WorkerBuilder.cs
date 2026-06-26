using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class WorkerBuilder : NetworkBehaviour
{
    private enum WorkerBuildMode
    {
        None = 0,
        BuildingConstruction = 1,
        RepairingBuilding = 2
    }

    [Header("Build Options")]
    [SerializeField] private List<BuildableBuilding> buildableBuildings = new List<BuildableBuilding>();

    [Header("Worker Build")]
    [SerializeField] private float buildRange = 4f;
    [SerializeField] private float buildWorkPerSecond = 1f;
    [SerializeField] private float repairHealthPerSecond = 35f;
    [SerializeField] private float repathInterval = 0.25f;
    [SerializeField] private float faceTargetSpeed = 720f;

    [Header("Build/Repair Line")]
    [SerializeField] private LineRenderer buildRepairLine;
    [SerializeField] private Transform lineOrigin;
    [SerializeField] private Color buildRepairLineColor = Color.yellow;
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private float linePulseAmount = 0.04f;
    [SerializeField] private float linePulseSpeed = 10f;
    [SerializeField] private float lineTargetHeight = 1.5f;

    private Unit unit;
    private UnitMovement unitMovement;
    private WorkerGathering workerGathering;

    private WorkerBuildMode currentMode = WorkerBuildMode.None;
    private ConstructionSite currentConstructionSite;
    private Building currentRepairBuilding;

    private float repathTimer;

    private NetworkVariable<int> visualWorkMode = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<NetworkObjectReference> visualTargetReference = new NetworkVariable<NetworkObjectReference>(
        default(NetworkObjectReference),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public IReadOnlyList<BuildableBuilding> BuildableBuildings => buildableBuildings;

    private void Awake()
    {
        unit = GetComponent<Unit>();
        unitMovement = GetComponent<UnitMovement>();
        workerGathering = GetComponent<WorkerGathering>();

        if (buildRepairLine != null)
        {
            buildRepairLine.enabled = false;
            buildRepairLine.positionCount = 2;
        }
    }

    private void Update()
    {
        if (IsServer)
            ServerUpdateCurrentWork();

        UpdateBuildRepairLine();
    }

    public bool BelongsToLocalPlayer()
    {
        if (NetworkManager.Singleton == null || unit == null)
            return false;

        return unit.PlayerClientId.Value == NetworkManager.Singleton.LocalClientId;
    }

    public bool CanBuild(BuildableBuilding buildableBuilding)
    {
        if (buildableBuilding == null)
            return false;

        if (!BelongsToLocalPlayer())
            return false;

        if (PlayerEconomyManager.Instance == null)
            return false;

        ulong clientId = unit.PlayerClientId.Value;

        if (!PlayerEconomyManager.Instance.TryGetPlayerState(clientId, out PlayerGameData gameData))
            return false;

        if (gameData.TechTier < buildableBuilding.RequiredTechTier)
            return false;

        if (gameData.Minerals < buildableBuilding.MineralCost)
            return false;

        if (buildableBuilding.RequiredFreePower > 0 && gameData.PowerAvailable < buildableBuilding.RequiredFreePower)
            return false;

        return true;
    }

    public BuildableBuilding FindBuildableBuilding(string buildingId)
    {
        for (int i = 0; i < buildableBuildings.Count; i++)
        {
            BuildableBuilding buildableBuilding = buildableBuildings[i];

            if (buildableBuilding == null)
                continue;

            if (string.Equals(buildableBuilding.BuildingId, buildingId, StringComparison.OrdinalIgnoreCase))
                return buildableBuilding;
        }

        return null;
    }

    public void RequestPlaceBuilding(
    string buildingId,
    Vector3 position,
    Quaternion rotation,
    NetworkObjectReference[] selectedWorkerReferences)
    {
        if (!BelongsToLocalPlayer())
            return;

        RequestPlaceBuildingServerRpc(
            new FixedString64Bytes(buildingId),
            position,
            rotation,
            selectedWorkerReferences
        );
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceBuildingServerRpc(
     FixedString64Bytes buildingId,
     Vector3 position,
     Quaternion rotation,
     NetworkObjectReference[] selectedWorkerReferences,
     ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (!ServerIsCommandFromOwner(senderClientId))
            return;

        if (BuildingPlacementSystem.Instance == null)
            return;

        BuildingPlacementSystem.Instance.ServerTryPlaceBuilding(
            this,
            senderClientId,
            buildingId.ToString(),
            position,
            rotation,
            selectedWorkerReferences
        );
    }

    public void RequestBuildConstructionSite(ConstructionSite constructionSite)
    {
        if (!BelongsToLocalPlayer())
            return;

        if (constructionSite == null)
            return;

        if (workerGathering != null)
            workerGathering.RequestCancelGathering();

        NetworkObject networkObject = constructionSite.GetComponent<NetworkObject>();

        if (networkObject == null)
            return;

        RequestBuildConstructionSiteServerRpc(new NetworkObjectReference(networkObject));
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestBuildConstructionSiteServerRpc(
        NetworkObjectReference constructionSiteReference,
        ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (!ServerIsCommandFromOwner(senderClientId))
            return;

        if (!constructionSiteReference.TryGet(out NetworkObject siteNetworkObject))
            return;

        ConstructionSite constructionSite = siteNetworkObject.GetComponent<ConstructionSite>();

        if (constructionSite == null)
            return;

        ServerStartBuilding(constructionSite);
    }

    public void RequestRepairBuilding(Building building)
    {
        if (!BelongsToLocalPlayer())
            return;

        if (building == null)
            return;

        if (workerGathering != null)
            workerGathering.RequestCancelGathering();

        NetworkObject networkObject = building.GetComponent<NetworkObject>();

        if (networkObject == null)
            return;

        RequestRepairBuildingServerRpc(new NetworkObjectReference(networkObject));
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRepairBuildingServerRpc(
        NetworkObjectReference buildingReference,
        ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (!ServerIsCommandFromOwner(senderClientId))
            return;

        if (!buildingReference.TryGet(out NetworkObject buildingNetworkObject))
            return;

        Building building = buildingNetworkObject.GetComponent<Building>();

        if (building == null)
            return;

        ServerStartRepairing(building);
    }

    public void RequestCancelWorkerWork()
    {
        if (!BelongsToLocalPlayer())
            return;

        RequestCancelWorkerWorkServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCancelWorkerWorkServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (!ServerIsCommandFromOwner(senderClientId))
            return;

        ServerCancelWorkerWork();
    }

    public void ServerStartBuilding(ConstructionSite constructionSite)
    {
        if (!IsServer)
            return;

        if (constructionSite == null || unit == null)
            return;

        Building siteBuilding = constructionSite.GetComponent<Building>();

        if (siteBuilding == null)
            return;

        if (siteBuilding.PlayerClientId.Value != unit.PlayerClientId.Value)
            return;

        if (siteBuilding.Health.Value <= 0f)
            return;

        currentConstructionSite = constructionSite;
        currentRepairBuilding = null;
        currentMode = WorkerBuildMode.BuildingConstruction;
        repathTimer = 0f;

        ServerSetWorkVisual(WorkerBuildMode.BuildingConstruction, constructionSite.GetComponent<NetworkObject>());
        ServerMoveTowards(constructionSite.transform, constructionSite.GetComponent<Collider>());
    }

    public void ServerStartRepairing(Building building)
    {
        if (!IsServer)
            return;

        if (building == null || unit == null)
            return;

        if (building.GetComponent<ConstructionSite>() != null)
            return;

        if (building.PlayerClientId.Value != unit.PlayerClientId.Value)
            return;

        if (building.Health.Value <= 0f)
            return;

        if (building.Health.Value >= building.MaxHealth.Value)
            return;

        currentRepairBuilding = building;
        currentConstructionSite = null;
        currentMode = WorkerBuildMode.RepairingBuilding;
        repathTimer = 0f;

        ServerSetWorkVisual(WorkerBuildMode.RepairingBuilding, building.GetComponent<NetworkObject>());
        ServerMoveTowards(building.transform, building.GetComponent<Collider>());
    }

    public void ServerCancelWorkerWork()
    {
        if (!IsServer)
            return;

        currentMode = WorkerBuildMode.None;
        currentConstructionSite = null;
        currentRepairBuilding = null;
        repathTimer = 0f;

        ServerSetWorkVisual(WorkerBuildMode.None, null);
    }

    private void ServerUpdateCurrentWork()
    {
        switch (currentMode)
        {
            case WorkerBuildMode.BuildingConstruction:
                ServerUpdateConstructionWork();
                break;

            case WorkerBuildMode.RepairingBuilding:
                ServerUpdateRepairWork();
                break;
        }
    }

    private void ServerUpdateConstructionWork()
    {
        if (currentConstructionSite == null || unit == null)
        {
            ServerCancelWorkerWork();
            return;
        }

        Building siteBuilding = currentConstructionSite.GetComponent<Building>();

        if (siteBuilding == null || siteBuilding.Health.Value <= 0f)
        {
            ServerCancelWorkerWork();
            return;
        }

        if (siteBuilding.PlayerClientId.Value != unit.PlayerClientId.Value)
        {
            ServerCancelWorkerWork();
            return;
        }

        if (currentConstructionSite.BuildProgress.Value >= currentConstructionSite.RequiredBuildWork.Value)
        {
            ServerCancelWorkerWork();
            return;
        }

        Collider targetCollider = currentConstructionSite.GetComponent<Collider>();
        float distance = GetDistanceToTarget(currentConstructionSite.transform, targetCollider);

        if (distance > buildRange)
        {
            ServerMoveTowards(currentConstructionSite.transform, targetCollider);
            return;
        }

        if (unitMovement != null)
            unitMovement.ServerStopMovementOnly();

        ServerFaceTarget(GetClosestTargetPoint(currentConstructionSite.transform, targetCollider));

        currentConstructionSite.ServerAddBuildWork(
            unit.PlayerClientId.Value,
            buildWorkPerSecond * Time.deltaTime
        );
    }

    private void ServerUpdateRepairWork()
    {
        if (currentRepairBuilding == null || unit == null)
        {
            ServerCancelWorkerWork();
            return;
        }

        if (currentRepairBuilding.Health.Value <= 0f)
        {
            ServerCancelWorkerWork();
            return;
        }

        if (currentRepairBuilding.PlayerClientId.Value != unit.PlayerClientId.Value)
        {
            ServerCancelWorkerWork();
            return;
        }

        if (currentRepairBuilding.Health.Value >= currentRepairBuilding.MaxHealth.Value)
        {
            ServerCancelWorkerWork();
            return;
        }

        Collider targetCollider = currentRepairBuilding.GetComponent<Collider>();
        float distance = GetDistanceToTarget(currentRepairBuilding.transform, targetCollider);

        if (distance > buildRange)
        {
            ServerMoveTowards(currentRepairBuilding.transform, targetCollider);
            return;
        }

        if (unitMovement != null)
            unitMovement.ServerStopMovementOnly();

        Vector3 targetPoint = GetClosestTargetPoint(currentRepairBuilding.transform, targetCollider);
        ServerFaceTarget(targetPoint);

        currentRepairBuilding.Health.Value = Mathf.Min(
            currentRepairBuilding.Health.Value + repairHealthPerSecond * Time.deltaTime,
            currentRepairBuilding.MaxHealth.Value
        );
    }

    private void ServerMoveTowards(Transform targetTransform, Collider targetCollider)
    {
        if (unitMovement == null || targetTransform == null)
            return;

        repathTimer -= Time.deltaTime;

        if (repathTimer > 0f)
            return;

        repathTimer = repathInterval;

        Vector3 targetPoint = GetClosestTargetPoint(targetTransform, targetCollider);
        unitMovement.ServerMoveToGatherRange(targetPoint, buildRange * 0.8f);
    }

    private void ServerFaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            faceTargetSpeed * Time.deltaTime
        );
    }

    private void ServerSetWorkVisual(WorkerBuildMode mode, NetworkObject targetNetworkObject)
    {
        visualWorkMode.Value = (int)mode;

        if (targetNetworkObject == null)
            visualTargetReference.Value = default(NetworkObjectReference);
        else
            visualTargetReference.Value = new NetworkObjectReference(targetNetworkObject);
    }

    private void UpdateBuildRepairLine()
    {
        if (buildRepairLine == null)
            return;

        WorkerBuildMode mode = (WorkerBuildMode)visualWorkMode.Value;

        if (mode == WorkerBuildMode.None)
        {
            buildRepairLine.enabled = false;
            return;
        }

        if (!visualTargetReference.Value.TryGet(out NetworkObject targetNetworkObject))
        {
            buildRepairLine.enabled = false;
            return;
        }

        Vector3 origin = lineOrigin != null
            ? lineOrigin.position
            : transform.position + Vector3.up * 1.2f;

        Vector3 target = targetNetworkObject.transform.position + Vector3.up * lineTargetHeight;

        if (FogOfWar.Instance != null)
        {
            if (!FogOfWar.Instance.IsVisibleNow(origin) ||
                !FogOfWar.Instance.IsVisibleNow(target))
            {
                buildRepairLine.enabled = false;
                return;
            }
        }

        Collider targetCollider = targetNetworkObject.GetComponent<Collider>();
        float distance = GetDistanceToTarget(targetNetworkObject.transform, targetCollider);

        if (distance > buildRange + 0.5f)
        {
            buildRepairLine.enabled = false;
            return;
        }

        float pulse = Mathf.Sin(Time.time * linePulseSpeed) * linePulseAmount;
        float width = Mathf.Max(0.01f, lineWidth + pulse);

        buildRepairLine.enabled = true;
        buildRepairLine.positionCount = 2;
        buildRepairLine.SetPosition(0, origin);
        buildRepairLine.SetPosition(1, target);
        buildRepairLine.startWidth = width;
        buildRepairLine.endWidth = width;
        buildRepairLine.startColor = buildRepairLineColor;
        buildRepairLine.endColor = buildRepairLineColor;
    }

    private float GetDistanceToTarget(Transform targetTransform, Collider targetCollider)
    {
        if (targetTransform == null)
            return float.MaxValue;

        Vector3 targetPoint = GetClosestTargetPoint(targetTransform, targetCollider);
        return Vector3.Distance(transform.position, targetPoint);
    }

    private Vector3 GetClosestTargetPoint(Transform targetTransform, Collider targetCollider)
    {
        if (targetCollider != null)
            return targetCollider.ClosestPoint(transform.position);

        return targetTransform.position;
    }

    private bool ServerIsCommandFromOwner(ulong senderClientId)
    {
        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null)
            return false;

        return unit.PlayerClientId.Value == senderClientId;
    }
}