using Unity.Netcode;
using UnityEngine;

public class MineralCrystal : NetworkBehaviour
{
    [Header("Minerals")]
    [SerializeField] private int startingMinerals = 1500;

    [Header("Visual")]
    [SerializeField] private Transform siphonTargetPoint;

    public NetworkVariable<int> RemainingMinerals = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsDepleted => RemainingMinerals.Value <= 0;

    public Vector3 SiphonTargetPosition
    {
        get
        {
            if (siphonTargetPoint != null)
                return siphonTargetPoint.position;

            return transform.position + Vector3.up * 1.5f;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        if (RemainingMinerals.Value <= 0)
            RemainingMinerals.Value = startingMinerals;
    }

    public int ServerTakeMinerals(int requestedAmount)
    {
        if (!IsServer)
            return 0;

        if (requestedAmount <= 0)
            return 0;

        if (RemainingMinerals.Value <= 0)
            return 0;

        int takenAmount = Mathf.Min(requestedAmount, RemainingMinerals.Value);

        RemainingMinerals.Value -= takenAmount;

        if (RemainingMinerals.Value <= 0)
        {
            RemainingMinerals.Value = 0;
            ServerDeplete();
        }

        return takenAmount;
    }

    private void ServerDeplete()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();

        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}