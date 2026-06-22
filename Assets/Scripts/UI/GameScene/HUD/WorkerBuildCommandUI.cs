using UnityEngine;

public class WorkerBuildCommandUI : MonoBehaviour
{
    [Header("Container")]
    [SerializeField] private Transform buildButtonContainer;

    [Header("Prefab")]
    [SerializeField] private WorkerBuildButtonUI buildButtonPrefab;

    private GameObject currentSelectedWorker;

    public void RefreshFromSelection()
    {
        GameObject selectedWorker = GetSelectedWorker();

        if (selectedWorker == null)
        {
            Clear();
            return;
        }

        if (selectedWorker == currentSelectedWorker)
            return;

        currentSelectedWorker = selectedWorker;

        ClearContainerOnly();

        WorkerBuilder workerBuilder = selectedWorker.GetComponent<WorkerBuilder>();

        if (workerBuilder == null)
            return;

        foreach (BuildableBuilding buildableBuilding in workerBuilder.BuildableBuildings)
        {
            if (buildableBuilding == null)
                continue;

            WorkerBuildButtonUI button = Instantiate(buildButtonPrefab, buildButtonContainer);
            button.Setup(buildableBuilding, workerBuilder);
        }
    }

    public void ForceRefresh()
    {
        currentSelectedWorker = null;
        RefreshFromSelection();
    }

    public void Clear()
    {
        currentSelectedWorker = null;
        ClearContainerOnly();
    }

    private GameObject GetSelectedWorker()
    {
        if (UnitManager.instance == null)
            return null;

        if (UnitManager.instance.SelectedUnits.Count == 0)
            return null;

        GameObject selectedUnit = UnitManager.instance.SelectedUnits[0];

        if (selectedUnit == null)
            return null;

        if (!selectedUnit.TryGetComponent<WorkerBuilder>(out WorkerBuilder workerBuilder))
            return null;

        return selectedUnit;
    }

    private void ClearContainerOnly()
    {
        if (buildButtonContainer == null)
            return;

        for (int i = buildButtonContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(buildButtonContainer.GetChild(i).gameObject);
        }
    }
}