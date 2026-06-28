using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameOverManager : NetworkBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("Check")]
    [SerializeField] private float initialCheckDelay = 3f;
    [SerializeField] private float checkInterval = 1f;

    private readonly HashSet<ulong> knownPlayers = new HashSet<ulong>();
    private readonly HashSet<ulong> defeatedPlayers = new HashSet<ulong>();
    private readonly HashSet<ulong> playersThatHadAssets = new HashSet<ulong>();

    private float nextCheckTime;
    private bool gameEnded;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        RegisterConnectedPlayers();

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
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (gameEnded)
            return;

        if (Time.time < initialCheckDelay)
            return;

        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + checkInterval;

        RegisterConnectedPlayers();
        CheckDefeatedPlayersByAssets();
        CheckWinCondition();
    }

    public void RequestSurrender()
    {
        if (!IsClient)
            return;

        RequestSurrenderServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSurrenderServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        DefeatPlayer(senderClientId, "Predao si se.");
    }

    private void RegisterConnectedPlayers()
    {
        if (NetworkManager.Singleton == null)
            return;

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            knownPlayers.Add(clientId);
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer)
            return;

        knownPlayers.Add(clientId);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        if (gameEnded)
            return;

        knownPlayers.Add(clientId);
        DefeatPlayer(clientId, "Igrač je izgubio konekciju.");
    }

    private void CheckDefeatedPlayersByAssets()
    {
        foreach (ulong clientId in knownPlayers)
        {
            if (defeatedPlayers.Contains(clientId))
                continue;

            bool hasAssets = HasAnyAliveAsset(clientId);

            if (hasAssets)
            {
                playersThatHadAssets.Add(clientId);
                continue;
            }

            if (!playersThatHadAssets.Contains(clientId))
                continue;

            DefeatPlayer(clientId, "Svi tvoji uniti i buildingzi su uništeni.");
        }
    }

    private bool HasAnyAliveAsset(ulong clientId)
    {
        if (UnitManager.instance != null)
        {
            foreach (GameObject unitObject in UnitManager.instance.AllUnitsList)
            {
                if (unitObject == null)
                    continue;

                Unit unit = unitObject.GetComponent<Unit>();

                if (unit == null)
                    continue;

                if (unit.PlayerClientId.Value != clientId)
                    continue;

                if (unit.Health.Value > 0f)
                    return true;
            }
        }

        if (BuildingManager.instance != null)
        {
            foreach (GameObject buildingObject in BuildingManager.instance.AllBuildingsList)
            {
                if (buildingObject == null)
                    continue;

                Building building = buildingObject.GetComponent<Building>();

                if (building == null)
                    continue;

                if (building.PlayerClientId.Value != clientId)
                    continue;

                if (building.Health.Value > 0f)
                    return true;
            }
        }

        return false;
    }

    private void DefeatPlayer(ulong clientId, string reason)
    {
        if (defeatedPlayers.Contains(clientId))
            return;

        defeatedPlayers.Add(clientId);

        if (PlayerEconomyManager.Instance != null)
            PlayerEconomyManager.Instance.SetDefeated(clientId, true);

        ShowDefeatClientRpc(
            reason,
            CreateTargetParams(clientId)
        );

        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        if (gameEnded)
            return;

        if (knownPlayers.Count < 2)
            return;

        int aliveCount = 0;
        ulong winnerClientId = 0;

        foreach (ulong clientId in knownPlayers)
        {
            if (defeatedPlayers.Contains(clientId))
                continue;

            aliveCount++;
            winnerClientId = clientId;
        }

        if (aliveCount != 1)
            return;

        gameEnded = true;

        ShowVictoryClientRpc(
            CreateTargetParams(winnerClientId)
        );
    }

    private ClientRpcParams CreateTargetParams(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };
    }

    [ClientRpc]
    private void ShowDefeatClientRpc(string reason, ClientRpcParams clientRpcParams = default)
    {
        if (GameOverModalUI.Instance == null)
        {
            Debug.LogWarning("GameOverModalUI ne postoji u sceni.");
            return;
        }

        GameOverModalUI.Instance.ShowDefeat(reason);
    }

    [ClientRpc]
    private void ShowVictoryClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (GameOverModalUI.Instance == null)
        {
            Debug.LogWarning("GameOverModalUI ne postoji u sceni.");
            return;
        }

        GameOverModalUI.Instance.ShowVictory();
    }
}