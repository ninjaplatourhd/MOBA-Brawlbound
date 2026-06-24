using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUIFunctions : MonoBehaviour
{

    [SerializeField]
    private Button playBtn;
    [SerializeField]
    private Button quitBtn;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playBtn.onClick.AddListener(() => SceneManager.LoadScene(sceneName: "MultiplayerMenu"));
        quitBtn.onClick.AddListener(() => QuitGame());
        
    }

    // Update is called once per frame
    void Update()
    {

    }


    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
