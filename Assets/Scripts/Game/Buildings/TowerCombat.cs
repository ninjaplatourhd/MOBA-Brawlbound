using Unity.Netcode;
using UnityEngine;

public class TowerCombat : NetworkBehaviour
{
    [Header("Weapon")]
    [SerializeField] private Weapon weapon;

    [Header("Rotation Parts")]
    [SerializeField] private Transform yawPivot;      // Kupola
    [SerializeField] private Transform pitchPivot;    // topni_drzac

    [Header("Yaw")]
    [SerializeField] private float yawOffset = 180f;

    [Header("Pitch")]
    [SerializeField] private float pitchRestX = 90f;
    [SerializeField] private float minPitchX = 30f;
    [SerializeField] private float maxPitchX = 150f;
    [SerializeField] private bool invertPitch = false;

    [Header("Targeting")]
    [SerializeField] private float scanInterval = 0.25f;
    [SerializeField] private float targetAimHeight = 0.8f;

    [Header("Fallback")]
    [SerializeField] private Transform fallbackBarrel;

    [Header("Network Sync")]
    [SerializeField] private float rotationSyncThreshold = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool debugTower = false;

    private Building building;
    private NetworkObject currentTarget;

    private bool hasManualTarget;

    private float nextScanTime;
    private float nextFireTime;
    private int nextBarrelIndex;

    private float yawBaseX;
    private float yawBaseZ;

    private float pitchBaseY;
    private float pitchBaseZ;

    private NetworkVariable<float> syncedYaw = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> syncedPitch = new NetworkVariable<float>(
        90f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        building = GetComponent<Building>();
        CacheBaseRotations();
    }

    public override void OnNetworkSpawn()
    {
        CacheBaseRotations();

        if (IsServer)
        {
            if (yawPivot != null)
                syncedYaw.Value = yawPivot.localEulerAngles.y;

            if (pitchPivot != null)
                syncedPitch.Value = pitchPivot.localEulerAngles.x;
        }
        else
        {
            ApplyYaw(syncedYaw.Value);
            ApplyPitch(syncedPitch.Value);
        }
    }

    private void Update()
    {
        if (IsServer)
            ServerUpdateTower();
        else
            ApplySyncedRotation();
    }

    private void ServerUpdateTower()
    {
        if (!CanFight())
        {
            currentTarget = null;
            ReturnPitchToRest();
            return;
        }

        if (currentTarget == null || !IsTargetValid(currentTarget))
        {
            currentTarget = null;
            hasManualTarget = false;
        }

        if (currentTarget == null && !hasManualTarget)
            TryFindTarget();

        if (currentTarget == null)
        {
            ReturnPitchToRest();
            return;
        }

        Vector3 aimPoint = GetAimPoint(currentTarget);

        RotateYawTowards(aimPoint);
        RotatePitchTowards(aimPoint);

        if (!IsAimedAtTarget(aimPoint))
            return;

        if (Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + 1f / Mathf.Max(0.01f, weapon.FireRate);

        FireAtTarget(aimPoint);
    }

    private bool CanFight()
    {
        if (building == null)
            building = GetComponent<Building>();

        if (building == null)
            return false;

        if (building.Health.Value <= 0f)
            return false;

        if (weapon == null)
            return false;

        if (weapon.Range <= 0f)
            return false;

        if (yawPivot == null)
            return false;

        if (pitchPivot == null)
            return false;

        return true;
    }

    private void TryFindTarget()
    {
        if (Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + scanInterval;

        NetworkObject bestTarget = null;
        float bestDistanceSqr = weapon.Range * weapon.Range;

        if (UnitManager.instance != null)
        {
            foreach (GameObject unitObject in UnitManager.instance.AllUnitsList)
                TryConsiderTarget(unitObject, ref bestTarget, ref bestDistanceSqr);
        }

        if (BuildingManager.instance != null)
        {
            foreach (GameObject buildingObject in BuildingManager.instance.AllBuildingsList)
                TryConsiderTarget(buildingObject, ref bestTarget, ref bestDistanceSqr);
        }

        currentTarget = bestTarget;

        if (currentTarget != null)
            DebugTower($"Target acquired: {currentTarget.name}");
    }

    private void TryConsiderTarget(
        GameObject targetObject,
        ref NetworkObject bestTarget,
        ref float bestDistanceSqr)
    {
        if (targetObject == null || targetObject == gameObject)
            return;

        NetworkObject targetNetworkObject = targetObject.GetComponent<NetworkObject>();

        if (!IsTargetValid(targetNetworkObject))
            return;

        float distanceSqr = (targetObject.transform.position - transform.position).sqrMagnitude;

        if (distanceSqr > bestDistanceSqr)
            return;

        bestDistanceSqr = distanceSqr;
        bestTarget = targetNetworkObject;
    }

    private bool IsTargetValid(NetworkObject targetObject)
    {
        if (targetObject == null || !targetObject.IsSpawned)
            return false;

        if (building == null)
            return false;

        IOwnedObject ownedTarget = targetObject.GetComponent<IOwnedObject>();
        IDamageable damageableTarget = targetObject.GetComponent<IDamageable>();

        if (ownedTarget == null || damageableTarget == null)
            return false;

        if (ownedTarget.OwnerClientId == building.PlayerClientId.Value)
            return false;

        if (IsTargetDead(targetObject))
            return false;

        float distance = Vector3.Distance(transform.position, targetObject.transform.position);

        return distance <= weapon.Range;
    }

    private bool IsTargetDead(NetworkObject targetObject)
    {
        Unit targetUnit = targetObject.GetComponent<Unit>();

        if (targetUnit != null)
            return targetUnit.Health.Value <= 0f;

        Building targetBuilding = targetObject.GetComponent<Building>();

        if (targetBuilding != null)
            return targetBuilding.Health.Value <= 0f;

        return true;
    }

    private void RotateYawTowards(Vector3 aimPoint)
    {
        float targetYaw = GetTargetYaw(aimPoint);
        float currentYaw = yawPivot.localEulerAngles.y;

        float newYaw = Mathf.MoveTowardsAngle(
            currentYaw,
            targetYaw,
            weapon.RotationSpeed * Time.deltaTime
        );

        ApplyYaw(newYaw);
        SyncYawIfNeeded(newYaw);
    }

    private void RotatePitchTowards(Vector3 aimPoint)
    {
        float targetPitch = GetTargetPitch(aimPoint);
        float currentPitch = pitchPivot.localEulerAngles.x;

        float newPitch = Mathf.MoveTowardsAngle(
            currentPitch,
            targetPitch,
            weapon.RotationSpeed * Time.deltaTime
        );

        ApplyPitch(newPitch);
        SyncPitchIfNeeded(newPitch);
    }

    private void ReturnPitchToRest()
    {
        if (pitchPivot == null || weapon == null)
            return;

        float currentPitch = pitchPivot.localEulerAngles.x;

        float newPitch = Mathf.MoveTowardsAngle(
            currentPitch,
            pitchRestX,
            weapon.RotationSpeed * Time.deltaTime
        );

        ApplyPitch(newPitch);
        SyncPitchIfNeeded(newPitch);
    }

    private float GetTargetYaw(Vector3 aimPoint)
    {
        Vector3 direction = aimPoint - yawPivot.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return yawPivot.localEulerAngles.y;

        Vector3 worldDirection = direction.normalized;

        Transform parent = yawPivot.parent;

        Vector3 localDirection = parent != null
            ? parent.InverseTransformDirection(worldDirection)
            : worldDirection;

        localDirection.y = 0f;

        if (localDirection.sqrMagnitude < 0.01f)
            return yawPivot.localEulerAngles.y;

        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        yaw += yawOffset;

        return yaw;
    }

    private float GetTargetPitch(Vector3 aimPoint)
    {
        Vector3 direction = aimPoint - pitchPivot.position;

        if (direction.sqrMagnitude < 0.01f)
            return pitchPivot.localEulerAngles.x;

        float horizontalDistance = new Vector2(direction.x, direction.z).magnitude;
        float verticalDifference = direction.y;

        float pitchAngle = Mathf.Atan2(verticalDifference, horizontalDistance) * Mathf.Rad2Deg;

        float targetPitch = invertPitch
            ? pitchRestX + pitchAngle
            : pitchRestX - pitchAngle;

        return Mathf.Clamp(targetPitch, minPitchX, maxPitchX);
    }

    private bool IsAimedAtTarget(Vector3 aimPoint)
    {
        float targetYaw = GetTargetYaw(aimPoint);
        float currentYaw = yawPivot.localEulerAngles.y;

        float targetPitch = GetTargetPitch(aimPoint);
        float currentPitch = pitchPivot.localEulerAngles.x;

        float yawDifference = Mathf.Abs(Mathf.DeltaAngle(currentYaw, targetYaw));
        float pitchDifference = Mathf.Abs(Mathf.DeltaAngle(currentPitch, targetPitch));

        bool aimed = yawDifference <= weapon.FiringArc &&
                     pitchDifference <= weapon.FiringArc;

        if (!aimed)
        {
            DebugTower(
                $"Not aimed. Yaw diff: {yawDifference:F1}, Pitch diff: {pitchDifference:F1}"
            );
        }

        return aimed;
    }

    private void FireAtTarget(Vector3 aimPoint)
    {
        Transform barrel = GetNextBarrel();

        if (barrel == null)
        {
            DebugTower("No barrel found.");
            return;
        }

        Vector3 direction = aimPoint - barrel.position;

        if (direction.sqrMagnitude < 0.01f)
            return;

        if (ServerProjectileSystem.Instance == null)
        {
            DebugTower("ServerProjectileSystem.Instance is null.");
            return;
        }

        ServerProjectileSystem.Instance.ServerFireProjectileFromBuilding(building, barrel.position, direction.normalized, weapon);

        DebugTower($"Fired from {barrel.name}");
    }

    private Transform GetNextBarrel()
    {
        if (weapon != null && weapon.Barrels != null && weapon.Barrels.Count > 0)
        {
            for (int i = 0; i < weapon.Barrels.Count; i++)
            {
                int index = nextBarrelIndex % weapon.Barrels.Count;
                nextBarrelIndex++;

                Transform barrel = weapon.Barrels[index];

                if (barrel != null)
                    return barrel;
            }
        }

        return fallbackBarrel;
    }

    private Vector3 GetAimPoint(NetworkObject targetObject)
    {
        Collider targetCollider = targetObject.GetComponentInChildren<Collider>();

        if (targetCollider != null)
        {
            Vector3 point = targetCollider.bounds.center;
            point.y += targetAimHeight;
            return point;
        }

        return targetObject.transform.position + Vector3.up * targetAimHeight;
    }

    private void ApplySyncedRotation()
    {
        ApplyYaw(syncedYaw.Value);
        ApplyPitch(syncedPitch.Value);
    }

    private void ApplyYaw(float yaw)
    {
        if (yawPivot == null)
            return;

        yawPivot.localRotation = Quaternion.Euler(
            yawBaseX,
            yaw,
            yawBaseZ
        );
    }

    private void ApplyPitch(float pitch)
    {
        if (pitchPivot == null)
            return;

        pitchPivot.localRotation = Quaternion.Euler(
            pitch,
            pitchBaseY,
            pitchBaseZ
        );
    }

    private void SyncYawIfNeeded(float newYaw)
    {
        if (!IsServer)
            return;

        if (Mathf.Abs(Mathf.DeltaAngle(syncedYaw.Value, newYaw)) <= rotationSyncThreshold)
            return;

        syncedYaw.Value = newYaw;
    }

    private void SyncPitchIfNeeded(float newPitch)
    {
        if (!IsServer)
            return;

        if (Mathf.Abs(Mathf.DeltaAngle(syncedPitch.Value, newPitch)) <= rotationSyncThreshold)
            return;

        syncedPitch.Value = newPitch;
    }

    private void CacheBaseRotations()
    {
        if (yawPivot != null)
        {
            Vector3 yawEuler = yawPivot.localEulerAngles;

            yawBaseX = yawEuler.x;
            yawBaseZ = yawEuler.z;
        }

        if (pitchPivot != null)
        {
            Vector3 pitchEuler = pitchPivot.localEulerAngles;

            pitchBaseY = pitchEuler.y;
            pitchBaseZ = pitchEuler.z;
        }
    }

    private void DebugTower(string message)
    {
        if (!debugTower)
            return;

        Debug.Log($"[{gameObject.name}] {message}");
    }

    public void RequestAttackTarget(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        if (building == null)
            building = GetComponent<Building>();

        if (building == null || !building.BelongsToLocalPlayer())
            return;

        NetworkObject targetNetworkObject = targetObject.GetComponent<NetworkObject>();

        if (targetNetworkObject == null)
            return;

        RequestAttackTargetServerRpc(new NetworkObjectReference(targetNetworkObject));
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestAttackTargetServerRpc(
        NetworkObjectReference targetReference,
        ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (building == null)
            building = GetComponent<Building>();

        if (building == null)
            return;

        if (building.PlayerClientId.Value != senderClientId)
            return;

        if (!targetReference.TryGet(out NetworkObject targetObject))
            return;

        if (!IsTargetValid(targetObject))
            return;

        currentTarget = targetObject;
        hasManualTarget = true;

        DebugTower($"Manual tower target set: {targetObject.name}");
    }
}