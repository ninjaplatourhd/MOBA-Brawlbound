using UnityEngine;

public class HUDCommandPanel : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject unitButtons;
    [SerializeField] private GameObject workerButtons;
    [SerializeField] private GameObject buildingButtons;

    public static bool MoveMode;
    public static bool AttackMode;

    private void Start()
    {
        ShowNothing();
    }

    public void ShowUnitCommands()
    {
        unitButtons.SetActive(true);
        workerButtons.SetActive(false);
        buildingButtons.SetActive(false);
    }

    public void ShowWorkerCommands()
    {
        unitButtons.SetActive(false);
        workerButtons.SetActive(true);
        buildingButtons.SetActive(false);
    }

    public void ShowBuildingCommands()
    {
        unitButtons.SetActive(false);
        workerButtons.SetActive(false);
        buildingButtons.SetActive(true);
    }

    public void ShowNothing()
    {
        unitButtons.SetActive(false);
        workerButtons.SetActive(false);
        buildingButtons.SetActive(false);
    }

    public void MoveCommand()
    {
        UnitManager.instance.CurrentCommandMode = CommandMode.Move;
    }

    public void AttackCommand()
    {
        UnitManager.instance.CurrentCommandMode = CommandMode.Attack;
    }

    public void GuardCommand()
    {
        UnitManager.instance.GuardSelectedUnits();
    }

    public void PatrolCommand()
    {
        var units = UnitManager.instance.SelectedUnits;

        if (units.Count == 0)
            return;

        Debug.Log("Patrol mode activated - click 2 points");

        IngameConsole.print("Click 2 points for patrol");

        UnitManager.instance.SendMessage("EnablePatrolMode", SendMessageOptions.DontRequireReceiver);
    }

    public void StopCommand()
    {
        var units = UnitManager.instance.SelectedUnits;

        foreach (var obj in units)
        {
            if (obj.TryGetComponent<UnitMovement>(out UnitMovement movement))
            {
                movement.RequestStop();
            }

            if (obj.TryGetComponent<UnitCombat>(out UnitCombat combat))
            {
                combat.ServerClearAttackTarget();
            }
        }
    }

    public void SpawnWorker()
    {
        var building = BuildingManager.instance.SelectedBuildings;

        if (building.Count == 0)
            return;

        Building selected = building[0].GetComponent<Building>();

        if (selected == null)
            return;

        ProductionBuilding production = selected.GetComponent<ProductionBuilding>();

        if (production == null)
        {
            Debug.LogWarning("Selected building cannot produce units.");
            return;
        }

        production.RequestBuildUnit("worker");
    }
}