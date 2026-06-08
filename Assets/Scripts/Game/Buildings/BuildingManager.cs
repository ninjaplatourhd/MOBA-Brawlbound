using System.Collections.Generic;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager instance { get; private set; }

    public List<GameObject> AllBuildingsList = new List<GameObject>();
    public List<GameObject> SelectedBuildings = new List<GameObject>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    public void SelectBuilding(GameObject building)
    {
        IngameConsole.print("Selektovan building");
        DeSelectAll();

        SelectedBuildings.Add(building);

        if (building.TryGetComponent<ISelectableObject>(out ISelectableObject selectable))
        {
            selectable.Select();
        }
    }

    public void DeSelectAll()
    {
        foreach (GameObject building in SelectedBuildings)
        {
            if (building != null &&
                building.TryGetComponent<ISelectableObject>(out ISelectableObject selectable))
            {
                selectable.DeSelect();
            }
        }

        SelectedBuildings.Clear();
    }
}