using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CodexUI : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private WorkerBuildButtonUI workerBuildButtonPrefab;
    [SerializeField] private ProductionButtonUI productionButtonPrefab;

    [Header("Buildings")]
    [SerializeField] private BuildingData komandniCentar;
    [SerializeField] private BuildingData sklapacDroida;
    [SerializeField] private BuildingData fabrikaTenkova;
    [SerializeField] private BuildingData tower;

    [Header("Rows")]
    [SerializeField] private CodexRowUI komandniCentarRow;
    [SerializeField] private CodexRowUI sklapacDroidaRow;
    [SerializeField] private CodexRowUI fabrikaTenkovaRow;
    [SerializeField] private CodexRowUI towerRow;

    private void Start()
    {
        FillBuildingRow(komandniCentar, komandniCentarRow);
        FillBuildingRow(sklapacDroida, sklapacDroidaRow);
        FillBuildingRow(fabrikaTenkova, fabrikaTenkovaRow);
        FillBuildingRow(tower, towerRow);

    }

    private void FillBuildingRow(BuildingData building, CodexRowUI row)
    {

        if (building == null || row == null)
            return;

        if (row.BuildingColumn != null)
        {
            WorkerBuildButtonUI buildBtn =
                Instantiate(workerBuildButtonPrefab, row.BuildingColumn);

            buildBtn.Setup(
                new BuildableBuilding()
                {
                    BuildingId = building.BuildingId,
                    DisplayName = building.DisplayName,
                    Icon = building.Icon
                },
                null
            );

            ForceIconOnly(buildBtn.gameObject);
        }

        foreach (var unit in building.BuildableUnits)
        {
            if (unit == null)
                continue;

            Transform parent = GetTierParent(row, unit.RequiredTechTier);
            if (parent == null)
                continue;

            ProductionButtonUI btn =
                Instantiate(productionButtonPrefab, parent);

            btn.SetupUnit(unit, null);

            ForceIconOnly(btn.gameObject);
        }

        foreach (var upgrade in building.BuildableUpgrades)
        {
            if (upgrade == null)
                continue;

            Transform parent = GetTierParent(row, upgrade.RequiredTechTier);
            if (parent == null)
                continue;

            ProductionButtonUI btn =
                Instantiate(productionButtonPrefab, parent);

            btn.SetupUpgrade(upgrade, null);

            ForceIconOnly(btn.gameObject);
        }
    }

    private Transform GetTierParent(CodexRowUI row, int tier)
    {
        switch (tier)
        {
            case 1: return row.Tier1;
            case 2: return row.Tier2;
            case 3: return row.Tier3;
            default: return null;
        }
    }

    private void ForceIconOnly(GameObject obj)
    {
        var tmpTexts = obj.GetComponentsInChildren<TMPro.TMP_Text>(true);
        foreach (var t in tmpTexts)
            t.gameObject.SetActive(false);

        var uiTexts = obj.GetComponentsInChildren<UnityEngine.UI.Text>(true);
        foreach (var t in uiTexts)
            t.gameObject.SetActive(false);

        var images = obj.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
            img.raycastTarget = true;

        var btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            btn.interactable = false;

            var colors = btn.colors;
            colors.disabledColor = Color.white;
            btn.colors = colors;
        }
    }

    public void GoBackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}