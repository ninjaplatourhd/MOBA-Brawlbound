using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class UnitCombat : NetworkBehaviour
{
    private class WeaponRuntime
    {
        public float NextFireTime;
        public int NextBarrelIndex;
        public float BaseLocalX;
        public float BaseLocalZ;
    }

    [Header("Projectile")]
    [SerializeField] private Transform fallbackBarrel;

    [Header("Auto Aggro")]
    [SerializeField] private float autoAggroInterval = 0.5f;
    [SerializeField] private float autoAggroDelayAfterMoveOrder = 1f;
    [SerializeField] private float autoAggroRangeMultiplier = 1.25f;

    [Header("Attack Movement")]
    [SerializeField] private float attackRangeFactor = 0.9f;
    [SerializeField] private float minimumAttackDistance = 1.5f;

    [Header("Weapon Rotation Sync")]
    [SerializeField] private float weaponYawSyncThreshold = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool debugCombat = false;

    private Unit unit;
    private UnitData data;
    private UnitMovement movement;

    private NetworkObject currentTarget;
    private bool guardMode;

    private float nextAutoAggroTime;
    private float suppressAutoAggroUntil;

    private readonly List<WeaponRuntime> weaponRuntimes = new List<WeaponRuntime>();
    private NetworkList<float> syncedWeaponYaws;

    private void Awake()
    {
        unit = GetComponent<Unit>();
        data = GetComponent<UnitData>();
        movement = GetComponent<UnitMovement>();

        syncedWeaponYaws = new NetworkList<float>();
    }

    public override void OnNetworkSpawn()
    {
        EnsureWeaponRuntimes();

        if (IsServer)
            SyncInitialWeaponYaws();
        else
            ApplySyncedWeaponYaws();
    }

    private void OnDestroy()
    {
        if (syncedWeaponYaws != null)
            syncedWeaponYaws.Dispose();
    }

    private void Update()
    {
        EnsureWeaponRuntimes();

        if (IsServer)
            ServerUpdateAttack();
        else
            ApplySyncedWeaponYaws();
    }

    public void RequestAttack(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null || !unit.BelongsToLocalPlayer())
            return;

        NetworkObject targetNetworkObject = targetObject.GetComponent<NetworkObject>();

        if (targetNetworkObject == null)
            return;

        SetAttackTargetServerRpc(new NetworkObjectReference(targetNetworkObject));
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetAttackTargetServerRpc(
        NetworkObjectReference targetReference,
        ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null)
            return;

        if (unit.PlayerClientId.Value != senderClientId)
            return;

        if (!targetReference.TryGet(out NetworkObject targetObject))
            return;

        if (!IsValidEnemyTarget(targetObject, senderClientId))
            return;

        if (!ServerCanOwnerSeeTarget(senderClientId, targetObject))
        {
            DebugCombat("Manual attack rejected because target is not visible.");
            return;
        }

        if (movement != null)
        {
            movement.ServerClearPatrol();
            movement.ServerClearCombatLookTarget();
            movement.ServerClearPlayerMoveCommand();
        }

        guardMode = false;
        currentTarget = targetObject;

        NotifyTargetThatItIsBeingAttacked(targetObject);
        DebugCombat($"Manual attack target set: {targetObject.name}");
    }

    public void ServerClearAttackTarget()
    {
        if (!IsServer)
            return;

        currentTarget = null;
        suppressAutoAggroUntil = Time.time + autoAggroDelayAfterMoveOrder;

        if (movement != null)
            movement.ServerClearCombatLookTarget();

        DebugCombat("Attack target cleared.");
    }

    private void ServerUpdateAttack()
    {
        if (data == null || data.Weapons == null || data.Weapons.Count == 0)
            return;

        bool playerMoveActive = movement != null && movement.IsExecutingPlayerMoveCommand;

        if (playerMoveActive && !HasAnyMovingWeapon())
        {
            ClearCurrentTarget();
            DebugCombat("Skipping combat because unit has no moving weapon while moving.");
            return;
        }

        if (currentTarget == null)
            TryAutoAcquireTarget();

        if (currentTarget == null)
        {
            if (movement != null)
                movement.ServerClearCombatLookTarget();

            return;
        }

        if (!IsTargetUsable(currentTarget))
        {
            ClearCurrentTarget();
            DebugCombat("Current target invalid or dead.");
            return;
        }

        if (!ServerCanOwnerSeeTarget(unit.PlayerClientId.Value, currentTarget))
        {
            ClearCurrentTarget();
            DebugCombat("Current target lost because it is no longer visible.");
            return;
        }

        Vector3 targetPosition = currentTarget.transform.position;
        float distance = Vector3.Distance(transform.position, targetPosition);
        float maxWeaponRange = GetMaximumWeaponRange();

        if (playerMoveActive && distance > maxWeaponRange)
        {
            ClearCurrentTarget();
            DebugCombat("Clearing target because moving unit target is outside max range.");
            return;
        }

        if (guardMode && distance > maxWeaponRange)
        {
            ClearCurrentTarget();
            DebugCombat("Guard mode: target left range.");
            return;
        }

        HandleAttackMovement(targetPosition, distance, playerMoveActive);

        if (distance > maxWeaponRange)
            return;

        AimWeaponsAtTarget(targetPosition, playerMoveActive);
        TryFireWeapons(targetPosition, distance, playerMoveActive);
    }

    private void HandleAttackMovement(Vector3 targetPosition, float distance, bool playerMoveActive)
    {
        if (guardMode || playerMoveActive)
            return;

        float desiredRange = Mathf.Max(
            minimumAttackDistance,
            GetMinimumWeaponRange() * attackRangeFactor
        );

        if (distance > desiredRange && movement != null)
            movement.ServerMoveToAttackRange(targetPosition, desiredRange);
    }

    private void AimWeaponsAtTarget(Vector3 targetPosition, bool playerMoveActive)
    {
        bool hasFixedWeapon = false;

        for (int i = 0; i < data.Weapons.Count; i++)
        {
            Weapon weapon = data.Weapons[i];

            if (weapon == null)
                continue;

            if (weapon.MovesGun)
                AimMovingWeapon(i, weapon, targetPosition);
            else
                hasFixedWeapon = true;
        }

        if (movement == null)
            return;

        if (hasFixedWeapon && !playerMoveActive)
            movement.ServerSetCombatLookTarget(targetPosition);
        else if (!hasFixedWeapon)
            movement.ServerClearCombatLookTarget();
    }

    private void AimMovingWeapon(int weaponIndex, Weapon weapon, Vector3 targetPosition)
    {
        Transform pivot = GetWeaponPivot(weapon);

        if (pivot == null)
            return;

        Vector3 direction = targetPosition - pivot.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        float targetYaw = GetTargetLocalYaw(pivot, direction.normalized, weapon.WeaponYawOffset);
        float currentYaw = pivot.localEulerAngles.y;

        float newYaw = Mathf.MoveTowardsAngle(
            currentYaw,
            targetYaw,
            weapon.RotationSpeed * Time.deltaTime
        );

        ApplyWeaponYaw(weaponIndex, weapon, newYaw);
        SyncWeaponYawIfNeeded(weaponIndex, newYaw);
    }

    private void TryFireWeapons(Vector3 targetPosition, float distance, bool playerMoveActive)
    {
        for (int i = 0; i < data.Weapons.Count; i++)
        {
            Weapon weapon = data.Weapons[i];

            if (weapon == null)
                continue;

            if (playerMoveActive && !weapon.MovesGun)
                continue;

            if (distance > weapon.Range)
                continue;

            if (!IsWeaponInFiringArc(weapon, targetPosition))
                continue;

            WeaponRuntime runtime = weaponRuntimes[i];

            if (Time.time < runtime.NextFireTime)
                continue;

            runtime.NextFireTime = Time.time + 1f / Mathf.Max(0.01f, weapon.FireRate);
            FireWeapon(i, weapon);
        }
    }

    private bool IsWeaponInFiringArc(Weapon weapon, Vector3 targetPosition)
    {
        Vector3 direction = GetDirectionToTarget(weapon, targetPosition);

        if (direction.sqrMagnitude < 0.01f)
            return false;

        float angle;

        if (weapon.MovesGun)
        {
            Transform pivot = GetWeaponPivot(weapon);

            if (pivot == null)
                return false;

            float targetYaw = GetTargetLocalYaw(pivot, direction.normalized, weapon.WeaponYawOffset);
            float currentYaw = pivot.localEulerAngles.y;

            angle = Mathf.Abs(Mathf.DeltaAngle(currentYaw, targetYaw));
        }
        else
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.01f)
                return false;

            Vector3 effectiveForward =
                Quaternion.Euler(0f, weapon.WeaponYawOffset, 0f) *
                forward.normalized;

            angle = Vector3.Angle(effectiveForward, direction.normalized);
        }

        return angle <= weapon.FiringArc;
    }

    private void FireWeapon(int weaponIndex, Weapon weapon)
    {
        if (!IsServer || currentTarget == null)
            return;

        Transform barrel = GetNextBarrel(weaponIndex, weapon);

        if (barrel == null)
            return;

        Vector3 aimPoint = currentTarget.transform.position + Vector3.up * 0.5f;
        Vector3 direction = aimPoint - barrel.position;

        if (direction.sqrMagnitude < 0.01f)
            return;

        if (ServerProjectileSystem.Instance == null)
            return;

        ServerProjectileSystem.Instance.ServerFireProjectile(
            unit,
            barrel.position,
            direction.normalized,
            weapon
        );
    }

    private Transform GetNextBarrel(int weaponIndex, Weapon weapon)
    {
        WeaponRuntime runtime = weaponRuntimes[weaponIndex];

        Transform barrel = GetNextFromList(weapon.Barrels, runtime);

        if (barrel != null)
            return barrel;

        if (unit != null)
            barrel = GetNextFromList(unit.Barrels, runtime);

        if (barrel != null)
            return barrel;

        return fallbackBarrel;
    }

    private Transform GetNextFromList(IReadOnlyList<Transform> barrels, WeaponRuntime runtime)
    {
        if (barrels == null || barrels.Count == 0)
            return null;

        for (int i = 0; i < barrels.Count; i++)
        {
            int index = runtime.NextBarrelIndex % barrels.Count;
            runtime.NextBarrelIndex++;

            if (barrels[index] != null)
                return barrels[index];
        }

        return null;
    }

    private void TryAutoAcquireTarget()
    {
        if (Time.time < suppressAutoAggroUntil)
            return;

        if (Time.time < nextAutoAggroTime)
            return;

        nextAutoAggroTime = Time.time + autoAggroInterval;

        float range = GetAutoAggroRange();

        if (range <= 0f)
            return;

        NetworkObject bestTarget = null;
        float bestDistanceSqr = range * range;

        FindBestTargetInUnits(ref bestTarget, ref bestDistanceSqr);
        FindBestTargetInBuildings(ref bestTarget, ref bestDistanceSqr);

        if (bestTarget == null)
            return;

        currentTarget = bestTarget;
        NotifyTargetThatItIsBeingAttacked(bestTarget);

        DebugCombat($"Auto-acquired target: {bestTarget.name}");
    }

    private void FindBestTargetInUnits(ref NetworkObject bestTarget, ref float bestDistanceSqr)
    {
        if (UnitManager.instance == null)
            return;

        foreach (GameObject unitObject in UnitManager.instance.AllUnitsList)
            TryConsiderAutoAggroTarget(unitObject, ref bestTarget, ref bestDistanceSqr);
    }

    private void FindBestTargetInBuildings(ref NetworkObject bestTarget, ref float bestDistanceSqr)
    {
        if (BuildingManager.instance == null)
            return;

        foreach (GameObject buildingObject in BuildingManager.instance.AllBuildingsList)
            TryConsiderAutoAggroTarget(buildingObject, ref bestTarget, ref bestDistanceSqr);
    }

    private void TryConsiderAutoAggroTarget(
        GameObject targetObject,
        ref NetworkObject bestTarget,
        ref float bestDistanceSqr)
    {
        if (targetObject == null || targetObject == gameObject)
            return;

        NetworkObject targetNetworkObject = targetObject.GetComponent<NetworkObject>();

        if (!IsValidEnemyTarget(targetNetworkObject, unit.PlayerClientId.Value))
            return;

        if (!IsTargetUsable(targetNetworkObject))
            return;

        float distanceSqr = (targetObject.transform.position - transform.position).sqrMagnitude;

        if (distanceSqr > bestDistanceSqr)
            return;

        if (!ServerCanOwnerSeeTarget(unit.PlayerClientId.Value, targetNetworkObject))
            return;

        bestDistanceSqr = distanceSqr;
        bestTarget = targetNetworkObject;
    }

    private bool IsValidEnemyTarget(NetworkObject targetObject, ulong ownerClientId)
    {
        if (targetObject == null)
            return false;

        IOwnedObject ownedTarget = targetObject.GetComponent<IOwnedObject>();
        IDamageable damageableTarget = targetObject.GetComponent<IDamageable>();

        if (ownedTarget == null || damageableTarget == null)
            return false;

        return ownedTarget.OwnerClientId != ownerClientId;
    }

    private bool IsTargetUsable(NetworkObject targetObject)
    {
        if (targetObject == null || !targetObject.IsSpawned)
            return false;

        return !IsTargetDead(targetObject);
    }

    private bool IsTargetDead(NetworkObject targetObject)
    {
        if (targetObject == null)
            return true;

        Unit targetUnit = targetObject.GetComponent<Unit>();

        if (targetUnit != null)
            return targetUnit.Health.Value <= 0f;

        Building targetBuilding = targetObject.GetComponent<Building>();

        if (targetBuilding != null)
            return targetBuilding.Health.Value <= 0f;

        return true;
    }

    private void ClearCurrentTarget()
    {
        currentTarget = null;

        if (movement != null)
            movement.ServerClearCombatLookTarget();
    }

    private void NotifyTargetThatItIsBeingAttacked(NetworkObject targetObject)
    {
        if (!IsServer || targetObject == null || unit == null)
            return;

        Unit targetUnit = targetObject.GetComponent<Unit>();

        if (targetUnit == null)
            return;

        UnitCombat targetCombat = targetUnit.GetComponent<UnitCombat>();

        if (targetCombat == null)
            return;

        float targetAggroRange = targetCombat.GetAutoAggroRange();

        if (targetAggroRange <= 0f)
            return;

        float distanceSqr = (targetUnit.transform.position - transform.position).sqrMagnitude;

        if (distanceSqr > targetAggroRange * targetAggroRange)
            return;

        targetCombat.ServerAggroOn(unit);
    }

    public void ServerAggroOn(Unit attacker)
    {
        if (!IsServer || attacker == null)
            return;

        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null)
            return;

        if (attacker.PlayerClientId.Value == unit.PlayerClientId.Value)
            return;

        if (currentTarget != null && IsTargetUsable(currentTarget))
        {
            DebugCombat($"Ignored aggro from {attacker.name} because current target is still valid.");
            return;
        }

        if (movement != null && movement.IsExecutingPlayerMoveCommand)
        {
            DebugCombat($"Ignored aggro from {attacker.name} because unit is executing player move command.");
            return;
        }

        if (movement != null)
            movement.ServerClearPatrol();

        currentTarget = attacker.NetworkObject;
        suppressAutoAggroUntil = 0f;

        DebugCombat($"Aggroed on attacker: {attacker.name}");
    }

    public void RequestGuard()
    {
        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null || !unit.BelongsToLocalPlayer())
            return;

        SetGuardServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetGuardServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (unit.PlayerClientId.Value != senderClientId)
            return;

        ServerSetGuardMode(true);
    }

    public void ServerSetGuardMode(bool enabled)
    {
        if (!IsServer)
            return;

        guardMode = enabled;

        if (!guardMode)
            return;

        currentTarget = null;
        suppressAutoAggroUntil = 0f;

        if (movement != null)
        {
            movement.ServerClearPatrol();
            movement.ServerClearCombatLookTarget();
            movement.ServerStopMovementOnly();
        }

        DebugCombat("Guard mode enabled.");
    }

    public void ServerClearGuardMode()
    {
        if (!IsServer)
            return;

        guardMode = false;
    }

    private void EnsureWeaponRuntimes()
    {
        if (unit == null)
            unit = GetComponent<Unit>();

        if (data == null)
            data = GetComponent<UnitData>();

        if (data == null || data.Weapons == null)
            return;

        MatchRuntimeCount();

        for (int i = 0; i < data.Weapons.Count; i++)
            CacheWeaponBaseRotation(i);

        if (IsServer)
            MatchSyncedYawCount();
    }

    private void MatchRuntimeCount()
    {
        while (weaponRuntimes.Count < data.Weapons.Count)
            weaponRuntimes.Add(new WeaponRuntime());

        while (weaponRuntimes.Count > data.Weapons.Count)
            weaponRuntimes.RemoveAt(weaponRuntimes.Count - 1);
    }

    private void MatchSyncedYawCount()
    {
        while (syncedWeaponYaws.Count < data.Weapons.Count)
            syncedWeaponYaws.Add(GetCurrentWeaponYaw(syncedWeaponYaws.Count));

        while (syncedWeaponYaws.Count > data.Weapons.Count)
            syncedWeaponYaws.RemoveAt(syncedWeaponYaws.Count - 1);
    }

    private void CacheWeaponBaseRotation(int weaponIndex)
    {
        Weapon weapon = data.Weapons[weaponIndex];
        Transform pivot = GetWeaponPivot(weapon);

        if (pivot == null)
            return;

        weaponRuntimes[weaponIndex].BaseLocalX = pivot.localEulerAngles.x;
        weaponRuntimes[weaponIndex].BaseLocalZ = pivot.localEulerAngles.z;
    }

    private void SyncInitialWeaponYaws()
    {
        if (data == null || data.Weapons == null)
            return;

        MatchSyncedYawCount();

        for (int i = 0; i < data.Weapons.Count; i++)
            syncedWeaponYaws[i] = GetCurrentWeaponYaw(i);
    }

    private void SyncWeaponYawIfNeeded(int weaponIndex, float newYaw)
    {
        if (!IsServer)
            return;

        if (weaponIndex < 0 || weaponIndex >= syncedWeaponYaws.Count)
            return;

        float oldYaw = syncedWeaponYaws[weaponIndex];

        if (Mathf.Abs(Mathf.DeltaAngle(oldYaw, newYaw)) <= weaponYawSyncThreshold)
            return;

        syncedWeaponYaws[weaponIndex] = newYaw;
    }

    private void ApplySyncedWeaponYaws()
    {
        if (data == null || data.Weapons == null)
            return;

        int count = Mathf.Min(data.Weapons.Count, syncedWeaponYaws.Count);

        for (int i = 0; i < count; i++)
        {
            Weapon weapon = data.Weapons[i];

            if (weapon == null || !weapon.MovesGun)
                continue;

            ApplyWeaponYaw(i, weapon, syncedWeaponYaws[i]);
        }
    }

    private Transform GetWeaponPivot(Weapon weapon)
    {
        if (weapon == null || !weapon.MovesGun)
            return null;

        if (weapon.GunPivot != null)
            return weapon.GunPivot;

        return unit != null ? unit.GunPivot : null;
    }

    private Vector3 GetDirectionToTarget(Weapon weapon, Vector3 targetPosition)
    {
        Transform pivot = GetWeaponPivot(weapon);
        Vector3 origin = pivot != null ? pivot.position : transform.position;

        Vector3 direction = targetPosition - origin;
        direction.y = 0f;

        return direction;
    }

    private float GetTargetLocalYaw(Transform pivot, Vector3 worldDirection, float weaponYawOffset)
    {
        Transform parent = pivot.parent;

        Vector3 localDirection = parent != null
            ? parent.InverseTransformDirection(worldDirection)
            : worldDirection;

        localDirection.y = 0f;

        if (localDirection.sqrMagnitude < 0.01f)
            return pivot.localEulerAngles.y;

        return Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg
               + weaponYawOffset;
    }

    private void ApplyWeaponYaw(int weaponIndex, Weapon weapon, float yaw)
    {
        Transform pivot = GetWeaponPivot(weapon);

        if (pivot == null)
            return;

        if (weaponIndex < 0 || weaponIndex >= weaponRuntimes.Count)
            return;

        WeaponRuntime runtime = weaponRuntimes[weaponIndex];

        pivot.localRotation = Quaternion.Euler(
            runtime.BaseLocalX,
            yaw,
            runtime.BaseLocalZ
        );
    }

    private float GetCurrentWeaponYaw(int weaponIndex)
    {
        if (data == null || data.Weapons == null)
            return 0f;

        if (weaponIndex < 0 || weaponIndex >= data.Weapons.Count)
            return 0f;

        Transform pivot = GetWeaponPivot(data.Weapons[weaponIndex]);

        if (pivot == null)
            return 0f;

        return pivot.localEulerAngles.y;
    }

    private bool HasAnyMovingWeapon()
    {
        if (data == null || data.Weapons == null)
            return false;

        foreach (Weapon weapon in data.Weapons)
        {
            if (weapon != null && weapon.MovesGun)
                return true;
        }

        return false;
    }

    private float GetAutoAggroRange()
    {
        float maxRange = GetMaximumWeaponRange();

        if (maxRange <= 0f)
            return 0f;

        return maxRange * autoAggroRangeMultiplier;
    }

    private float GetMinimumWeaponRange()
    {
        if (data == null || data.Weapons == null || data.Weapons.Count == 0)
            return 0f;

        float minRange = float.MaxValue;

        foreach (Weapon weapon in data.Weapons)
        {
            if (weapon == null)
                continue;

            minRange = Mathf.Min(minRange, weapon.Range);
        }

        return minRange == float.MaxValue ? 0f : minRange;
    }

    private float GetMaximumWeaponRange()
    {
        if (data == null || data.Weapons == null || data.Weapons.Count == 0)
            return 0f;

        float maxRange = 0f;

        foreach (Weapon weapon in data.Weapons)
        {
            if (weapon == null)
                continue;

            maxRange = Mathf.Max(maxRange, weapon.Range);
        }

        return maxRange;
    }

    private bool ServerCanOwnerSeeTarget(ulong ownerClientId, NetworkObject targetObject)
    {
        if (!IsServer || targetObject == null || !targetObject.IsSpawned)
            return false;

        Collider targetCollider = targetObject.GetComponent<Collider>();
        Vector3 targetPosition = targetObject.transform.position;

        return CanAnyFriendlyUnitSeeTarget(ownerClientId, targetCollider, targetPosition) ||
               CanAnyFriendlyBuildingSeeTarget(ownerClientId, targetCollider, targetPosition);
    }

    private bool CanAnyFriendlyUnitSeeTarget(
        ulong ownerClientId,
        Collider targetCollider,
        Vector3 targetPosition)
    {
        if (UnitManager.instance == null)
            return false;

        foreach (GameObject unitObject in UnitManager.instance.AllUnitsList)
        {
            Unit friendlyUnit = unitObject != null ? unitObject.GetComponent<Unit>() : null;

            if (friendlyUnit == null)
                continue;

            if (friendlyUnit.PlayerClientId.Value != ownerClientId)
                continue;

            if (friendlyUnit.Health.Value <= 0f)
                continue;

            UnitData friendlyData = friendlyUnit.Data;

            if (friendlyData == null)
                friendlyData = friendlyUnit.GetComponent<UnitData>();

            if (friendlyData == null || friendlyData.SightRadius <= 0f)
                continue;

            Vector3 closestPoint = targetCollider != null
                ? targetCollider.ClosestPoint(friendlyUnit.transform.position)
                : targetPosition;

            float distanceSqr = (closestPoint - friendlyUnit.transform.position).sqrMagnitude;
            float sightSqr = friendlyData.SightRadius * friendlyData.SightRadius;

            if (distanceSqr <= sightSqr)
                return true;
        }

        return false;
    }

    private bool CanAnyFriendlyBuildingSeeTarget(
        ulong ownerClientId,
        Collider targetCollider,
        Vector3 targetPosition)
    {
        if (BuildingManager.instance == null)
            return false;

        foreach (GameObject buildingObject in BuildingManager.instance.AllBuildingsList)
        {
            Building friendlyBuilding = buildingObject != null
                ? buildingObject.GetComponent<Building>()
                : null;

            if (friendlyBuilding == null)
                continue;

            if (friendlyBuilding.PlayerClientId.Value != ownerClientId)
                continue;

            if (friendlyBuilding.Health.Value <= 0f)
                continue;

            BuildingData friendlyData = friendlyBuilding.Data;

            if (friendlyData == null)
                friendlyData = friendlyBuilding.GetComponent<BuildingData>();

            if (friendlyData == null || friendlyData.SightRadius <= 0f)
                continue;

            Vector3 closestPoint = targetCollider != null
                ? targetCollider.ClosestPoint(friendlyBuilding.transform.position)
                : targetPosition;

            float distanceSqr = (closestPoint - friendlyBuilding.transform.position).sqrMagnitude;
            float sightSqr = friendlyData.SightRadius * friendlyData.SightRadius;

            if (distanceSqr <= sightSqr)
                return true;
        }

        return false;
    }

    private void DebugCombat(string message)
    {
        if (!debugCombat)
            return;

        Debug.Log($"[{gameObject.name}] {message}");
    }
}