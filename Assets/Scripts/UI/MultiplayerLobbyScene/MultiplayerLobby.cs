using System.Collections.Generic;
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
using static Unity.Services.Lobbies.Models.DataObject;

public class MultiplayerLobby : MonoBehaviour
{

    [SerializeField]
    private Button gameStartButton;

    [SerializeField]
    private GameObject networkManager;

    private Lobby hostLobby;
    private Lobby clientLobby;

    private async void Start()
    {
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += async () =>
        {
            Debug.Log("Singed in " + AuthenticationService.Instance.PlayerId);
            await AuthenticationService.Instance.UpdatePlayerNameAsync(LobbyManager.Instance.PlayerName);

        };
        AuthenticationService.Instance.SignInAnonymouslyAsync();

        if (LobbyManager.Instance.IsOwner)
            CreateLobby();
        else
            JoinLobby();

    }

    private void Awake()
    {
        gameStartButton.onClick.AddListener(() => StartGameScene());

        DontDestroyOnLoad(networkManager);
    }


    private void Update()
    {
        LogLobbyDiagnostics();
        HandleLobbyHeartbeat();
        HandleLobbyPolling();
    }

    private float heartbeatTimer = 50;
    private void HandleLobbyHeartbeat()
    {
        if (hostLobby == null)
            return;

        if (heartbeatTimer > 10)
        {
            heartbeatTimer = 0;
            LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
        }
        heartbeatTimer += Time.deltaTime;
    }

    private float lobbyLogTimer = 2;
    private void LogLobbyDiagnostics()
    {
        if (hostLobby == null)
            return;

        if (lobbyLogTimer > 3)
        {
            lobbyLogTimer = 0;
            Debug.Log($"Players in lobby: ${hostLobby.Players.Count}");
            foreach (Player p in hostLobby.Players)
            {
                Debug.Log(p.Id);
            }
        }
        lobbyLogTimer += Time.deltaTime;
    }

    private float lobbyUpdateTimer = 0;
    private async void HandleLobbyPolling()
    {
        if (clientLobby == null)
            return;

        try
        {
            if (lobbyUpdateTimer > 4)
            {
                lobbyUpdateTimer = 0;
                Debug.Log("Bruh");
                clientLobby = await LobbyService.Instance.GetLobbyAsync(clientLobby.Id);
                Debug.Log($"Huh wuah huh {clientLobby.Id}");
                foreach (var d in clientLobby.Data)
                {
                    Debug.Log($"Key :{d.Key}");
                }
                if (clientLobby.Data["GameStartKey"].Value != "0")
                {
                    //Game start logic for client
                    JoinRelay(clientLobby.Data["GameStartKey"].Value);
                    SceneManager.LoadScene(sceneName: "GameScene");

                }
            }


        }
        catch (LobbyServiceException e)
        { Debug.Log(e); }
        lobbyUpdateTimer += Time.deltaTime;
    }

    private async void CreateLobby()
    {
        try
        {
            string lobbyName = LobbyManager.Instance.LobbyName;
            int maxPlayers = 4;

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject> {
                    { "GameStartKey", new DataObject(VisibilityOptions.Public,"0")}
                }
            };


            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            Debug.Log($"Created Lobby! {lobby.Name} {lobby.MaxPlayers}");
            Debug.Log($"Lobby Code: {lobby.LobbyCode}");

            hostLobby = lobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private async void JoinLobby()
    {
        try
        {
            var lobbyJoined = await LobbyService.Instance.JoinLobbyByIdAsync(LobbyManager.Instance.LobbyID);
            clientLobby = lobbyJoined;
            Debug.Log($"Joined lobby with code: {LobbyManager.Instance.LobbyID}");

            Debug.Log($"Players in lobby FROM CLIENT PERSPECTIVE: ${lobbyJoined.Players.Count}");
            foreach (Player p in lobbyJoined.Players)
            {
                Debug.Log(p.Id);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }


    private void UpdateLobbyStartKey(string relayCode)
    {
        try
        {
            LobbyService.Instance.UpdateLobbyAsync(hostLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "GameStartKey", new DataObject(DataObject.VisibilityOptions.Public, relayCode) }
                }
            });
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }


    private async void CreateRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            //  codeText.text = "Room Code: " + joinCode;
            Debug.Log("Room Code: " + joinCode);
            UpdateLobbyStartKey(joinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData);

            NetworkManager.Singleton.StartHost();
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
    }

    private async void JoinRelay(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData);

            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
    }

    private void StartGameScene()
    {

        CreateRelay();

        SceneManager.LoadScene(sceneName: "GameScene");
    }


}