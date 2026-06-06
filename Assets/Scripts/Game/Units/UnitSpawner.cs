using Unity.Netcode;
using UnityEngine;

public class UnitSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject tankPrefab;
    [SerializeField] private LayerMask ground;

    private Camera mainCamera;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            mainCamera = Camera.main;
        }
    }

    private void Update()
    {
        //  IngameConsole.print("KAAAAA");
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.P))
        {
            IngameConsole.print("Klikno sam p");
            TrySpawnTank();
        }
    }

    private void TrySpawnTank()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ground))
        {
            SpawnTankServerRpc(hit.point);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnTankServerRpc(Vector3 position, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        GameObject tankObj = Instantiate(tankPrefab, position, Quaternion.identity);

        Unit unit = tankObj.GetComponent<Unit>();
        NetworkObject netObj = tankObj.GetComponent<NetworkObject>();

        unit.PlayerClientId.Value = clientId;

        netObj.Spawn();
    }
}