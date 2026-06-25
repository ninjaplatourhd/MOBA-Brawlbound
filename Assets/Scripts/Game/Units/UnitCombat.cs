using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class UnitCombat : NetworkBehaviour
{
    [Header("Projectile")]
    [SerializeField] private Transform fallbackBarrel;
    [SerializeField] private float projectileSpeed = 35f;

    [Header("Auto Aggro")]
    [SerializeField] private float autoAggroInterval = 0.5f;
    [SerializeField] private float autoAggroDelayAfterMoveOrder = 1f;

    [SerializeField] private float autoAggroRangeMultiplier = 1.25f;

    [Header("Attack Movement")]
    [SerializeField] private float attackRangeFactor = 0.9f;
    [SerializeField] private float minimumAttackDistance = 1.5f;

    [Header("Turret Rotation")]
    [SerializeField] private float turretYawOffset = 0f;
    [SerializeField] private float turretYawSyncThreshold = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool debugCombat = false;

    private bool guardMode;
    private readonly List<float> weaponNextFireTimes = new List<float>();

    private float nextAutoAggroTime;
    private float suppressAutoAggroUntil;

    private Unit unit;
    private UnitData data;
    private NetworkObject currentTarget;
    private UnitMovement movement;

    private float gunPivotBaseLocalX;
    private float gunPivotBaseLocalZ;

    private NetworkVariable<float> syncedGunYaw = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        unit = GetComponent<Unit>();
        data = GetComponent<UnitData>();
        movement = GetComponent<UnitMovement>();

        CacheGunPivotBaseRotation();
    }

    public override void OnNetworkSpawn()
    {
        CacheGunPivotBaseRotation();

        if (data != null && data.MovesGun && unit != null && unit.GunPivot != null)
        {
            if (IsServer)
            {
                syncedGunYaw.Value = unit.GunPivot.localEulerAngles.y;
            }
            else
            {
                ApplyGunYaw(syncedGunYaw.Value);
            }
        }
    }

    private void Update()
    {
        if (IsServer)
        {
            ServerUpdateAttack();
        }
        else
        {
            ApplySyncedGunRotation();
        }
    }

    private void ApplySyncedGunRotation()
    {
        if (data == null || !data.MovesGun)
            return;

        if (unit == null || unit.GunPivot == null)
            return;

        ApplyGunYaw(syncedGunYaw.Value);
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

    public void RequestAttack(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null)
            return;

        if (!unit.BelongsToLocalPlayer())
            return;

        NetworkObject targetNetObj = targetObject.GetComponent<NetworkObject>();

        if (targetNetObj == null)
            return;

        NetworkObjectReference targetRef = new NetworkObjectReference(targetNetObj);

        SetAttackTargetServerRpc(targetRef);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetAttackTargetServerRpc(NetworkObjectReference targetRef, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (unit.PlayerClientId.Value != senderClientId)
            return;

        if (!targetRef.TryGet(out NetworkObject targetObject))
            return;

        IOwnedObject ownedTarget = targetObject.GetComponent<IOwnedObject>();
        IDamageable damageableTarget = targetObject.GetComponent<IDamageable>();

        if (ownedTarget == null || damageableTarget == null)
            return;

        if (ownedTarget.OwnerClientId == senderClientId)
            return;

        if (movement != null)
        {
            movement.ServerClearPatrol();
            movement.ServerClearCombatLookTarget();
            movement.ServerClearPlayerMoveCommand();
        }

        guardMode = false;
        currentTarget = targetObject;

        DebugCombat($"Manual attack target set: {targetObject.name}");

        NotifyTargetThatItIsBeingAttacked(targetObject);
    }

    private void ServerUpdateAttack()
    {
        if (data == null || data.Weapons == null || data.Weapons.Count == 0)
            return;

        bool playerMoveActive = movement != null && movement.IsExecutingPlayerMoveCommand;


        if (playerMoveActive && !data.MovesGun)
        {
            currentTarget = null;

            if (movement != null)
                movement.ServerClearCombatLookTarget();

            DebugCombat("Skipping combat because non-turret unit is executing player move command.");
            return;
        }

        if (currentTarget == null)
        {
            TryAutoAcquireTarget();

            if (currentTarget == null)
            {
                if (movement != null)
                    movement.ServerClearCombatLookTarget();

                return;
            }
        }

        IDamageable damageableTarget = currentTarget.GetComponent<IDamageable>();
        IOwnedObject ownedTarget = currentTarget.GetComponent<IOwnedObject>();

        if (damageableTarget == null || ownedTarget == null)
        {
            DebugCombat("Current target invalid: missing IDamageable or IOwnedObject.");
            currentTarget = null;
            return;
        }

        if (IsTargetInvalidOrDead())
        {
            DebugCombat("Current target invalid or dead.");
            currentTarget = null;

            if (movement != null)
                movement.ServerClearCombatLookTarget();

            return;
        }

        Vector3 targetPosition = currentTarget.transform.position;
        float distance = Vector3.Distance(transform.position, targetPosition);

        float minWeaponRange = GetMinimumWeaponRange();
        float maxWeaponRange = GetMaximumWeaponRange();

        float desiredAttackRange = Mathf.Max(
            minimumAttackDistance,
            minWeaponRange * attackRangeFactor
        );


        if (playerMoveActive && distance > maxWeaponRange)
        {
            currentTarget = null;

            if (movement != null)
                movement.ServerClearCombatLookTarget();

            DebugCombat("Clearing target because turret unit is moving and target is outside max range.");
            return;
        }

        if (!guardMode && !playerMoveActive && distance > desiredAttackRange)
        {
            if (movement != null)
                movement.ServerMoveToAttackRange(targetPosition, desiredAttackRange);
        }

        if (guardMode && distance > maxWeaponRange)
        {
            currentTarget = null;

            if (movement != null)
                movement.ServerClearCombatLookTarget();

            DebugCombat("Guard mode: target left range.");
            return;
        }

        if (distance > maxWeaponRange)
            return;

        Transform aimTransform = GetAimTransform();

        Vector3 direction = targetPosition - aimTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Vector3 normalizedDirection = direction.normalized;

        RotateTowardsTarget(aimTransform, normalizedDirection, targetPosition);
        TryFireWeapons(distance, aimTransform, normalizedDirection);
    }

    private void TryAutoAcquireTarget()
    {
        if (!IsServer)
            return;

        if (Time.time < suppressAutoAggroUntil)
            return;

        if (Time.time < nextAutoAggroTime)
            return;

        nextAutoAggroTime = Time.time + autoAggroInterval;

        if (data == null || data.Weapons == null || data.Weapons.Count == 0)
            return;

        float autoAggroRange = GetAutoAggroRange();

        if (autoAggroRange <= 0f)
            return;

        NetworkObject bestTarget = null;
        float bestDistanceSqr = autoAggroRange * autoAggroRange;

        if (UnitManager.instance != null)
        {
            foreach (GameObject unitObj in UnitManager.instance.AllUnitsList)
            {
                TryConsiderAutoAggroTarget(unitObj, ref bestTarget, ref bestDistanceSqr);
            }
        }

        if (BuildingManager.instance != null)
        {
            foreach (GameObject buildingObj in BuildingManager.instance.AllBuildingsList)
            {
                TryConsiderAutoAggroTarget(buildingObj, ref bestTarget, ref bestDistanceSqr);
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget;

            DebugCombat($"Auto-acquired target: {bestTarget.name}");

            NotifyTargetThatItIsBeingAttacked(bestTarget);
        }
    }

    private void TryConsiderAutoAggroTarget(
        GameObject targetObj,
        ref NetworkObject bestTarget,
        ref float bestDistanceSqr)
    {
        if (targetObj == null || targetObj == gameObject)
            return;

        NetworkObject targetNetworkObject = targetObj.GetComponent<NetworkObject>();
        IOwnedObject ownedTarget = targetObj.GetComponent<IOwnedObject>();
        IDamageable damageableTarget = targetObj.GetComponent<IDamageable>();

        if (targetNetworkObject == null || ownedTarget == null || damageableTarget == null)
            return;

        if (ownedTarget.OwnerClientId == unit.PlayerClientId.Value)
            return;

        if (IsTargetDead(targetNetworkObject))
            return;

        float distanceSqr = (targetObj.transform.position - transform.position).sqrMagnitude;

        if (distanceSqr <= bestDistanceSqr)
        {
            bestDistanceSqr = distanceSqr;
            bestTarget = targetNetworkObject;
        }
    }

    private void NotifyTargetThatItIsBeingAttacked(NetworkObject targetObject)
    {
        if (!IsServer)
            return;

        if (targetObject == null)
            return;

        if (unit == null)
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
        if (!IsServer)
            return;

        if (attacker == null)
            return;

        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null)
            return;

        if (attacker.PlayerClientId.Value == unit.PlayerClientId.Value)
            return;

        if (currentTarget != null && !IsTargetInvalidOrDead())
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

    private Transform GetBarrel()
    {
        if (unit != null && unit.Barrels != null && unit.Barrels.Count > 0)
        {
            return unit.Barrels[0];
        }

        if (fallbackBarrel != null)
            return fallbackBarrel;

        Debug.LogError($"{gameObject.name} nema nijedan barrel podešen u Unit komponenti.");
        return null;
    }

    private Transform GetAimTransform()
    {
        if (data != null && data.MovesGun && unit != null && unit.GunPivot != null)
        {
            return unit.GunPivot;
        }

        return transform;
    }

    private void FireProjectile(NetworkObject targetObject, Weapon weapon)
    {
        if (!IsServer)
            return;

        if (targetObject == null)
            return;

        Transform barrel = GetBarrel();

        if (barrel == null)
            return;

        Vector3 aimPoint = targetObject.transform.position + Vector3.up * 0.5f;
        Vector3 direction = aimPoint - barrel.position;

        if (direction.sqrMagnitude < 0.01f)
            return;

        ServerProjectileSystem.Instance.ServerFireProjectile(
            unit,
            barrel.position,
            direction.normalized,
            weapon
        );
    }

    private bool IsTargetInvalidOrDead()
    {
        if (currentTarget == null)
            return true;

        if (!currentTarget.IsSpawned)
            return true;

        return IsTargetDead(currentTarget);
    }

    private bool IsTargetDead(NetworkObject target)
    {
        if (target == null)
            return true;

        Unit targetUnit = target.GetComponent<Unit>();
        if (targetUnit != null)
            return targetUnit.Health.Value <= 0f;

        Building targetBuilding = target.GetComponent<Building>();
        if (targetBuilding != null)
            return targetBuilding.Health.Value <= 0f;

        return true;
    }

    public void RequestGuard()
    {
        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null)
            return;

        if (!unit.BelongsToLocalPlayer())
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

        if (guardMode)
        {
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
    }

    public void ServerClearGuardMode()
    {
        if (!IsServer)
            return;

        guardMode = false;
    }

    private void RotateTowardsTarget(Transform aimTransform, Vector3 normalizedDirection, Vector3 targetPosition)
    {
        float rotationSpeed = GetRotationSpeedForAiming();
        float yawOffset = GetPrimaryWeaponYawOffset();

        if (data.MovesGun)
        {
            RotateGunYawOnly(aimTransform, normalizedDirection, rotationSpeed, yawOffset);
        }
        else
        {
            if (movement != null && !movement.IsExecutingPlayerMoveCommand)
                movement.ServerSetCombatLookTarget(targetPosition);
        }
    }

    private void RotateGunYawOnly(Transform gunPivot, Vector3 worldDirection, float rotationSpeed, float weaponYawOffset)
    {
        if (gunPivot == null)
            return;

        float targetYaw = GetTargetLocalYaw(gunPivot, worldDirection, weaponYawOffset);
        float currentYaw = gunPivot.localEulerAngles.y;

        float newYaw = Mathf.MoveTowardsAngle(
            currentYaw,
            targetYaw,
            rotationSpeed * Time.deltaTime
        );

        ApplyGunYaw(newYaw);

        if (IsServer && Mathf.Abs(Mathf.DeltaAngle(syncedGunYaw.Value, newYaw)) > turretYawSyncThreshold)
        {
            syncedGunYaw.Value = newYaw;
        }
    }

    private float GetTargetLocalYaw(Transform gunPivot, Vector3 worldDirection, float weaponYawOffset)
    {
        Transform parent = gunPivot.parent;

        Vector3 localDirection = parent != null
            ? parent.InverseTransformDirection(worldDirection)
            : worldDirection;

        localDirection.y = 0f;

        if (localDirection.sqrMagnitude < 0.01f)
            return gunPivot.localEulerAngles.y;

        float targetYaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        targetYaw += turretYawOffset;
        targetYaw += weaponYawOffset;

        return targetYaw;
    }

    private void ApplyGunYaw(float yaw)
    {
        if (unit == null || unit.GunPivot == null)
            return;

        unit.GunPivot.localRotation = Quaternion.Euler(
            gunPivotBaseLocalX,
            yaw,
            gunPivotBaseLocalZ
        );
    }

    private void CacheGunPivotBaseRotation()
    {
        if (unit == null)
            unit = GetComponent<Unit>();

        if (unit == null || unit.GunPivot == null)
            return;

        Vector3 localEuler = unit.GunPivot.localEulerAngles;

        gunPivotBaseLocalX = localEuler.x;
        gunPivotBaseLocalZ = localEuler.z;
    }

    private void TryFireWeapons(float distance, Transform aimTransform, Vector3 normalizedDirection)
    {
        EnsureWeaponCooldownList();

        for (int i = 0; i < data.Weapons.Count; i++)
        {
            Weapon weapon = data.Weapons[i];

            if (weapon == null)
                continue;

            if (distance > weapon.Range)
                continue;

            float angle = GetAimAngleToTarget(aimTransform, normalizedDirection, weapon);

            if (angle > weapon.FiringArc)
                continue;

            if (Time.time < weaponNextFireTimes[i])
                continue;

            float fireRate = Mathf.Max(0.01f, weapon.FireRate);
            weaponNextFireTimes[i] = Time.time + 1f / fireRate;

            FireProjectile(currentTarget, weapon);
        }
    }

    private float GetAimAngleToTarget(Transform aimTransform, Vector3 normalizedDirection, Weapon weapon)
    {
        float weaponYawOffset = weapon != null ? weapon.WeaponYawOffset : 0f;

        if (data != null && data.MovesGun && unit != null && unit.GunPivot != null)
        {
            float targetYaw = GetTargetLocalYaw(unit.GunPivot, normalizedDirection, weaponYawOffset);
            float currentYaw = unit.GunPivot.localEulerAngles.y;

            return Mathf.Abs(Mathf.DeltaAngle(currentYaw, targetYaw));
        }

        Vector3 forward = aimTransform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.01f)
            return 999f;

        Vector3 effectiveForward = Quaternion.Euler(0f, weaponYawOffset, 0f) * forward.normalized;

        return Vector3.Angle(effectiveForward, normalizedDirection);
    }

    private void EnsureWeaponCooldownList()
    {
        while (weaponNextFireTimes.Count < data.Weapons.Count)
        {
            weaponNextFireTimes.Add(0f);
        }

        while (weaponNextFireTimes.Count > data.Weapons.Count)
        {
            weaponNextFireTimes.RemoveAt(weaponNextFireTimes.Count - 1);
        }
    }

    private float GetAutoAggroRange()
    {
        float maxWeaponRange = GetMaximumWeaponRange();

        if (maxWeaponRange <= 0f)
            return 0f;

        return maxWeaponRange * autoAggroRangeMultiplier;
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

        if (minRange == float.MaxValue)
            return 0f;

        return minRange;
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

    private float GetRotationSpeedForAiming()
    {
        if (data == null || data.Weapons == null || data.Weapons.Count == 0)
            return 120f;

        float slowestRotationSpeed = float.MaxValue;

        foreach (Weapon weapon in data.Weapons)
        {
            if (weapon == null)
                continue;

            slowestRotationSpeed = Mathf.Min(slowestRotationSpeed, weapon.RotationSpeed);
        }

        if (slowestRotationSpeed == float.MaxValue)
            return 120f;

        return slowestRotationSpeed;
    }

    private void DebugCombat(string message)
    {
        if (!debugCombat)
            return;

        Debug.Log($"[{gameObject.name}] {message}");
    }

    private float GetPrimaryWeaponYawOffset()
    {
        if (data == null || data.Weapons == null || data.Weapons.Count == 0)
            return 0f;

        Weapon weapon = data.Weapons[0];

        if (weapon == null)
            return 0f;

        return weapon.WeaponYawOffset;
    }
}