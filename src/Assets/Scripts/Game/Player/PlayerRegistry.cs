using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public static class PlayerRegistry
{
    public static Dictionary<ulong, PlayerData> Players = new();

    public static void RegisterPlayer(ulong clientId, PlayerData data)
    {
        Players[clientId] = data;
    }

    public static void UnregisterPlayer(ulong clientId)
    {
        if (Players.ContainsKey(clientId))
            Players.Remove(clientId);
    }

    public static string GetPlayerName(ulong clientId)
    {
        if (Players.TryGetValue(clientId, out var data))
            return data.Name;

        return "Unknown";
    }

    public static Color GetPlayerColor(ulong clientId)
    {
        if (Players.TryGetValue(clientId, out var data) &&
            !string.IsNullOrWhiteSpace(data.Color))
        {
            return PlayerData.PlayerColorFromName(data.Color);
        }

        if (NetworkManager.Singleton != null &&
            LobbyManager.Instance != null &&
            clientId == NetworkManager.Singleton.LocalClientId)
        {
            return PlayerData.PlayerColorFromName(LobbyManager.Instance.Color);
        }

        return Color.white;
    }

    public static PlayerData GetPlayer(ulong clientId)
    {
        if (Players.TryGetValue(clientId, out var data))
            return data;

        return null;
    }

    public static void Clear()
    {
        Players.Clear();
    }
}