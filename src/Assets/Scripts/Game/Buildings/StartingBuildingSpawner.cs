using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class StartingBuildingSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject commandCenterPrefab;
    [SerializeField] private MapData mapdata;

    private readonly HashSet<ulong> spawnedForClients = new HashSet<ulong>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        Debug.Log("StartingBuildingSpawner spawned on server.");

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnCommandCenterForClient(clientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer)
            return;

        Debug.Log($"Client connected after spawner existed: {clientId}");

        SpawnCommandCenterForClient(clientId);
    }

    private void SpawnCommandCenterForClient(ulong clientId)
    {
        if (spawnedForClients.Contains(clientId))
            return;

        if (commandCenterPrefab == null)
        {
            Debug.LogError("CommandCenter prefab nije podešen na StartingBuildingSpawner.");
            return;
        }

        Vector3 position = GetStartPosition(clientId);

        GameObject obj = Instantiate(commandCenterPrefab, position, Quaternion.identity);

        Building building = obj.GetComponent<Building>();
        NetworkObject netObj = obj.GetComponent<NetworkObject>();

        if (building == null || netObj == null)
        {
            Debug.LogError("CommandCenter prefab mora imati Building i NetworkObject.");
            Destroy(obj);
            return;
        }

        building.PlayerClientId.Value = clientId;

        netObj.Spawn();

        spawnedForClients.Add(clientId);

        Debug.Log($"Spawned command center for client {clientId} at {position}");
    }

    private Vector3 GetStartPosition(ulong clientId)
    {
        int id = (int)clientId;
        if (mapdata.StartingBaseLocations.Count < id)
            return new Vector3(0, 0, 0);
        else
            return mapdata.StartingBaseLocations[(int)clientId].transform.position;

    }
}