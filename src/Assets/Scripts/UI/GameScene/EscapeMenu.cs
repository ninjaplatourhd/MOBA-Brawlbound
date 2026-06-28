using UnityEngine;
using UnityEngine.SceneManagement;

public class EscapeMenu : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;

    private bool isOpen;

    void Update()
    {
        if (IngameConsole.IsTypingInConsole)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    private void ToggleMenu()
    {
        isOpen = !isOpen;
        menuPanel.SetActive(isOpen);

        Time.timeScale = isOpen ? 0f : 1f;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        isOpen = false;
        menuPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void Surrender()
    {
        Time.timeScale = 1f;

        isOpen = false;

        if (menuPanel != null)
            menuPanel.SetActive(false);

        if (GameOverManager.Instance != null)
        {
            GameOverManager.Instance.RequestSurrender();
            return;
        }

        SceneManager.LoadScene("MultiplayerMenu");
    }
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}