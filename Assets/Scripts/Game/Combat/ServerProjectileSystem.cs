using System;
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
        public ulong SourceUnitNetworkObjectId;

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

        if (sourceUnit == null || weapon == null)
            return;

        int projectileId = nextProjectileId++;

        ServerProjectile projectile = new ServerProjectile
        {
            Id = projectileId,
            OwnerClientId = sourceUnit.PlayerClientId.Value,
            SourceUnitNetworkObjectId = sourceUnit.NetworkObjectId,

            Position = startPosition,
            Direction = direction.normalized,

            Speed = weapon.ProjectileSpeed,
            Damage = weapon.Damage + sourceUnit.Data.DamageBonus,
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

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Unit hitUnit = hit.collider.GetComponentInParent<Unit>();

            if (hitUnit != null)
            {
                // Ne udaraj samog sebe.
                if (hitUnit.NetworkObjectId == projectile.SourceUnitNetworkObjectId)
                    continue;

                // Za sada ignoriši friendly fire.
                if (hitUnit.PlayerClientId.Value == projectile.OwnerClientId)
                    continue;

                chosenHit = hit;
                return true;
            }

            // Ako nije unit, tretiramo kao terrain/wall/obstacle hit.
            chosenHit = hit;
            return true;
        }

        return false;
    }

    private void HandleProjectileHit(ServerProjectile projectile, RaycastHit hit)
    {
        Vector3 impactPosition = hit.point;

        Unit hitUnit = hit.collider.GetComponentInParent<Unit>();

        if (hitUnit != null)
        {
            Unit attackerUnit = null;

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    projectile.SourceUnitNetworkObjectId,
                    out NetworkObject sourceObject))
            {
                attackerUnit = sourceObject.GetComponent<Unit>();
            }

            hitUnit.Damage(projectile.Damage, attackerUnit);
        }

        ProjectileImpactClientRpc(projectile.Id, impactPosition);
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
            visual.Impact(impactPosition);
            clientVisuals.Remove(projectileId);
        }

        // Kasnije ovde možeš spawnovati explosion visual.
        // Instantiate(explosionPrefab, impactPosition, Quaternion.identity);
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