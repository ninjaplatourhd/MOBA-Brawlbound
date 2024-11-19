using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiplayerMenu : MonoBehaviour
{

    [SerializeField]
    private Transform lobbyContainer;

    [SerializeField]
    private GameObject lobbyPreviewPrefab;

    [SerializeField]
    private TMP_InputField lobbyNameText;

    [SerializeField]
    private Button lobbyCreateSceneBtn;


    private async void Start()
    {
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log("Singed in " + AuthenticationService.Instance.PlayerId);
        };



        AuthenticationService.Instance.SignInAnonymouslyAsync();

        ListLobbies();
    }

    private void Awake()
    {
        lobbyCreateSceneBtn.onClick.AddListener(() => CreateLobbyScene());
    }

    private float lobbyRefreshTimer = 50;
    public void Update()
    {
        if (lobbyRefreshTimer >= 5)
        {
            ListLobbies();
            lobbyRefreshTimer = 0;
        }
        lobbyRefreshTimer += Time.deltaTime;
    }


    private async void ListLobbies()
    {
        try
        {
            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync();
            Debug.Log($"Lobbies found: {queryResponse.Results.Count}");

            foreach (Transform child in lobbyContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (Lobby lobby in queryResponse.Results)
            {
                Debug.Log($"{lobby.Name} {lobby.MaxPlayers}");

                GameObject instance = Instantiate(lobbyPreviewPrefab, lobbyContainer);

                LobbyPreviewLogic lobbyPreviewLogic = instance.GetComponent<LobbyPreviewLogic>();
                lobbyPreviewLogic.LoadLobbyData(lobby);

            }
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private void CreateLobbyScene()
    {
        LobbyManager.Instance.LobbyName = lobbyNameText.text;
        LobbyManager.Instance.IsOwner = true;
        SceneManager.LoadScene(sceneName: "GameLobby");
    }


}
