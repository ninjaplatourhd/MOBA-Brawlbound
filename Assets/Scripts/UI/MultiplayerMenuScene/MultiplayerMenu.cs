using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class MultiplayerMenu : MonoBehaviour
{
    [Header("Lobby List")]
    [SerializeField] private Transform lobbyContainer;
    [SerializeField] private GameObject lobbyPreviewPrefab;

    [Header("Create Lobby")]
    [SerializeField] private TMP_InputField lobbyNameText;
    [SerializeField] private TMP_InputField lobbyPasswordText;
    [SerializeField] private TMP_InputField playerNameText;
    [SerializeField] private Button lobbyCreateSceneBtn;

    [Header("Password Modal")]
    [SerializeField] private GameObject passwordModalPanel;
    [SerializeField] private TMP_Text passwordModalTitleText;
    [SerializeField] private TMP_InputField joinPasswordInput;
    [SerializeField] private Button passwordModalJoinButton;
    [SerializeField] private Button passwordModalCancelButton;

    private Lobby selectedLobbyForJoin;
    private bool isRefreshingLobbies;
    private float lobbyRefreshTimer = 0f;

    private async void Start()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        Debug.Log("Signed in " + AuthenticationService.Instance.PlayerId);

        if (passwordModalPanel != null)
            passwordModalPanel.SetActive(false);

        await ListLobbies();
    }

    private void Awake()
    {
        Debug.Log("MultiplayerMenu Awake");

        if (lobbyCreateSceneBtn == null)
        {
            Debug.LogError("Lobby Create Scene Button nije povezan u Inspectoru.");
            return;
        }

        lobbyCreateSceneBtn.onClick.RemoveAllListeners();
        lobbyCreateSceneBtn.onClick.AddListener(CreateLobbyScene);

        if (passwordModalJoinButton != null)
            passwordModalJoinButton.onClick.AddListener(JoinPasswordLobbyFromModal);

        if (passwordModalCancelButton != null)
            passwordModalCancelButton.onClick.AddListener(ClosePasswordModal);
    }

    private void Update()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
            return;

        lobbyRefreshTimer += Time.deltaTime;

        if (lobbyRefreshTimer >= 5f)
        {
            lobbyRefreshTimer = 0f;
            _ = ListLobbies();
        }
    }

    private async Task ListLobbies()
    {
        if (isRefreshingLobbies)
            return;

        isRefreshingLobbies = true;

        try
        {
            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync();

            foreach (Transform child in lobbyContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (Lobby lobby in queryResponse.Results)
            {
                GameObject instance = Instantiate(lobbyPreviewPrefab, lobbyContainer);

                LobbyPreviewLogic lobbyPreviewLogic = instance.GetComponent<LobbyPreviewLogic>();

                if (lobbyPreviewLogic != null)
                {
                    lobbyPreviewLogic.LoadLobbyData(lobby, this);
                }
                else
                {
                    Debug.LogError("LobbyPreviewPrefab nema LobbyPreviewLogic komponentu.");
                }
            }

            Debug.Log($"Lobbies found: {queryResponse.Results.Count}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning("Lobby query failed: " + e);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Unexpected lobby query error: " + e);
        }
        finally
        {
            isRefreshingLobbies = false;
        }
    }
    private bool LobbyHasCustomPassword(Lobby lobby)
    {
        if (lobby.Data == null)
            return false;

        if (!lobby.Data.ContainsKey("HasPassword"))
            return false;

        return lobby.Data["HasPassword"].Value == "true";
    }

    private bool IsCorrectCustomPassword(Lobby lobby, string password)
    {
        if (!LobbyHasCustomPassword(lobby))
            return true;

        if (lobby.Data == null || !lobby.Data.ContainsKey("Password"))
            return false;

        return lobby.Data["Password"].Value == password;
    }

    public void TryJoinLobby(Lobby lobby)
    {
        if (lobby == null)
            return;

        selectedLobbyForJoin = lobby;

        if (LobbyHasCustomPassword(lobby))
        {
            OpenPasswordModal(lobby);
            return;
        }

        JoinLobbyScene(lobby, "");
    }

    private void OpenPasswordModal(Lobby lobby)
    {
        if (passwordModalPanel == null)
        {
            Debug.LogError("Password modal panel nije povezan.");
            return;
        }

        passwordModalPanel.SetActive(true);

        if (passwordModalTitleText != null)
            passwordModalTitleText.text = $"Enter password for {lobby.Name}";

        if (joinPasswordInput != null)
            joinPasswordInput.text = "";
    }

    private void ClosePasswordModal()
    {
        selectedLobbyForJoin = null;

        if (passwordModalPanel != null)
            passwordModalPanel.SetActive(false);
    }

    private void JoinPasswordLobbyFromModal()
    {
        if (selectedLobbyForJoin == null)
            return;

        string password = joinPasswordInput != null
            ? joinPasswordInput.text.Trim()
            : "";

        if (!IsCorrectCustomPassword(selectedLobbyForJoin, password))
        {
            Debug.LogWarning("Pogrešan password.");
            return;
        }

        JoinLobbyScene(selectedLobbyForJoin, password);
    }

    private void JoinLobbyScene(Lobby lobby, string password)
    {
        LobbyManager.Instance.LobbyName = lobby.Name;
        LobbyManager.Instance.LobbyID = lobby.Id;
        LobbyManager.Instance.LobbyCode = lobby.LobbyCode;
        LobbyManager.Instance.Password = password;

        LobbyManager.Instance.IsOwner = false;
        LobbyManager.Instance.PlayerName = GetPlayerName();

        LobbyManager.Instance.Team = "Team 1";
        LobbyManager.Instance.Color = "Blue";
        LobbyManager.Instance.Ready = false;

        SceneManager.LoadScene("GameLobby");
    }

    private void CreateLobbyScene()
    {
        Debug.Log("Create Lobby button clicked.");

        if (LobbyManager.Instance == null)
        {
            Debug.LogError("LobbyManager.Instance je null. Da li LobbyManager postoji u sceni?");
            return;
        }

        string lobbyName = string.IsNullOrWhiteSpace(lobbyNameText.text)
            ? "New Lobby"
            : lobbyNameText.text.Trim();

        string password = lobbyPasswordText != null
            ? lobbyPasswordText.text.Trim()
            : "";

        Debug.Log($"Lobby name: {lobbyName}");
        Debug.Log($"Password length: {password.Length}");

        if (!string.IsNullOrWhiteSpace(password) && password.Length < 3)
        {
            Debug.LogWarning("Password mora imati bar 3 karaktera ili ostavi prazno.");
            return;
        }

        LobbyManager.Instance.LobbyName = lobbyName;
        LobbyManager.Instance.Password = password;

        LobbyManager.Instance.IsOwner = true;
        LobbyManager.Instance.PlayerName = GetPlayerName();

        LobbyManager.Instance.Team = "Team 1";
        LobbyManager.Instance.Color = "Blue";
        LobbyManager.Instance.Ready = false;

        Debug.Log("Loading GameLobby scene...");

        SceneManager.LoadScene("GameLobby");
    }

    private string GetPlayerName()
    {
        if (playerNameText != null && !string.IsNullOrWhiteSpace(playerNameText.text))
        {
            return playerNameText.text.Trim();
        }

        return "Player" + Random.Range(1, 1000);
    }

    public void GoBackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}