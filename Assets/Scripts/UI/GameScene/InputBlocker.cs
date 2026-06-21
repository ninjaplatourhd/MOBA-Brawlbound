using UnityEngine.EventSystems;

public static class InputBlocker
{
    public static bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }
}