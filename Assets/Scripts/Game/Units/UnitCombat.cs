using Unity.Netcode;
using UnityEngine;

public class UnitCombat : NetworkBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
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

    public void RequestAttack(Unit targetUnit)
    {
        if (targetUnit == null)
            return;

        if (unit == null)
            unit = GetComponent<Unit>();

        if (!unit.BelongsToLocalPlayer())
            return;

        NetworkObjectReference targetRef = new NetworkObjectReference(targetUnit.NetworkObject);
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

        Unit targetUnit = targetObject.GetComponent<Unit>();

        if (targetUnit == null)
            return;

        if (targetUnit.PlayerClientId.Value == senderClientId)
            return;

        currentTarget = targetObject;

        Debug.Log($"Unit {name} got attack target: {targetUnit.name}");
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

        Unit targetUnit = currentTarget.GetComponent<Unit>();

        if (targetUnit == null || targetUnit.Health.Value <= 0f)
        {
            currentTarget = null;

            if (movement != null)
                movement.ServerClearCombatLookTarget();

            return;
        }

        Weapon weapon = data.Weapons[0];

        float distance = Vector3.Distance(transform.position, targetUnit.transform.position);

        if (distance > weapon.Range)
            return;

        Transform aimTransform = GetAimTransform();

        Vector3 direction = targetUnit.transform.position - aimTransform.position;
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
                movement.ServerSetCombatLookTarget(targetUnit.transform.position);
        }

        float angle = Vector3.Angle(aimTransform.forward, normalizedDirection);

        if (angle > weapon.FiringArc)
            return;

        if (Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + 1f / weapon.FireRate;

        FireProjectile(targetUnit, weapon);
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

        Unit bestTarget = null;
        float bestDistanceSqr = weapon.Range * weapon.Range;

        foreach (GameObject unitObj in UnitManager.instance.AllUnitsList)
        {
            if (unitObj == null || unitObj == gameObject)
                continue;

            Unit possibleTarget = unitObj.GetComponent<Unit>();

            if (possibleTarget == null)
                continue;

            if (possibleTarget.Health.Value <= 0f)
                continue;

            if (possibleTarget.PlayerClientId.Value == unit.PlayerClientId.Value)
                continue;

            float distanceSqr = (possibleTarget.transform.position - transform.position).sqrMagnitude;

            if (distanceSqr <= bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestTarget = possibleTarget;
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget.NetworkObject;
        }
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

    private void FireProjectile(Unit targetUnit, Weapon weapon)
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("Projectile prefab is missing on UnitCombat.");
            return;
        }

        Transform barrel = fallbackBarrel != null ? fallbackBarrel : transform;

        GameObject projectileObj = Instantiate(
            projectilePrefab,
            barrel.position,
            barrel.rotation
        );

        Projectile projectile = projectileObj.GetComponent<Projectile>();
        NetworkObject projectileNetObj = projectileObj.GetComponent<NetworkObject>();

        projectileNetObj.Spawn();

        float finalDamage = weapon.Damage + data.DamageBonus;

        projectile.Setup(
            unit.NetworkObject,
            targetUnit.NetworkObject,
            unit.PlayerClientId.Value,
            finalDamage,
            35f
        );
    }
}