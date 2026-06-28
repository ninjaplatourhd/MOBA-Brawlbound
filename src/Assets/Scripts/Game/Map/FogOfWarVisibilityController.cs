using System.Collections.Generic;
using UnityEngine;

public class FogOfWarVisibilityController : MonoBehaviour
{
    [Header("Update")]
    [SerializeField] private float updateInterval = 0.1f;

    [Header("Known Building Visual")]
    [SerializeField] private bool hideKnownBuildingUIWhenNotVisible = true;

    private float updateTimer;

    private readonly Dictionary<GameObject, Renderer[]> rendererCache = new Dictionary<GameObject, Renderer[]>();
    private readonly Dictionary<GameObject, Canvas[]> canvasCache = new Dictionary<GameObject, Canvas[]>();

    private void Update()
    {
        updateTimer += Time.deltaTime;

        if (updateTimer < updateInterval)
            return;

        updateTimer = 0f;

        UpdateUnitVisibility();
        UpdateBuildingVisibility();
    }

    private void UpdateUnitVisibility()
    {
        if (UnitManager.instance == null)
            return;

        FogOfWar fog = FogOfWar.Instance;

        if (fog == null)
            return;

        foreach (GameObject unitObj in UnitManager.instance.AllUnitsList)
        {
            if (unitObj == null)
                continue;

            bool shouldShow = fog.ShouldShowUnit(unitObj);

            SetObjectRenderersVisible(unitObj, shouldShow);
            SetObjectCanvasesVisible(unitObj, shouldShow);
        }
    }

    private void UpdateBuildingVisibility()
    {
        if (BuildingManager.instance == null)
            return;

        FogOfWar fog = FogOfWar.Instance;

        if (fog == null)
            return;

        foreach (GameObject buildingObj in BuildingManager.instance.AllBuildingsList)
        {
            if (buildingObj == null)
                continue;

            bool shouldShow = fog.ShouldShowBuilding(buildingObj);

            SetObjectRenderersVisible(buildingObj, shouldShow);

            if (!shouldShow)
            {
                SetObjectCanvasesVisible(buildingObj, false);
                continue;
            }

            IOwnedObject ownedObject = buildingObj.GetComponent<IOwnedObject>();

            bool isFriendly = ownedObject != null && fog.IsFriendly(ownedObject);
            bool visibleNow = fog.IsVisibleNow(buildingObj.transform.position);
            bool knownEnemyBuilding = fog.IsKnownEnemyBuilding(buildingObj);

            bool shouldShowUI = true;

            if (!isFriendly && knownEnemyBuilding && !visibleNow && hideKnownBuildingUIWhenNotVisible)
            {
                shouldShowUI = false;
            }

            SetObjectCanvasesVisible(buildingObj, shouldShowUI);
        }
    }

    private void SetObjectRenderersVisible(GameObject obj, bool visible)
    {
        Renderer[] renderers = GetRenderers(obj);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            renderers[i].enabled = visible;
        }
    }

    private void SetObjectCanvasesVisible(GameObject obj, bool visible)
    {
        Canvas[] canvases = GetCanvases(obj);

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null)
                continue;

            canvases[i].enabled = visible;
        }
    }

    private Renderer[] GetRenderers(GameObject obj)
    {
        if (rendererCache.TryGetValue(obj, out Renderer[] renderers))
            return renderers;

        renderers = obj.GetComponentsInChildren<Renderer>(true);
        rendererCache[obj] = renderers;

        return renderers;
    }

    private Canvas[] GetCanvases(GameObject obj)
    {
        if (canvasCache.TryGetValue(obj, out Canvas[] canvases))
            return canvases;

        canvases = obj.GetComponentsInChildren<Canvas>(true);
        canvasCache[obj] = canvases;

        return canvases;
    }
}