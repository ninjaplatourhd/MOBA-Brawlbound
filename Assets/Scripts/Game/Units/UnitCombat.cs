using Unity.Netcode;
using UnityEngine;

public class UnitCombat : NetworkBehaviour
{

    [SerializeField] private Transform fallbackBarrel;
    [SerializeField] private float projectileSpeed = 35f;
    [SerializeField] private float autoAggroInterval = 0.5f;
    [SerializeField] private float autoAggroDelayAfterMoveOrder = 1f;

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

        // Ne napadaj svoje.
        if (ownedTarget.OwnerClientId == senderClientId)
            return;

        currentTarget = targetObject;
    }

    private void ServerUpdateAttack()
    {
        if (currentTarget == null)
        {
            TryAutoAcquireTarget();

            if (movement != null)
                movement.ServerClearCombatLookTarget();

            return;
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

        Weapon weapon = data.Weapons[0];

        Vector3 targetPosition = currentTarget.transform.position;
        float distance = Vector3.Distance(transform.position, targetPosition);

        if (distance > weapon.Range)
            return;

        Transform aimTransform = GetAimTransform();

        Vector3 direction = targetPosition - aimTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Vector3 normalizedDirection = direction.normalized;

        if (data.MovesGun)
        {
            // Rotira samo kupolu/gun pivot.
            Quaternion targetRotation = Quaternion.LookRotation(normalizedDirection);

            aimTransform.rotation = Quaternion.RotateTowards(
                aimTransform.rotation,
                targetRotation,
                weapon.RotationSpeed * Time.deltaTime
            );
        }
        else
        {
            // Rotira celo telo tenka preko UnitMovement.
            if (movement != null)
                movement.ServerSetCombatLookTarget(targetPosition);
        }

        float angle = Vector3.Angle(aimTransform.forward, normalizedDirection);

        if (angle > weapon.FiringArc)
            return;

        if (Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + 1f / weapon.FireRate;

        FireProjectile(currentTarget, weapon);
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

        Weapon weapon = data.Weapons[0];

        NetworkObject bestTarget = null;
        float bestDistanceSqr = weapon.Range * weapon.Range;

        // Check enemy units
        if (UnitManager.instance != null)
        {
            foreach (GameObject unitObj in UnitManager.instance.AllUnitsList)
            {
                TryConsiderAutoAggroTarget(unitObj, ref bestTarget, ref bestDistanceSqr);
            }
        }

        // Check enemy buildings
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

        // Ignore friendly targets
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
}