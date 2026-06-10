using UnityEngine;

public class HUDCommandPanel : MonoBehaviour
{
    public static bool MoveMode;
    public static bool AttackMode;

    public void MoveCommand()
    {
        UnitManager.instance.CurrentCommandMode = CommandMode.Move;
    }

    public void AttackCommand()
    {
        UnitManager.instance.CurrentCommandMode = CommandMode.Attack;
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
}