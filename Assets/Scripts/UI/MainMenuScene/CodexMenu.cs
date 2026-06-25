using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;



public class CodexMenu : MonoBehaviour
{

    [SerializeField]
    private Button backBtn;
    [SerializeField]
    private Button buildingsBtn;

    [SerializeField]
    private Button unitsBtn;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        backBtn.onClick.AddListener(() => SceneManager.LoadScene(sceneName: "MainMenu"));

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
