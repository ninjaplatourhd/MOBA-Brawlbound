using UnityEngine;

public class MenuSwitch : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public GameObject[] menus;

    public void OpenMenu(GameObject menuToOpen)
    {
        foreach (GameObject menu in menus)
        {
            menu.SetActive(false);
        }

        menuToOpen.SetActive(true);
    }

}
