using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyPreviewLogic : MonoBehaviour
{
    private string lobbyName;
    private string lobbyId;

    [SerializeField]
    private TMP_Text lobbyNameText;

    [SerializeField]
    public Button joinButton;

    public void Awake()
    {
        joinButton.onClick.AddListener(() => JoinLobby());
    }
    public void LoadLobbyData(Lobby lobby)
    {
        this.lobbyName = lobby.Name;
        this.lobbyId = lobby.Id;

        lobbyNameText.text = lobby.Name;

    }


    public async void JoinLobby()
    {
        LobbyManager.Instance.LobbyName = lobbyName;
        LobbyManager.Instance.LobbyID = lobbyId;
        LobbyManager.Instance.IsOwner = false;
        SceneManager.LoadScene(sceneName: "GameLobby");
    }
}
