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

    private void Update()
    {
        UpdateSelectionUI();
    }

    private void UpdateSelectionUI()
    {
        List<GameObject> units = UnitManager.instance.SelectedUnits;
        List<GameObject> buildings = BuildingManager.instance.SelectedBuildings;

        if (units.Count == 0 && buildings.Count == 0)
        {
            ClearUI();
            return;
        }

        // PRIORITY 1: Units
        if (units.Count > 0)
        {
            ShowUnit(units);
            return;
        }

        // PRIORITY 2: Buildings
        if (buildings.Count > 0)
        {
            ShowBuilding(buildings);
            return;
        }
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