using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class UnitCombat : NetworkBehaviour
{

    [SerializeField] private Transform fallbackBarrel;
    [SerializeField] private float projectileSpeed = 35f;
    [SerializeField] private float autoAggroInterval = 0.5f;
    [SerializeField] private float autoAggroDelayAfterMoveOrder = 1f;
    [SerializeField] private float attackRangeFactor = 0.9f;
    [SerializeField] private float minimumAttackDistance = 1.5f;

    private bool guardMode;
    private readonly List<float> weaponNextFireTimes = new List<float>();

    private float nextAutoAggroTime;
    private float suppressAutoAggroUntil;
    private Unit unit;
    private UnitData data;
    private NetworkObject currentTarget;
    private float nextFireTime;
    private UnitMovement movement;
    private void Awake()
    {
        unit = GetComponent<Unit>();
        data = GetComponent<UnitData>();
        movement = GetComponent<UnitMovement>();
    }

    private void Update()
    {
        if (!IsServer)
            return;

        ServerUpdateAttack();
    }

    public void ServerClearAttackTarget()
    {
        if (!IsServer)
            return;

        currentTarget = null;
        suppressAutoAggroUntil = Time.time + autoAggroDelayAfterMoveOrder;

        if (movement != null)
            movement.ServerClearCombatLookTarget();
    }

    public void RequestAttack(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        if (unit == null)
            unit = GetComponent<Unit>();

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

        UnitMovement movement = GetComponent<UnitMovement>();
        if (movement != null)
        {
            movement.ServerClearPatrol();
        }

        guardMode = false;
        currentTarget = targetObject;
    }

    private void ServerUpdateAttack()
    {
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

        if (data == null || data.Weapons == null || data.Weapons.Count == 0)
            return;

        IDamageable damageableTarget = currentTarget.GetComponent<IDamageable>();
        IOwnedObject ownedTarget = currentTarget.GetComponent<IOwnedObject>();

        if (damageableTarget == null || ownedTarget == null)
        {
            currentTarget = null;
            return;
        }

        if (IsTargetInvalidOrDead())
        {
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

        // Ako nije Guard, unit sme da chase-uje dok ne uđe u range najkraćeg oružja.
        if (!guardMode && distance > desiredAttackRange)
        {
            if (movement != null)
                movement.ServerMoveToAttackRange(targetPosition, desiredAttackRange);
        }

        // Ako je Guard i target izađe iz max range-a, ne chase-uj.
        if (guardMode && distance > maxWeaponRange)
        {
            currentTarget = null;

            if (movement != null)
                movement.ServerClearCombatLookTarget();

            return;
        }

        // Ako je van range-a svih oružja, nema pucanja još.
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

        float maxWeaponRange = GetMaximumWeaponRange();

        NetworkObject bestTarget = null;
        float bestDistanceSqr = maxWeaponRange * maxWeaponRange;

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

    private Transform GetBarrel()
    {
        if (unit != null && unit.Barrels != null && unit.Barrels.Count > 0)
        {
            return unit.Barrels[0];
        }

        Debug.LogError($"{gameObject.name} nema nijedan barrel podešen u Unit komponenti.");
        return null;
    }

    public void ServerAggroOn(Unit attacker)
    {
        var movement = GetComponent<UnitMovement>();
        if (movement != null)
        {
            movement.ServerClearPatrol();
        }

        if (!IsServer)
            return;

        if (attacker == null)
            return;

        if (attacker.PlayerClientId.Value == unit.PlayerClientId.Value)
            return;

        currentTarget = attacker.NetworkObject;
        suppressAutoAggroUntil = 0f;
    }

    private Transform GetAimTransform()
    {
        if (data != null && data.MovesGun && unit.GunPivot != null)
        {
            return unit.GunPivot;
        }

        return transform;
    }

    private Vector3 GetFlatDirectionTo(Unit targetUnit)
    {
        Vector3 direction = targetUnit.transform.position - GetAimTransform().position;
        direction.y = 0f;
        return direction;
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

        Vector3 aimPoint = targetObject.transform.position + Vector3.up * 1.2f;
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

        if (data.MovesGun)
        {
            Quaternion targetRotation = Quaternion.LookRotation(normalizedDirection);

            aimTransform.rotation = Quaternion.RotateTowards(
                aimTransform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
        else
        {
            if (movement != null)
                movement.ServerSetCombatLookTarget(targetPosition);
        }
    }

    private void TryFireWeapons(float distance, Transform aimTransform, Vector3 normalizedDirection)
    {
        EnsureWeaponCooldownList();

        float angle = Vector3.Angle(aimTransform.forward, normalizedDirection);

        for (int i = 0; i < data.Weapons.Count; i++)
        {
            Weapon weapon = data.Weapons[i];

            if (weapon == null)
                continue;

            if (distance > weapon.Range)
                continue;

            if (angle > weapon.FiringArc)
                continue;

            if (Time.time < weaponNextFireTimes[i])
                continue;

            float fireRate = Mathf.Max(0.01f, weapon.FireRate);
            weaponNextFireTimes[i] = Time.time + 1f / fireRate;

            FireProjectile(currentTarget, weapon);
        }
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
}