using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("UI - Info")]
    public TMP_Text nameText;
    public TMP_Text typeText;
    public TMP_Text hpText;

    [Header("UI - Icon")]
    public Image icon;

    [SerializeField] private HUDCommandPanel commandPanel;
    
    private void Update()
    {
        UpdateSelectionUI();
    }

    private void UpdateSelectionUI()
    {
        var units = UnitManager.instance != null ? UnitManager.instance.SelectedUnits : null;
        var buildings = BuildingManager.instance != null ? BuildingManager.instance.SelectedBuildings : null;

        if (units == null || buildings == null)
            return;

        // No selection
        if (units.Count == 0 && buildings.Count == 0)
        {
            ClearUI();
            commandPanel.ShowNothing();
            return;
        }

        // BUILDINGS PRIORITY (optional, you can change later)
        if (buildings.Count > 0)
        {
            ShowBuilding(buildings);
            commandPanel.ShowBuildingCommands();
            return;
        }

        // UNIT HANDLING
        if (units.Count > 0)
        {
            bool hasCombatUnits = false;
            bool hasWorkerOnly = true;

            foreach (var u in units)
            {
                if (u == null) continue;

                var data = u.GetComponent<UnitData>();
                if (data == null) continue;

                if (data.UnitType == UnitType.Combat)
                    hasCombatUnits = true;

                if (data.UnitType == UnitType.Combat)
                    hasWorkerOnly = false;
            }

            
            if (hasCombatUnits)
            {
                ShowUnit(units);
                commandPanel.ShowUnitCommands(); // army commands
                return;
            }

            // Only workers selected
            if (hasWorkerOnly)
            {
                ShowWorker(units);
                commandPanel.ShowWorkerCommands();
                return;
            }
        }

        ClearUI();
        commandPanel.ShowNothing();
    }

    private void ShowUnit(List<GameObject> units)
    {
        if (units.Count == 1)
        {
            Unit unit = units[0].GetComponent<Unit>();
            UnitData data = units[0].GetComponent<UnitData>();

            nameText.text = data.DisplayName;
            typeText.text = "Unit";

            float hp = unit.Health.Value;
            float maxHp = unit.MaxHealth.Value;

            hpText.text = $"{hp} / {maxHp}";

            // icon later (optional)
            icon.enabled = true;
        }
        else
        {
            nameText.text = $"{units.Count} Units Selected";
            typeText.text = "Group";
            hpText.text = "";

            icon.enabled = false;
        }
    }

    private void ShowWorker(List<GameObject> units)
    {
        if (units.Count == 1)
        {
            Unit unit = units[0].GetComponent<Unit>();
            UnitData data = units[0].GetComponent<UnitData>();

            nameText.text = data.DisplayName;
            typeText.text = "Worker";

            float hp = unit.Health.Value;
            float maxHp = unit.MaxHealth.Value;

            hpText.text = $"{hp} / {maxHp}";

            icon.enabled = true;
        }
        else
        {
            nameText.text = $"{units.Count} Workers Selected";
            typeText.text = "Workers";
            hpText.text = "";

            icon.enabled = false;
        }
    }

    private void ShowBuilding(List<GameObject> buildings)
    {
        if (buildings.Count == 1)
        {
            Building b = buildings[0].GetComponent<Building>();
            BuildingData data = buildings[0].GetComponent<BuildingData>();

            nameText.text = data.DisplayName;
            typeText.text = "Building";

            float hp = b.Health.Value;
            float maxHp = b.MaxHealth.Value;

            hpText.text = $"{hp} / {maxHp}";

            icon.enabled = true;
        }
        else
        {
            nameText.text = $"{buildings.Count} Buildings Selected";
            typeText.text = "Group";
            hpText.text = "";

            icon.enabled = false;
        }
    }

    private void ClearUI()
    {
        nameText.text = "";
        typeText.text = "";
        hpText.text = "";
        icon.enabled = false;
    }
}