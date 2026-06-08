using UnityEngine;

public class BuildingInputTest : MonoBehaviour
{
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.B))
            return;

        if (BuildingManager.instance == null)
        {
            IngameConsole.print("BuildingManager ne postoji.");
            return;
        }

        if (BuildingManager.instance.SelectedBuildings.Count == 0)
        {
            IngameConsole.print("Nijedan building nije selektovan.");
            return;
        }

        GameObject selectedBuilding = BuildingManager.instance.SelectedBuildings[0];

        if (!selectedBuilding.TryGetComponent<Building>(out Building building))
        {
            IngameConsole.print("Selektovani objekat nema Building komponentu.");
            return;
        }

        if (!building.BelongsToLocalPlayer())
        {
            IngameConsole.print("Ovaj building ne pripada lokalnom playeru.");
            return;
        }

        if (selectedBuilding.TryGetComponent<ProductionBuilding>(out ProductionBuilding production))
        {
            IngameConsole.print("Requesting worker from selected building.");
            production.RequestBuildUnit("worker");
        }
        else
        {
            IngameConsole.print("Selektovani building nema ProductionBuilding.");
        }
    }
}