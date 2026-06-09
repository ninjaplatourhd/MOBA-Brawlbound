using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiplayerLobby : MonoBehaviour
{
    private const string GameStartKey = "GameStartKey";
    private const string SelectedMapSceneKey = "SelectedMapScene";
    private const string SelectedMapNameKey = "SelectedMapName";
    private const string MapVersionKey = "MapVersion";

    private const string PlayerNameKey = "Name";
    private const string PlayerTeamKey = "Team";
    private const string PlayerColorKey = "Color";
    private const string PlayerReadyKey = "Ready";
    private const string PlayerReadyMapVersionKey = "ReadyMapVersion";

    [Header("Main UI")]
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerRowPrefab;

    [Header("Map UI")]
    [SerializeField] private TMP_Dropdown mapDropdown;
    [SerializeField] private Image mapPreviewImage;
    [SerializeField] private List<MapOption> mapOptions = new List<MapOption>();

    [Header("Start")]
    [SerializeField] private Button gameStartButton;
    [SerializeField] private int minPlayersToStart = 1;

    [Header("Network")]
    [SerializeField] private GameObject networkManager;

    private readonly Dictionary<string, LobbyPlayerUI> playerRows = new Dictionary<string, LobbyPlayerUI>();

    private Lobby currentLobby;

    private float heartbeatTimer;
    private float lobbyPollTimer;

    private bool isSendingHeartbeat;
    private bool isPollingLobby;
    private bool isJoiningGame;
    private bool isUpdatingPlayerData;
    private bool isUpdatingMapFromCode;

    private async void Start()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        await AuthenticationService.Instance.UpdatePlayerNameAsync(LobbyManager.Instance.PlayerName);

        PopulateMapDropdown();

        if (gameStartButton != null)
        {
            gameStartButton.onClick.RemoveAllListeners();
            gameStartButton.gameObject.SetActive(LobbyManager.Instance.IsOwner);
            gameStartButton.interactable = false;
            gameStartButton.onClick.AddListener(StartGameScene);

            Debug.Log("Start button listener added.");
        }
        else
        {
            Debug.LogError("Game Start Button nije povezan u Inspectoru.");
        }

        if (mapDropdown != null)
        {
            mapDropdown.interactable = LobbyManager.Instance.IsOwner;
            mapDropdown.onValueChanged.AddListener(OnMapDropdownChanged);
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        }

        if (LobbyManager.Instance.IsOwner)
            await CreateLobby();
        else
            await JoinLobby();
    }

    private void Awake()
    {
        if (networkManager != null)
            DontDestroyOnLoad(networkManager);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyPolling();
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Disconnect reason: " + NetworkManager.Singleton.DisconnectReason);
        }
    }

    private void PopulateMapDropdown()
    {
        if (mapDropdown == null)
            return;

        mapDropdown.ClearOptions();

        List<string> mapNames = new List<string>();

        foreach (MapOption map in mapOptions)
        {
            mapNames.Add(map.DisplayName);
        }

        mapDropdown.AddOptions(mapNames);

        if (mapOptions.Count > 0)
        {
            mapDropdown.SetValueWithoutNotify(0);
            UpdateMapPreview(0);
        }
    }

    private async Task CreateLobby()
    {
        try
        {
            MapOption defaultMap = mapOptions.Count > 0
                ? mapOptions[0]
                : new MapOption
                {
                    DisplayName = "Riverside Rumble",
                    SceneName = "GameScene_riverside_rumble"
                };

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = BuildLobbyPlayer(false),
                Data = new Dictionary<string, DataObject>
                {
                    { GameStartKey, new DataObject(DataObject.VisibilityOptions.Public, "0") },
                    { SelectedMapSceneKey, new DataObject(DataObject.VisibilityOptions.Public, defaultMap.SceneName) },
                    { SelectedMapNameKey, new DataObject(DataObject.VisibilityOptions.Public, defaultMap.DisplayName) },
                    { MapVersionKey, new DataObject(DataObject.VisibilityOptions.Public, "0") },
                    { "HasPassword", new DataObject(DataObject.VisibilityOptions.Public, string.IsNullOrWhiteSpace(LobbyManager.Instance.Password) ? "false" : "true") },
                    { "Password", new DataObject(DataObject.VisibilityOptions.Public, LobbyManager.Instance.Password) }
                }
            };



            currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                LobbyManager.Instance.LobbyName,
                LobbyManager.Instance.MaxPlayers,
                options
            );

            LobbyManager.Instance.LobbyID = currentLobby.Id;
            LobbyManager.Instance.LobbyCode = currentLobby.LobbyCode;

            RefreshLobbyUI();

            Debug.Log($"Created lobby: {currentLobby.Name}");
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private async Task JoinLobby()
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = BuildLobbyPlayer(false)
            };

            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(
                LobbyManager.Instance.LobbyID,
                options
            );

            RefreshLobbyUI();

            Debug.Log($"Joined lobby: {currentLobby.Name}");
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);

            // Ako password nije tačan, za sada samo vrati igrača u meni.
            // Kasnije možemo napraviti error modal.
            SceneManager.LoadScene("MultiplayerMenu");
        }
    }

    private Player BuildLobbyPlayer(bool ready)
    {
        string currentMapVersion = GetCurrentMapVersion();

        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {
                    PlayerNameKey,
                    new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Member,
                        LobbyManager.Instance.PlayerName
                    )
                },
                {
                    PlayerTeamKey,
                    new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Member,
                        LobbyManager.Instance.Team
                    )
                },
                {
                    PlayerColorKey,
                    new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Member,
                        LobbyManager.Instance.Color
                    )
                },
                {
                    PlayerReadyKey,
                    new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Member,
                        ready ? "true" : "false"
                    )
                },
                {
                    PlayerReadyMapVersionKey,
                    new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Member,
                        ready ? currentMapVersion : "-1"
                    )
                }
            }
        };
    }

    private async void HandleLobbyPolling()
    {
        if (currentLobby == null)
            return;

        if (isPollingLobby)
            return;

        lobbyPollTimer += Time.deltaTime;

        if (lobbyPollTimer < 2f)
            return;

        lobbyPollTimer = 0f;
        isPollingLobby = true;

        try
        {
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);

            RefreshLobbyUI();

            if (!LobbyManager.Instance.IsOwner && !isJoiningGame)
            {
                if (currentLobby.Data != null &&
                    currentLobby.Data.ContainsKey(GameStartKey) &&
                    currentLobby.Data[GameStartKey].Value != "0")
                {
                    isJoiningGame = true;

                    string relayCode = currentLobby.Data[GameStartKey].Value;

                    bool joined = await JoinRelay(relayCode);

                    if (!joined)
                    {
                        isJoiningGame = false;
                    }
                }
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
        finally
        {
            isPollingLobby = false;
        }
    }

    private async void HandleLobbyHeartbeat()
    {
        if (!LobbyManager.Instance.IsOwner)
            return;

        if (currentLobby == null)
            return;

        if (isSendingHeartbeat)
            return;

        heartbeatTimer += Time.deltaTime;

        if (heartbeatTimer < 10f)
            return;

        heartbeatTimer = 0f;
        isSendingHeartbeat = true;

        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
        finally
        {
            isSendingHeartbeat = false;
        }
    }

    private void RefreshLobbyUI()
    {
        if (currentLobby == null)
            return;

        if (lobbyNameText != null)
            lobbyNameText.text = currentLobby.Name;

        SyncMapUIFromLobby();
        RefreshPlayerList();
        RefreshStartButton();
    }

    private void RefreshPlayerList()
    {
        if (playerListContainer == null || playerRowPrefab == null)
            return;

        string currentMapVersion = GetCurrentMapVersion();
        string localPlayerId = AuthenticationService.Instance.PlayerId;

        HashSet<string> playersSeenThisRefresh = new HashSet<string>();

        foreach (Player player in currentLobby.Players)
        {
            string playerId = player.Id;
            playersSeenThisRefresh.Add(playerId);

            string playerName = GetPlayerData(player, PlayerNameKey, player.Id);
            string team = GetPlayerData(player, PlayerTeamKey, "Team 1");
            string color = GetPlayerData(player, PlayerColorKey, "Blue");

            bool ready = GetPlayerData(player, PlayerReadyKey, "false") == "true";
            string readyMapVersion = GetPlayerData(player, PlayerReadyMapVersionKey, "-1");

            bool readyForCurrentMap = ready && readyMapVersion == currentMapVersion;
            bool isHost = player.Id == currentLobby.HostId;
            bool isLocalPlayer = player.Id == localPlayerId;

            LobbyPlayerUI rowUI;

            if (!playerRows.TryGetValue(playerId, out rowUI) || rowUI == null)
            {
                GameObject rowObject = Instantiate(playerRowPrefab, playerListContainer);

                rowUI = rowObject.GetComponent<LobbyPlayerUI>();

                if (rowUI == null)
                {
                    Debug.LogError("Player row prefab nema LobbyPlayerUI komponentu.");
                    Destroy(rowObject);
                    continue;
                }

                playerRows[playerId] = rowUI;
            }

            rowUI.Load(
                playerName,
                team,
                color,
                readyForCurrentMap,
                isHost,
                isLocalPlayer,
                isLocalPlayer ? OnLocalTeamChanged : null,
                isLocalPlayer ? OnLocalColorChanged : null,
                isLocalPlayer ? ToggleLocalReady : null
            );
        }

        List<string> playersToRemove = new List<string>();

        foreach (string existingPlayerId in playerRows.Keys)
        {
            if (!playersSeenThisRefresh.Contains(existingPlayerId))
                playersToRemove.Add(existingPlayerId);
        }

        foreach (string playerIdToRemove in playersToRemove)
        {
            if (playerRows[playerIdToRemove] != null)
                Destroy(playerRows[playerIdToRemove].gameObject);

            playerRows.Remove(playerIdToRemove);
        }
    }

    private void RefreshStartButton()
    {
        if (!LobbyManager.Instance.IsOwner)
            return;

        if (gameStartButton == null)
            return;

        gameStartButton.interactable = AreAllPlayersReady();
    }

    private void SyncMapUIFromLobby()
    {
        if (currentLobby.Data == null)
            return;

        if (!currentLobby.Data.ContainsKey(SelectedMapSceneKey))
            return;

        string selectedScene = currentLobby.Data[SelectedMapSceneKey].Value;

        for (int i = 0; i < mapOptions.Count; i++)
        {
            if (mapOptions[i].SceneName == selectedScene)
            {
                isUpdatingMapFromCode = true;

                if (mapDropdown != null)
                {
                    mapDropdown.SetValueWithoutNotify(i);
                    mapDropdown.RefreshShownValue();
                }

                UpdateMapPreview(i);

                isUpdatingMapFromCode = false;
                return;
            }
        }
    }

    private void UpdateMapPreview(int index)
    {
        if (mapPreviewImage == null)
            return;

        if (index < 0 || index >= mapOptions.Count)
            return;

        mapPreviewImage.sprite = mapOptions[index].PreviewImage;
    }

    private async void OnMapDropdownChanged(int index)
    {
        if (isUpdatingMapFromCode)
            return;

        if (!LobbyManager.Instance.IsOwner)
            return;

        if (currentLobby == null)
            return;

        if (index < 0 || index >= mapOptions.Count)
            return;

        MapOption selectedMap = mapOptions[index];

        int currentVersion = 0;
        int.TryParse(GetCurrentMapVersion(), out currentVersion);

        int newVersion = currentVersion + 1;

        try
        {
            currentLobby = await LobbyService.Instance.UpdateLobbyAsync(
                currentLobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { SelectedMapSceneKey, new DataObject(DataObject.VisibilityOptions.Public, selectedMap.SceneName) },
                        { SelectedMapNameKey, new DataObject(DataObject.VisibilityOptions.Public, selectedMap.DisplayName) },
                        { MapVersionKey, new DataObject(DataObject.VisibilityOptions.Public, newVersion.ToString()) }
                    }
                }
            );

            LobbyManager.Instance.Ready = false;
            await UpdateLocalPlayerData(false);

            RefreshLobbyUI();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private async void OnLocalTeamChanged(string selectedTeam)
    {
        if (isUpdatingPlayerData)
            return;

        LobbyManager.Instance.Team = selectedTeam;
        LobbyManager.Instance.Ready = false;

        await UpdateLocalPlayerData(false);
    }

    private async void OnLocalColorChanged(string selectedColor)
    {
        if (isUpdatingPlayerData)
            return;

        LobbyManager.Instance.Color = selectedColor;
        LobbyManager.Instance.Ready = false;

        await UpdateLocalPlayerData(false);
    }

    private async void ToggleLocalReady()
    {
        if (isUpdatingPlayerData)
            return;

        Player localPlayer = GetLocalLobbyPlayer();

        bool currentlyReady = localPlayer != null && IsPlayerReadyForCurrentMap(localPlayer);
        bool newReady = !currentlyReady;

        LobbyManager.Instance.Ready = newReady;

        await UpdateLocalPlayerData(newReady);
    }

    private async Task UpdateLocalPlayerData(bool ready)
    {
        if (currentLobby == null)
            return;

        isUpdatingPlayerData = true;

        try
        {
            string currentMapVersion = GetCurrentMapVersion();

            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    {
                        PlayerNameKey,
                        new PlayerDataObject(
                            PlayerDataObject.VisibilityOptions.Member,
                            LobbyManager.Instance.PlayerName
                        )
                    },
                    {
                        PlayerTeamKey,
                        new PlayerDataObject(
                            PlayerDataObject.VisibilityOptions.Member,
                            LobbyManager.Instance.Team
                        )
                    },
                    {
                        PlayerColorKey,
                        new PlayerDataObject(
                            PlayerDataObject.VisibilityOptions.Member,
                            LobbyManager.Instance.Color
                        )
                    },
                    {
                        PlayerReadyKey,
                        new PlayerDataObject(
                            PlayerDataObject.VisibilityOptions.Member,
                            ready ? "true" : "false"
                        )
                    },
                    {
                        PlayerReadyMapVersionKey,
                        new PlayerDataObject(
                            PlayerDataObject.VisibilityOptions.Member,
                            ready ? currentMapVersion : "-1"
                        )
                    }
                }
            };

            currentLobby = await LobbyService.Instance.UpdatePlayerAsync(
                currentLobby.Id,
                AuthenticationService.Instance.PlayerId,
                options
            );

            RefreshLobbyUI();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
        finally
        {
            isUpdatingPlayerData = false;
        }
    }

    private async void StartGameScene()
    {
        Debug.Log("Start Game clicked.");

        if (!LobbyManager.Instance.IsOwner)
        {
            Debug.LogWarning("Start blocked: local player is not lobby owner.");
            return;
        }

        if (currentLobby == null)
        {
            Debug.LogWarning("Start blocked: currentLobby is null.");
            return;
        }

        bool allReady = AreAllPlayersReady();
        Debug.Log($"Are all players ready: {allReady}");

        if (!allReady)
        {
            Debug.LogWarning("Ne mogu da startujem. Nisu svi ready.");
            DebugLogReadyStates();
            return;
        }

        if (currentLobby.Data == null || !currentLobby.Data.ContainsKey(SelectedMapSceneKey))
        {
            Debug.LogError("Start blocked: SelectedMapScene ne postoji u Lobby Data.");
            return;
        }

        string selectedMapScene = currentLobby.Data[SelectedMapSceneKey].Value;
        Debug.Log($"Selected map scene: {selectedMapScene}");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("Start blocked: NetworkManager.Singleton is null.");
            return;
        }

        if (gameStartButton != null)
            gameStartButton.interactable = false;

        bool relayCreated = await CreateRelay();

        Debug.Log($"Relay created: {relayCreated}");

        if (!relayCreated)
        {
            if (gameStartButton != null)
                gameStartButton.interactable = true;

            return;
        }

        Debug.Log($"Loading network scene: {selectedMapScene}");

        NetworkManager.Singleton.SceneManager.LoadScene(
            selectedMapScene,
            LoadSceneMode.Single
        );
    }

    private void DebugLogReadyStates()
    {
        if (currentLobby == null)
            return;

        string currentMapVersion = GetCurrentMapVersion();

        Debug.Log($"Current MapVersion: {currentMapVersion}");

        foreach (Player player in currentLobby.Players)
        {
            string playerName = GetPlayerData(player, PlayerNameKey, player.Id);
            string ready = GetPlayerData(player, PlayerReadyKey, "false");
            string readyMapVersion = GetPlayerData(player, PlayerReadyMapVersionKey, "-1");

            Debug.Log($"{playerName} | Ready={ready} | ReadyMapVersion={readyMapVersion}");
        }
    }

    private async Task<bool> CreateRelay()
    {
        try
        {
            int maxConnections = LobbyManager.Instance.MaxPlayers - 1;

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            bool started = NetworkManager.Singleton.StartHost();

            if (!started)
            {
                Debug.LogError("StartHost failed.");
                return false;
            }

            currentLobby = await LobbyService.Instance.UpdateLobbyAsync(
                currentLobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { GameStartKey, new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                    }
                }
            );

            Debug.Log("Relay join code: " + joinCode);

            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
            return false;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return false;
        }
    }

    private async Task<bool> JoinRelay(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            bool started = NetworkManager.Singleton.StartClient();

            if (!started)
            {
                Debug.LogError("StartClient failed.");
                return false;
            }

            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
            return false;
        }
    }

    private bool AreAllPlayersReady()
    {
        if (currentLobby == null)
            return false;

        if (currentLobby.Players.Count < minPlayersToStart)
            return false;

        foreach (Player player in currentLobby.Players)
        {
            if (!IsPlayerReadyForCurrentMap(player))
                return false;
        }

        return true;
    }

    private bool IsPlayerReadyForCurrentMap(Player player)
    {
        string currentMapVersion = GetCurrentMapVersion();

        bool ready = GetPlayerData(player, PlayerReadyKey, "false") == "true";
        string readyMapVersion = GetPlayerData(player, PlayerReadyMapVersionKey, "-1");

        return ready && readyMapVersion == currentMapVersion;
    }

    private Player GetLocalLobbyPlayer()
    {
        if (currentLobby == null)
            return null;

        string localPlayerId = AuthenticationService.Instance.PlayerId;

        foreach (Player player in currentLobby.Players)
        {
            if (player.Id == localPlayerId)
                return player;
        }

        return null;
    }

    private string GetPlayerData(Player player, string key, string defaultValue)
    {
        if (player.Data == null)
            return defaultValue;

        if (!player.Data.ContainsKey(key))
            return defaultValue;

        return player.Data[key].Value;
    }

    private string GetCurrentMapVersion()
    {
        if (currentLobby == null || currentLobby.Data == null)
            return "0";

        if (!currentLobby.Data.ContainsKey(MapVersionKey))
            return "0";

        return currentLobby.Data[MapVersionKey].Value;
    }
}