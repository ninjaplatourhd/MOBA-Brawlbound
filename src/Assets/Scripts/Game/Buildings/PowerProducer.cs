using Unity.Netcode;
using UnityEngine;

public class PowerProducer : NetworkBehaviour
{
    [SerializeField] private int powerProduced = 50;

    private IOwnedObject ownedObject;
    private bool registered;
    private ulong registeredOwnerId;

    private void Awake()
    {
        ownedObject = GetComponent<IOwnedObject>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        RegisterPower();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
            return;

        UnregisterPower();
    }

    private void RegisterPower()
    {
        if (registered)
            return;

        if (PlayerEconomyManager.Instance == null)
            return;

        if (ownedObject == null)
            ownedObject = GetComponent<IOwnedObject>();

        if (ownedObject == null)
            return;

        registeredOwnerId = ownedObject.OwnerClientId;

        PlayerEconomyManager.Instance.AddPowerProduced(registeredOwnerId, powerProduced);
        registered = true;
    }

    private void UnregisterPower()
    {
        if (!registered)
            return;

        if (PlayerEconomyManager.Instance != null)
            PlayerEconomyManager.Instance.AddPowerProduced(registeredOwnerId, -powerProduced);

        registered = false;
    }
}