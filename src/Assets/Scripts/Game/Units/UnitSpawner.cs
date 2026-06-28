using Unity.Netcode;
using UnityEngine;

public class UnitSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject tankPrefab;
    [SerializeField] private GameObject tankPrefab2;
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

        if (IngameConsole.IsTypingInConsole)
            return;
        /*
        if (Input.GetKeyDown(KeyCode.P))
        {
            IngameConsole.print("Klikno sam p");
            TrySpawnTank("Malj");
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            IngameConsole.print("Klikno sam o");
            TrySpawnTank("Leopard");
        }*/
    }

    private void TrySpawnTank(string name)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ground))
        {
            SpawnTankServerRpc(hit.point, name);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnTankServerRpc(Vector3 position, string name, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        GameObject tankObj;

        if (name == "Malj")
            tankObj = Instantiate(tankPrefab, position, Quaternion.identity);
        else
            tankObj = Instantiate(tankPrefab2, position, Quaternion.identity);

        Unit unit = tankObj.GetComponent<Unit>();
        NetworkObject netObj = tankObj.GetComponent<NetworkObject>();

        unit.PlayerClientId.Value = clientId;

        netObj.Spawn();
    }
}