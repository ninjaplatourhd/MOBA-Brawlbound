using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance;

    public string LobbyName;
    public string LobbyID;
    public string LobbyCode;

    public string PlayerName;
    public string Password;

    public int MaxPlayers = 4;
    public bool IsOwner;

    public string Team = "Team 1";
    public string Color = "Blue";
    public bool Ready = false;

    public string SelectedMapSceneName = "GameScene_Map_Desert";
    public string SelectedMapDisplayName = "Desert Valley";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}