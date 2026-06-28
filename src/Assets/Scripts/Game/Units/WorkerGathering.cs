using Unity.Netcode;
using UnityEngine;

public class WorkerGathering : NetworkBehaviour
{
    [Header("Gathering")]
    [SerializeField] private float gatherRange = 4f;
    [SerializeField] private float mineralsPerSecond = 10f;
    [SerializeField] private float gatherTickInterval = 2f;

    [Header("Movement")]
    [SerializeField] private float repathInterval = 0.25f;

    [Header("Laser Visual")]
    [SerializeField] private LineRenderer siphonLaser;
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private Color laserColor = Color.cyan;
    [SerializeField] private float laserWidth = 0.08f;

    [Header("Upgrade Multipliers")]
    [SerializeField] private float gatherRateMultiplier = 1f;
    [SerializeField] private float gatherRangeMultiplier = 1f;

    [Header("Auto Gather")]
    [SerializeField] private bool autoFindNextCrystal = true;
    [SerializeField] private float autoFindRadiusFallback = 40f;

    [Header("Laser Pulse")]
    [SerializeField] private Color laserColorA = Color.cyan;
    [SerializeField] private Color laserColorB = Color.white;
    [SerializeField] private float laserMinWidth = 0.04f;
    [SerializeField] private float laserMaxWidth = 0.12f;
    [SerializeField] private float laserPulseSpeed = 8f;

    private Unit unit;
    private UnitMovement unitMovement;
    private UnitCombat unitCombat;

    private MineralCrystal currentCrystal;

    private bool hasGatherOrder;
    private bool stoppedForGathering;

    private float gatherTimer;
    private float repathTimer;

    private NetworkVariable<bool> laserActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<Vector3> laserEndPosition = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        unit = GetComponent<Unit>();
        unitMovement = GetComponent<UnitMovement>();
        unitCombat = GetComponent<UnitCombat>();

        SetupLaser();
    }

    private void Update()
    {
        UpdateLaserVisual();

        if (!IsServer)
            return;

        ServerUpdateGathering();
    }

    public void RequestGather(MineralCrystal crystal)
    {
        if (crystal == null)
            return;

        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null)
            return;

        if (!unit.BelongsToLocalPlayer())
            return;

        NetworkObject crystalNetworkObject = crystal.GetComponent<NetworkObject>();

        if (crystalNetworkObject == null)
            return;

        RequestGatherServerRpc(crystalNetworkObject);
    }

    public void RequestCancelGathering()
    {
        if (!IsSpawned)
            return;

        RequestCancelGatheringServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestGatherServerRpc(NetworkObjectReference crystalReference, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null)
            return;

        if (unit.PlayerClientId.Value != senderClientId)
            return;

        if (!crystalReference.TryGet(out NetworkObject crystalNetworkObject))
            return;

        MineralCrystal crystal = crystalNetworkObject.GetComponent<MineralCrystal>();

        if (crystal == null || crystal.IsDepleted)
            return;

        ServerStartGathering(crystal);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCancelGatheringServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null)
            return;

        if (unit.PlayerClientId.Value != senderClientId)
            return;

        ServerCancelGathering();
    }

    private void ServerUpdateGathering()
    {
        if (!hasGatherOrder)
        {
            laserActive.Value = false;
            return;
        }

        if (currentCrystal == null || currentCrystal.IsDepleted)
        {
            ServerTryContinueToNextCrystalOrStop();
            return;
        }

        float distance = Vector3.Distance(transform.position, currentCrystal.transform.position);
        float finalGatherRange = GetFinalGatherRange();

        if (distance > finalGatherRange)
        {
            laserActive.Value = false;
            stoppedForGathering = false;

            repathTimer -= Time.deltaTime;

            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                ServerMoveTowardsCrystal();
            }

            return;
        }

        if (!stoppedForGathering)
        {
            if (unitMovement != null)
                unitMovement.ServerStopMovementOnly();

            stoppedForGathering = true;
        }

        RotateTowardsCrystal();

        laserEndPosition.Value = currentCrystal.SiphonTargetPosition;
        laserActive.Value = true;

        gatherTimer += Time.deltaTime;

        if (gatherTimer < gatherTickInterval)
            return;

        gatherTimer = 0f;

        int amountToGather = Mathf.Max(
            1,
            Mathf.RoundToInt(GetFinalGatherRate() * gatherTickInterval)
        );

        int gatheredAmount = currentCrystal.ServerTakeMinerals(amountToGather);

        if (gatheredAmount <= 0)
        {
            ServerTryContinueToNextCrystalOrStop();
            return;
        }

        if (PlayerEconomyManager.Instance != null)
        {
            PlayerEconomyManager.Instance.AddMinerals(
                unit.PlayerClientId.Value,
                gatheredAmount
            );
        }

        if (currentCrystal == null || currentCrystal.IsDepleted)
        {
            ServerTryContinueToNextCrystalOrStop();
        }
    }

    private void ServerStartGathering(MineralCrystal crystal)
    {
        if (!IsServer)
            return;

        if (crystal == null || crystal.IsDepleted)
            return;

        hasGatherOrder = true;

        currentCrystal = crystal;
        gatherTimer = 0f;
        repathTimer = 0f;
        stoppedForGathering = false;

        laserActive.Value = false;

        if (unitCombat != null)
            unitCombat.ServerClearAttackTarget();

        if (unitMovement != null)
        {
            unitMovement.ServerClearPatrol();
            unitMovement.ServerClearCombatLookTarget();
            unitMovement.ServerClearPlayerMoveCommand();
        }

        ServerMoveTowardsCrystal();
    }

    public void ServerCancelGathering()
    {
        if (!IsServer)
            return;

        hasGatherOrder = false;

        currentCrystal = null;
        gatherTimer = 0f;
        repathTimer = 0f;
        stoppedForGathering = false;

        laserActive.Value = false;
    }

    private void ServerTryContinueToNextCrystalOrStop()
    {
        currentCrystal = null;
        stoppedForGathering = false;
        gatherTimer = 0f;
        repathTimer = 0f;
        laserActive.Value = false;

        if (hasGatherOrder && autoFindNextCrystal && TryFindNextCrystal(out MineralCrystal nextCrystal))
        {
            ServerStartGathering(nextCrystal);
            return;
        }

        ServerCancelGathering();
    }

    private void ServerMoveTowardsCrystal()
    {
        if (currentCrystal == null)
            return;

        if (unitMovement == null)
            unitMovement = GetComponent<UnitMovement>();

        if (unitMovement == null)
            return;

        float finalGatherRange = GetFinalGatherRange();

        unitMovement.ServerMoveToGatherRange(
            currentCrystal.transform.position,
            finalGatherRange * 0.8f
        );
    }

    private void RotateTowardsCrystal()
    {
        if (currentCrystal == null)
            return;

        Vector3 lookDirection = currentCrystal.transform.position - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude <= 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            360f * Time.deltaTime
        );
    }

    private bool TryFindNextCrystal(out MineralCrystal nextCrystal)
    {
        nextCrystal = null;

        float searchRadius = GetAutoFindRadius();
        float bestDistanceSqr = searchRadius * searchRadius;

        foreach (MineralCrystal crystal in MineralCrystal.AllCrystals)
        {
            if (crystal == null || crystal.IsDepleted)
                continue;

            if (crystal == currentCrystal)
                continue;

            float distanceSqr = (crystal.transform.position - transform.position).sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                nextCrystal = crystal;
            }
        }

        return nextCrystal != null;
    }

    private float GetAutoFindRadius()
    {
        UnitData unitData = GetComponent<UnitData>();

        if (unitData != null && unitData.SightRadius > 0f)
            return unitData.SightRadius;

        return autoFindRadiusFallback;
    }

    private float GetFinalGatherRate()
    {
        return mineralsPerSecond * gatherRateMultiplier;
    }

    private float GetFinalGatherRange()
    {
        return gatherRange * gatherRangeMultiplier;
    }

    public void SetGatherRateMultiplier(float multiplier)
    {
        gatherRateMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void SetGatherRangeMultiplier(float multiplier)
    {
        gatherRangeMultiplier = Mathf.Max(0.1f, multiplier);
    }

    private void SetupLaser()
    {
        if (siphonLaser == null)
            siphonLaser = GetComponent<LineRenderer>();

        if (siphonLaser == null)
            return;

        siphonLaser.enabled = false;
        siphonLaser.positionCount = 2;
        siphonLaser.useWorldSpace = true;
        siphonLaser.startWidth = laserWidth;
        siphonLaser.endWidth = laserWidth;
        siphonLaser.startColor = laserColor;
        siphonLaser.endColor = laserColor;
    }

    private void UpdateLaserVisual()
    {
        if (siphonLaser == null)
            return;

        bool shouldShowLaser = laserActive.Value;

        siphonLaser.enabled = shouldShowLaser;

        if (!shouldShowLaser)
            return;

        float pulse = (Mathf.Sin(Time.time * laserPulseSpeed) + 1f) * 0.5f;

        float width = Mathf.Lerp(laserMinWidth, laserMaxWidth, pulse);
        Color color = Color.Lerp(laserColorA, laserColorB, pulse);

        siphonLaser.startWidth = width;
        siphonLaser.endWidth = width * 0.65f;

        siphonLaser.startColor = color;
        siphonLaser.endColor = color;

        Vector3 startPosition = laserOrigin != null
            ? laserOrigin.position
            : transform.position + Vector3.up * 1.2f;

        siphonLaser.SetPosition(0, startPosition);
        siphonLaser.SetPosition(1, laserEndPosition.Value);
    }
}