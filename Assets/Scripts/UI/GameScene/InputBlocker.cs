using UnityEngine.EventSystems;

public static class InputBlocker
{
    public static bool SelectionConsumed;

    public static bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }
}