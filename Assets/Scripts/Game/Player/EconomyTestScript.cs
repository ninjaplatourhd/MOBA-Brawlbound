using Unity.Netcode;
using UnityEngine;

public class EconomyDebugInput : NetworkBehaviour
{
    private void Update()
    {
        if (!IsClient)
            return;

        if (IngameConsole.IsTypingInConsole)
            return;

        if (Input.GetKeyDown(KeyCode.F8))
        {
            AddMineralsServerRpc(100);
        }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            AddPowerServerRpc(25);
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            UsePowerServerRpc(5);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddMineralsServerRpc(int amount, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (PlayerEconomyManager.Instance == null)
            return;

        PlayerEconomyManager.Instance.AddMinerals(clientId, amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPowerServerRpc(int amount, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (PlayerEconomyManager.Instance == null)
            return;

        PlayerEconomyManager.Instance.AddPowerProduced(clientId, amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UsePowerServerRpc(int amount, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (PlayerEconomyManager.Instance == null)
            return;

        PlayerEconomyManager.Instance.AddPowerUsed(clientId, amount);
    }
}