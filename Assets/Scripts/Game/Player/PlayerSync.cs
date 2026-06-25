using Unity.Netcode;

public class PlayerSync : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            RegisterPlayerOnServer(
                NetworkManager.Singleton.LocalClientId,
                "Host",
                LobbyManager.Instance.PlayerName,
                LobbyManager.Instance.Team,
                LobbyManager.Instance.Color
            );
        }

        if (IsClient && IsOwner)
        {
            RegisterMeServerRpc(
                LobbyManager.Instance.PlayerName,
                LobbyManager.Instance.Team,
                LobbyManager.Instance.Color
            );
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterMeServerRpc(
        string name,
        string team,
        string color,
        ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        RegisterPlayerOnServer(
            clientId,
            clientId.ToString(),
            name,
            team,
            color
        );
    }

    private void RegisterPlayerOnServer(
        ulong clientId,
        string lobbyPlayerId,
        string name,
        string team,
        string color)
    {
        if (!IsServer)
            return;

        PlayerData data = new PlayerData
        {
            LobbyPlayerId = lobbyPlayerId,
            Name = name,
            Team = team,
            Color = color
        };

        PlayerRegistry.RegisterPlayer(clientId, data);

        RegisterPlayerClientRpc(clientId, lobbyPlayerId, name, team, color);
    }

    [ClientRpc]
    private void RegisterPlayerClientRpc(
        ulong clientId,
        string lobbyPlayerId,
        string name,
        string team,
        string color)
    {
        PlayerRegistry.RegisterPlayer(clientId, new PlayerData
        {
            LobbyPlayerId = lobbyPlayerId,
            Name = name,
            Team = team,
            Color = color
        });
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            ulong clientId = OwnerClientId;
            PlayerRegistry.UnregisterPlayer(clientId);
            UnregisterPlayerClientRpc(clientId);
        }
    }

    [ClientRpc]
    private void UnregisterPlayerClientRpc(ulong clientId)
    {
        PlayerRegistry.UnregisterPlayer(clientId);
    }
}