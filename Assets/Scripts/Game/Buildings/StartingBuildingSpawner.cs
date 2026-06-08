using Unity.Netcode;
using UnityEngine;

public class StartingBuildingSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject commandCenterPrefab;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        SpawnStartingBuildings();
    }

    private void SpawnStartingBuildings()
    {
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Vector3 position = GetStartPosition(clientId);

            GameObject obj = Instantiate(commandCenterPrefab, position, Quaternion.identity);

            Building building = obj.GetComponent<Building>();
            NetworkObject netObj = obj.GetComponent<NetworkObject>();

            building.PlayerClientId.Value = clientId;

            netObj.Spawn();
        }
    }

    private Vector3 GetStartPosition(ulong clientId)
    {
        if (clientId == 0)
            return new Vector3(100, 0, 100);

        return new Vector3(50, 0, 100);
    }
}