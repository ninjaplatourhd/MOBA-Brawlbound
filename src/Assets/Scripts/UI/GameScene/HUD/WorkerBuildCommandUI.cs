using UnityEngine;

public class WorkerBuildCommandUI : MonoBehaviour
{
    [Header("Container")]
    [SerializeField] private Transform buildButtonContainer;

    [Header("Prefab")]
    [SerializeField] private WorkerBuildButtonUI buildButtonPrefab;

    [Header("Auto Refresh")]
    [SerializeField] private float autoRefreshInterval = 1f;

    private GameObject currentSelectedWorker;
    private float autoRefreshTimer;

    private void OnEnable()
    {
        ForceRefresh();
    }

    private void Update()
    {
        GameObject selectedWorker = GetSelectedWorker();

        if (selectedWorker != currentSelectedWorker)
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
        GameObject selectedWorker = GetSelectedWorker();

        if (selectedWorker == null)
        {
            Clear();
            return;
        }

        if (selectedWorker == currentSelectedWorker)
            return;

        RebuildForWorker(selectedWorker);
    }

    public void ForceRefresh()
    {
        GameObject selectedWorker = GetSelectedWorker();

        if (selectedWorker == null)
        {
            Clear();
            return;
        }

        RebuildForWorker(selectedWorker);
    }

    public void Clear()
    {
        currentSelectedWorker = null;
        ClearContainerOnly();
    }

    private void RebuildForWorker(GameObject selectedWorker)
    {
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
            Destroy(buildButtonContainer.GetChild(i).gameObject);
    }
}