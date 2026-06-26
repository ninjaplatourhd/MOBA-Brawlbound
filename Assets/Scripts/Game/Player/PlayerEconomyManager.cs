using Unity.Netcode;
using UnityEngine;

public class PlayerEconomyManager : NetworkBehaviour
{
    public static PlayerEconomyManager Instance { get; private set; }

    [Header("Starting Economy")]
    [SerializeField] private int startingMinerals = 500;
    [SerializeField] private int startingPowerProduced = 50;
    [SerializeField] private int startingTechTier = 1;

    private NetworkList<PlayerGameData> playerEconomyStates;

    public NetworkList<PlayerGameData> PlayerEconomyStates => playerEconomyStates;

    private void Awake()
    {
        Instance = this;
        playerEconomyStates = new NetworkList<PlayerGameData>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        InitializeExistingPlayers();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        playerEconomyStates?.Dispose();

        if (Instance == this)
            Instance = null;
    }

    private void InitializeExistingPlayers()
    {
        if (NetworkManager.Singleton == null)
            return;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            InitializePlayer(clientId);
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer)
            return;

        InitializePlayer(clientId);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        int index = FindPlayerIndex(clientId);

        if (index >= 0)
            playerEconomyStates.RemoveAt(index);
    }

    private void InitializePlayer(ulong clientId)
    {
        if (FindPlayerIndex(clientId) >= 0)
            return;

        PlayerGameData data = new PlayerGameData
        {
            ClientId = clientId,
            Minerals = startingMinerals,
            PowerProduced = startingPowerProduced,
            PowerUsed = 0,
            TechTier = startingTechTier,
            IsDefeated = false
        };

        playerEconomyStates.Add(data);
    }

    public bool TryGetPlayerState(ulong clientId, out PlayerGameData data)
    {
        int index = FindPlayerIndex(clientId);

        if (index < 0)
        {
            data = default;
            return false;
        }

        data = playerEconomyStates[index];
        return true;
    }

    public bool CanAfford(ulong clientId, int mineralCost, int requiredFreePower)
    {
        if (!TryGetPlayerState(clientId, out PlayerGameData data))
            return false;

        if (data.IsDefeated)
            return false;

        if (data.Minerals < mineralCost)
            return false;

        if (requiredFreePower <= 0)
            return true;

        return data.PowerAvailable >= requiredFreePower;
    }

    public bool CanResearchUpgrade(ulong clientId, BuildableUpgrade upgrade)
    {
        if (upgrade == null)
            return false;

        if (!TryGetPlayerState(clientId, out PlayerGameData data))
            return false;

        if (data.IsDefeated)
            return false;

        if (data.TechTier < upgrade.RequiredTechTier)
            return false;

        if (upgrade.SetTechTierOnComplete > 0 &&
            data.TechTier >= upgrade.SetTechTierOnComplete)
            return false;

        if (data.Minerals < upgrade.MineralCost)
            return false;

        if (upgrade.RequiredFreePower > 0 &&
            data.PowerAvailable < upgrade.RequiredFreePower)
            return false;

        return true;
    }

    public bool CompleteUpgrade(ulong clientId, BuildableUpgrade upgrade)
    {
        if (!IsServer)
            return false;

        if (upgrade == null)
            return false;

        int index = FindPlayerIndex(clientId);

        if (index < 0)
            return false;

        PlayerGameData data = playerEconomyStates[index];

        if (data.IsDefeated)
            return false;

        if (upgrade.SetTechTierOnComplete <= 0)
            return false;

        if (data.TechTier >= upgrade.SetTechTierOnComplete)
            return false;

        data.TechTier = upgrade.SetTechTierOnComplete;
        playerEconomyStates[index] = data;

        return true;
    }

    public bool TrySpendMinerals(ulong clientId, int amount)
    {
        if (!IsServer)
            return false;

        if (amount < 0)
            return false;

        int index = FindPlayerIndex(clientId);

        if (index < 0)
            return false;

        PlayerGameData data = playerEconomyStates[index];

        if (data.Minerals < amount)
            return false;

        data.Minerals -= amount;
        playerEconomyStates[index] = data;

        return true;
    }

    public bool TrySpendResourcesAndReservePower(ulong clientId, int mineralCost, int powerToReserve)
    {
        if (!IsServer)
            return false;

        int index = FindPlayerIndex(clientId);

        if (index < 0)
            return false;

        PlayerGameData data = playerEconomyStates[index];

        if (data.IsDefeated)
            return false;

        if (data.Minerals < mineralCost)
            return false;

        if (powerToReserve > 0 && data.PowerAvailable < powerToReserve)
            return false;

        data.Minerals -= mineralCost;
        data.PowerUsed += Mathf.Max(0, powerToReserve);

        playerEconomyStates[index] = data;

        return true;
    }

    public void AddMinerals(ulong clientId, int amount)
    {
        if (!IsServer)
            return;

        if (amount <= 0)
            return;

        int index = FindPlayerIndex(clientId);

        if (index < 0)
            return;

        PlayerGameData data = playerEconomyStates[index];
        data.Minerals += amount;

        playerEconomyStates[index] = data;
    }

    public void AddPowerProduced(ulong clientId, int amount)
    {
        if (!IsServer)
            return;

        int index = FindPlayerIndex(clientId);

        if (index < 0)
            return;

        PlayerGameData data = playerEconomyStates[index];
        data.PowerProduced += amount;

        if (data.PowerProduced < 0)
            data.PowerProduced = 0;

        playerEconomyStates[index] = data;
    }

    public void AddPowerUsed(ulong clientId, int amount)
    {
        if (!IsServer)
            return;

        int index = FindPlayerIndex(clientId);

        if (index < 0)
            return;

        PlayerGameData data = playerEconomyStates[index];
        data.PowerUsed += amount;

        if (data.PowerUsed < 0)
            data.PowerUsed = 0;

        playerEconomyStates[index] = data;
    }

    public void SetTechTier(ulong clientId, int techTier)
    {
        if (!IsServer)
            return;

        int index = FindPlayerIndex(clientId);

        if (index < 0)
            return;

        PlayerGameData data = playerEconomyStates[index];
        data.TechTier = Mathf.Max(1, techTier);

        playerEconomyStates[index] = data;
    }

    public void SetDefeated(ulong clientId, bool defeated)
    {
        if (!IsServer)
            return;

        int index = FindPlayerIndex(clientId);

        if (index < 0)
            return;

        PlayerGameData data = playerEconomyStates[index];
        data.IsDefeated = defeated;

        playerEconomyStates[index] = data;
    }

    private int FindPlayerIndex(ulong clientId)
    {
        for (int i = 0; i < playerEconomyStates.Count; i++)
        {
            if (playerEconomyStates[i].ClientId == clientId)
                return i;
        }

        return -1;
    }
}