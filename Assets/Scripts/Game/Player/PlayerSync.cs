using Unity.Netcode;
using UnityEngine;

public class PlayerSync : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // server registers host
            PlayerRegistry.RegisterPlayer(
                NetworkManager.Singleton.LocalClientId,
                new PlayerData
                {
                    LobbyPlayerId = "Host",
                    Name = LobbyManager.Instance.PlayerName,
                    Team = LobbyManager.Instance.Team
                });
        }

        if (IsClient && IsOwner)
        {
            RegisterMeServerRpc(
                LobbyManager.Instance.PlayerName,
                LobbyManager.Instance.Team);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterMeServerRpc(string name, string team, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        PlayerRegistry.RegisterPlayer(clientId, new PlayerData
        {
            LobbyPlayerId = clientId.ToString(),
            Name = name,
            Team = team
        });
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            PlayerRegistry.UnregisterPlayer(NetworkManager.Singleton.LocalClientId);
        }
    }
}