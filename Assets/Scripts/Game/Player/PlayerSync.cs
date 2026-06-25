using Unity.Netcode;
using UnityEngine;

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

        PlayerRegistry.RegisterPlayer(clientId, new PlayerData
        {
            LobbyPlayerId = lobbyPlayerId,
            Name = name,
            Team = team,
            Color = color
        });

        RegisterPlayerClientRpc(clientId, lobbyPlayerId, name, team, color);

        RefreshExistingColors();
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

        RefreshExistingColors();
    }

    private void RefreshExistingColors()
    {
        Unit[] units = FindObjectsByType<Unit>(FindObjectsSortMode.None);

        foreach (Unit unit in units)
        {
            if (unit != null)
                unit.RefreshPlayerColor();
        }

        Building[] buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);

        foreach (Building building in buildings)
        {
            if (building != null)
                building.RefreshPlayerColor();
        }
    }
}