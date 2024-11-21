using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance;
    public string LobbyName;
    public string LobbyID;
    public string PlayerName;
    public int MaxPlayers;
    public string Password;
    public bool IsOwner;


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
