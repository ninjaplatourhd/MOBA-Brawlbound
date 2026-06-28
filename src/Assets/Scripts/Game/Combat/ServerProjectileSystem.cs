using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ServerProjectileSystem : NetworkBehaviour
{
    public static ServerProjectileSystem Instance { get; private set; }

    [SerializeField] private LayerMask projectileHitMask;

    private int nextProjectileId = 1;

    private readonly List<ServerProjectile> activeProjectiles = new List<ServerProjectile>();
    private readonly Dictionary<int, ProjectileVisual> clientVisuals = new Dictionary<int, ProjectileVisual>();

    private class ServerProjectile
    {
        public int Id;

        public ulong OwnerClientId;
        public ulong SourceNetworkObjectId;
        public bool HasSourceNetworkObject;

        public Vector3 Position;
        public Vector3 Direction;

        public float Speed;
        public float Damage;
        public float Radius;
        public float LifeTime;

        public DamageType DamageType;
        public string VisualName;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        ServerUpdateProjectiles();
    }

    public void ServerFireProjectile(
        Unit sourceUnit,
        Vector3 startPosition,
        Vector3 direction,
        Weapon weapon)
    {
        if (!IsServer)
            return;

        if (sourceUnit == null)
            return;

        if (weapon == null)
            return;

        float damageBonus = 0f;

        if (sourceUnit.Data != null)
            damageBonus = sourceUnit.Data.DamageBonus;

        ServerFireProjectileInternal(
            sourceUnit.PlayerClientId.Value,
            sourceUnit.NetworkObjectId,
            true,
            startPosition,
            direction,
            weapon,
            damageBonus
        );
    }

    public void ServerFireProjectileFromBuilding(
        Building sourceBuilding,
        Vector3 startPosition,
        Vector3 direction,
        Weapon weapon)
    {
        if (!IsServer)
            return;

        if (sourceBuilding == null)
            return;

        if (weapon == null)
            return;

        ServerFireProjectileInternal(
            sourceBuilding.PlayerClientId.Value,
            sourceBuilding.NetworkObjectId,
            true,
            startPosition,
            direction,
            weapon,
            0f
        );
    }

    private void ServerFireProjectileInternal(
        ulong ownerClientId,
        ulong sourceNetworkObjectId,
        bool hasSourceNetworkObject,
        Vector3 startPosition,
        Vector3 direction,
        Weapon weapon,
        float damageBonus)
    {
        if (!IsServer)
            return;

        if (weapon == null)
            return;

        if (direction.sqrMagnitude < 0.01f)
            return;

        int projectileId = nextProjectileId++;

        ServerProjectile projectile = new ServerProjectile
        {
            Id = projectileId,

            OwnerClientId = ownerClientId,
            SourceNetworkObjectId = sourceNetworkObjectId,
            HasSourceNetworkObject = hasSourceNetworkObject,

            Position = startPosition,
            Direction = direction.normalized,

            Speed = weapon.ProjectileSpeed,
            Damage = weapon.Damage + damageBonus,
            Radius = weapon.ProjectileRadius,
            LifeTime = weapon.ProjectileLifeTime,

            DamageType = weapon.DamageType,
            VisualName = weapon.ProjectileName
        };

        activeProjectiles.Add(projectile);

        SpawnProjectileVisualClientRpc(
            projectile.Id,
            projectile.VisualName,
            projectile.Position,
            projectile.Direction,
            projectile.Speed
        );
    }

    private void ServerUpdateProjectiles()
    {
        float deltaTime = Time.deltaTime;

        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            ServerProjectile projectile = activeProjectiles[i];

            projectile.LifeTime -= deltaTime;

            if (projectile.LifeTime <= 0f)
            {
                DestroyProjectileVisualClientRpc(projectile.Id);
                activeProjectiles.RemoveAt(i);
                continue;
            }

            Vector3 oldPosition = projectile.Position;
            float travelDistance = projectile.Speed * deltaTime;

            if (TryFindHit(projectile, oldPosition, travelDistance, out RaycastHit hit))
            {
                HandleProjectileHit(projectile, hit);
                activeProjectiles.RemoveAt(i);
                continue;
            }

            projectile.Position += projectile.Direction * travelDistance;
        }
    }

    private bool TryFindHit(
        ServerProjectile projectile,
        Vector3 oldPosition,
        float travelDistance,
        out RaycastHit chosenHit)
    {
        chosenHit = default;

        RaycastHit[] hits = Physics.SphereCastAll(
            oldPosition,
            projectile.Radius,
            projectile.Direction,
            travelDistance,
            projectileHitMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Unit hitUnit = hit.collider.GetComponentInParent<Unit>();

            if (hitUnit != null)
            {
                if (projectile.HasSourceNetworkObject &&
                    hitUnit.NetworkObjectId == projectile.SourceNetworkObjectId)
                {
                    continue;
                }

                if (hitUnit.PlayerClientId.Value == projectile.OwnerClientId)
                    continue;

                chosenHit = hit;
                return true;
            }

            Building hitBuilding = hit.collider.GetComponentInParent<Building>();

            if (hitBuilding != null)
            {
                if (projectile.HasSourceNetworkObject &&
                    hitBuilding.NetworkObjectId == projectile.SourceNetworkObjectId)
                {
                    continue;
                }

                if (hitBuilding.PlayerClientId.Value == projectile.OwnerClientId)
                    continue;

                chosenHit = hit;
                return true;
            }

            chosenHit = hit;
            return true;
        }

        return false;
    }

    private void HandleProjectileHit(ServerProjectile projectile, RaycastHit hit)
    {
        Vector3 impactPosition = hit.point;

        Unit attackerUnit = GetAttackerUnit(projectile);

        Unit hitUnit = hit.collider.GetComponentInParent<Unit>();

        if (hitUnit != null)
        {
            hitUnit.Damage(projectile.Damage, attackerUnit);
            ProjectileImpactClientRpc(projectile.Id, impactPosition);
            return;
        }

        Building hitBuilding = hit.collider.GetComponentInParent<Building>();

        if (hitBuilding != null)
        {
            hitBuilding.Damage(projectile.Damage, attackerUnit);
            ProjectileImpactClientRpc(projectile.Id, impactPosition);
            return;
        }

        ProjectileImpactClientRpc(projectile.Id, impactPosition);
    }

    private Unit GetAttackerUnit(ServerProjectile projectile)
    {
        if (!projectile.HasSourceNetworkObject)
            return null;

        if (NetworkManager.Singleton == null)
            return null;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                projectile.SourceNetworkObjectId,
                out NetworkObject sourceObject))
        {
            return null;
        }

        return sourceObject.GetComponent<Unit>();
    }

    [ClientRpc]
    private void SpawnProjectileVisualClientRpc(
        int projectileId,
        string visualName,
        Vector3 startPosition,
        Vector3 direction,
        float speed)
    {
        GameObject prefab = Resources.Load<GameObject>(
            "Prefabs/Misc/Projectiles/" + visualName
        );

        if (prefab == null)
        {
            Debug.LogError($"Projectile visual prefab not found: {visualName}");
            return;
        }

        GameObject visualObject = Instantiate(
            prefab,
            startPosition,
            Quaternion.LookRotation(direction)
        );

        ProjectileVisual visual = visualObject.GetComponent<ProjectileVisual>();

        if (visual == null)
        {
            Debug.LogError($"{visualName} nema ProjectileVisual script.");
            Destroy(visualObject);
            return;
        }

        visual.Initialize(projectileId, direction, speed);
        clientVisuals[projectileId] = visual;
    }

    [ClientRpc]
    private void ProjectileImpactClientRpc(int projectileId, Vector3 impactPosition)
    {
        if (clientVisuals.TryGetValue(projectileId, out ProjectileVisual visual))
        {
            if (visual != null)
                visual.Impact(impactPosition);

            clientVisuals.Remove(projectileId);
        }

        // TODO dodati particle na hit
    }

    [ClientRpc]
    private void DestroyProjectileVisualClientRpc(int projectileId)
    {
        if (clientVisuals.TryGetValue(projectileId, out ProjectileVisual visual))
        {
            if (visual != null)
                Destroy(visual.gameObject);

            clientVisuals.Remove(projectileId);
        }
    }
}