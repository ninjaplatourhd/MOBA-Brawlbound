using System.Collections.Generic;
using UnityEngine;

public static class PlayerRegistry
{
    // ClientId → PlayerData mapping
    public static Dictionary<ulong, PlayerData> Players = new();

    public static void RegisterPlayer(ulong clientId, PlayerData data)
    {
        //  if (!NetworkManager.Singleton.IsServer)
        //    return;

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
        {
            return data.Name;
        }

        return "Unknown";
    }

    public static Color GetPlayerColor(ulong clientId)
    {
        if (Players.TryGetValue(clientId, out var data))
            return PlayerData.PlayerColorFromName(data.Color);

        return Color.black;
    }

    public static PlayerData? GetPlayer(ulong clientId)
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