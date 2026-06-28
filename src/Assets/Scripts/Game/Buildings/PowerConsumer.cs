using Unity.Netcode;
using UnityEngine;

public class PowerConsumer : NetworkBehaviour
{
    [SerializeField] private int powerUpkeep = 0;

    private IOwnedObject ownedObject;
    private bool registered;
    private ulong registeredOwnerId;

    public int PowerUpkeep => powerUpkeep;

    private void Awake()
    {
        ownedObject = GetComponent<IOwnedObject>();
    }

    public void SetRuntimePowerUpkeep(int value)
    {
        powerUpkeep = Mathf.Max(0, value);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        RegisterPowerUsage();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
            return;

        UnregisterPowerUsage();
    }

    private void RegisterPowerUsage()
    {
        if (registered)
            return;

        if (powerUpkeep <= 0)
            return;

        if (PlayerEconomyManager.Instance == null)
            return;

        if (ownedObject == null)
            ownedObject = GetComponent<IOwnedObject>();

        if (ownedObject == null)
            return;

        registeredOwnerId = ownedObject.OwnerClientId;

        PlayerEconomyManager.Instance.AddPowerUsed(registeredOwnerId, powerUpkeep);
        registered = true;
    }

    private void UnregisterPowerUsage()
    {
        if (!registered)
            return;

        if (PlayerEconomyManager.Instance != null)
            PlayerEconomyManager.Instance.AddPowerUsed(registeredOwnerId, -powerUpkeep);

        registered = false;
    }
}