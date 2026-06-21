using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

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

    private Unit unit;
    private UnitMovement unitMovement;
    private UnitCombat unitCombat;
    private NavMeshAgent agent;

    private MineralCrystal currentCrystal;

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
        agent = GetComponent<NavMeshAgent>();

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

        currentCrystal = crystal;
        gatherTimer = 0f;
        repathTimer = 0f;

        if (unitCombat != null)
            unitCombat.ServerClearAttackTarget();

        if (unitMovement != null)
        {
            unitMovement.ServerClearCombatLookTarget();
            unitMovement.ServerClearPlayerMoveCommand();
        }

        laserActive.Value = false;

        ServerMoveTowardsCrystal();
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
        if (currentCrystal == null || currentCrystal.IsDepleted)
        {
            ServerCancelGathering();
            return;
        }

        float distance = Vector3.Distance(transform.position, currentCrystal.transform.position);
        float finalGatherRange = GetFinalGatherRange();

        if (distance > finalGatherRange)
        {
            laserActive.Value = false;

            repathTimer -= Time.deltaTime;

            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                ServerMoveTowardsCrystal();
            }

            return;
        }

        if (agent != null)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        Vector3 lookDirection = currentCrystal.transform.position - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                360f * Time.deltaTime
            );
        }

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
            ServerCancelGathering();
            return;
        }

        if (PlayerEconomyManager.Instance != null)
        {
            PlayerEconomyManager.Instance.AddMinerals(
                unit.PlayerClientId.Value,
                gatheredAmount
            );
        }
    }

    private void ServerMoveTowardsCrystal()
    {
        if (currentCrystal == null)
            return;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent == null)
            return;

        Vector3 targetPosition = currentCrystal.transform.position;

        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 6f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    private void ServerCancelGathering()
    {
        currentCrystal = null;
        gatherTimer = 0f;
        repathTimer = 0f;
        laserActive.Value = false;
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

        Vector3 startPosition = laserOrigin != null
            ? laserOrigin.position
            : transform.position + Vector3.up * 1.2f;

        siphonLaser.SetPosition(0, startPosition);
        siphonLaser.SetPosition(1, laserEndPosition.Value);
    }
}