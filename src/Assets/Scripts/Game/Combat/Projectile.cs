using Unity.Netcode;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    public NetworkVariable<ulong> TargetNetworkObjectId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<ulong> SourceNetworkObjectId = new NetworkVariable<ulong>(
    0,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

    public NetworkVariable<ulong> OwnerPlayerClientId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> Damage = new NetworkVariable<float>(
        10f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> Speed = new NetworkVariable<float>(
        35f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [SerializeField] private float hitDistance = 1.2f;
    [SerializeField] private float lifeTime = 5f;

    private float lifeTimer;

    public void Setup(NetworkObject source, NetworkObject target, ulong ownerPlayerClientId, float damage, float speed)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        SourceNetworkObjectId.Value = source.NetworkObjectId;
        TargetNetworkObjectId.Value = target.NetworkObjectId;
        OwnerPlayerClientId.Value = ownerPlayerClientId;
        Damage.Value = damage;
        Speed.Value = speed;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        lifeTimer += Time.deltaTime;

        if (lifeTimer >= lifeTime)
        {
            NetworkObject.Despawn(true);
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                TargetNetworkObjectId.Value,
                out NetworkObject targetNetworkObject))
        {
            NetworkObject.Despawn(true);
            return;
        }

        Unit targetUnit = targetNetworkObject.GetComponent<Unit>();

        if (targetUnit == null || targetUnit.Health.Value <= 0f)
        {
            NetworkObject.Despawn(true);
            return;
        }

        Vector3 targetPosition = targetUnit.transform.position + Vector3.up * 1.2f;
        Vector3 direction = targetPosition - transform.position;

        float distanceThisFrame = Speed.Value * Time.deltaTime;

        if (direction.magnitude <= hitDistance || direction.magnitude <= distanceThisFrame)
        {
            Unit attackerUnit = null;

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    SourceNetworkObjectId.Value,
                    out NetworkObject sourceNetworkObject))
            {
                attackerUnit = sourceNetworkObject.GetComponent<Unit>();
            }

            targetUnit.Damage(Damage.Value, attackerUnit);
            NetworkObject.Despawn(true);
            return;
        }

        transform.position += direction.normalized * distanceThisFrame;
        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }
}