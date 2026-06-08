using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPreviewLogic : MonoBehaviour
{
    private Lobby lobby;
    private MultiplayerMenu multiplayerMenu;

    private string lobbyName;
    private string lobbyId;

    [SerializeField]
    private TMP_Text lobbyNameText;

    [SerializeField]
    private TMP_Text ownerNameText;

    [SerializeField]
    private TMP_Text playerCountText;

    [SerializeField]
    private TMP_Text lockStatusText;

    [SerializeField]
    public Button joinButton;

    public void Awake()
    {
        joinButton.onClick.AddListener(() => JoinLobby());
    }
    public void LoadLobbyData(Lobby lobby, MultiplayerMenu menu)
    {
        this.lobby = lobby;
        multiplayerMenu = menu;

        lobbyName = lobby.Name;
        lobbyId = lobby.Id;
        ownerNameText.text = "Test";
        playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
        if (lobby.HasPassword)
        {
            lockStatusText.text = "Locked";
            lockStatusText.color = Color.red;
        }
        else
        {
            lockStatusText.text = "Open";
            lockStatusText.color = Color.green;
        }
        lobbyNameText.text = lobby.Name;
    }


    public void JoinLobby()
    {
        if (lobby == null || multiplayerMenu == null)
            return;

        multiplayerMenu.TryJoinLobby(lobby);
    }
}
