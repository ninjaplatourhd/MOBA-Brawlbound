using UnityEngine;

public class PanelController : MonoBehaviour
{

    public GameObject panel;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        panel.SetActive(false);   
    }

    public void TogglePanel()
    {
        if (panel != null)
        {
            bool isActive = panel.activeSelf;
            panel.SetActive(!isActive);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
