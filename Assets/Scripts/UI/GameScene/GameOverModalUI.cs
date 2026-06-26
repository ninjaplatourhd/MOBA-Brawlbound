using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverModalUI : MonoBehaviour
{
    public static GameOverModalUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject modalPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;

    private void Awake()
    {
        Instance = this;

        if (modalPanel != null)
            modalPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowVictory()
    {
        Show(
            "Pobeda",
            "Svi protivnici su poraženi."
        );
    }

    public void ShowDefeat(string reason)
    {
        Show(
            "Poraz",
            reason
        );
    }

    private void Show(string title, string message)
    {
        Time.timeScale = 1f;

        if (modalPanel != null)
            modalPanel.SetActive(true);

        if (titleText != null)
            titleText.text = title;

        if (messageText != null)
            messageText.text = message;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("MultiplayerMenu");
    }
}