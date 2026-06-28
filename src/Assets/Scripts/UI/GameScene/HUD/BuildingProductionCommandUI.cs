using System.Collections.Generic;
using UnityEngine;

public class BuildingProductionCommandUI : MonoBehaviour
{
    [Header("Containers")]
    [SerializeField] private Transform unitButtonContainer;
    [SerializeField] private Transform upgradeButtonContainer;

    [Header("Prefab")]
    [SerializeField] private ProductionButtonUI productionButtonPrefab;

    [Header("Auto Refresh")]
    [SerializeField] private float autoRefreshInterval = 1f;

    private GameObject currentSelectedBuilding;
    private float autoRefreshTimer;

    private void OnEnable()
    {
        ForceRefresh();
    }

    private void Update()
    {
        GameObject selectedBuilding = GetSelectedBuilding();

        if (selectedBuilding != currentSelectedBuilding)
        {
            RefreshFromSelection();
            autoRefreshTimer = 0f;
            return;
        }

        autoRefreshTimer += Time.unscaledDeltaTime;

        if (autoRefreshTimer < autoRefreshInterval)
            return;

        autoRefreshTimer = 0f;

        ForceRefresh();
    }

    public void RefreshFromSelection()
    {
        GameObject selectedBuilding = GetSelectedBuilding();

        if (selectedBuilding == null)
        {
            Clear();
            return;
        }

        if (selectedBuilding == currentSelectedBuilding)
            return;

        RebuildForBuilding(selectedBuilding);
    }

    public void ForceRefresh()
    {
        GameObject selectedBuilding = GetSelectedBuilding();

        if (selectedBuilding == null)
        {
            Clear();
            return;
        }

        RebuildForBuilding(selectedBuilding);
    }

    public void Clear()
    {
        currentSelectedBuilding = null;
        ClearContainersOnly();
    }

    private void RebuildForBuilding(GameObject selectedBuilding)
    {
        currentSelectedBuilding = selectedBuilding;

        ClearContainersOnly();

        ProductionBuilding productionBuilding = selectedBuilding.GetComponent<ProductionBuilding>();

        if (productionBuilding == null)
            return;

        List<BuildableUnit> units = productionBuilding.GetBuildableUnits();

        foreach (BuildableUnit unit in units)
        {
            if (unit == null)
                continue;

            ProductionButtonUI button = Instantiate(productionButtonPrefab, unitButtonContainer);
            button.SetupUnit(unit, productionBuilding);
        }

        List<BuildableUpgrade> upgrades = productionBuilding.GetBuildableUpgrades();

        foreach (BuildableUpgrade upgrade in upgrades)
        {
            if (upgrade == null)
                continue;

            ProductionButtonUI button = Instantiate(productionButtonPrefab, upgradeButtonContainer);
            button.SetupUpgrade(upgrade, productionBuilding);
        }
    }

    private GameObject GetSelectedBuilding()
    {
        if (BuildingManager.instance == null)
            return null;

        if (BuildingManager.instance.SelectedBuildings.Count == 0)
            return null;

        return BuildingManager.instance.SelectedBuildings[0];
    }

    private void ClearContainersOnly()
    {
        ClearContainer(unitButtonContainer);
        ClearContainer(upgradeButtonContainer);
    }

    private void ClearContainer(Transform container)
    {
        if (container == null)
            return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

}