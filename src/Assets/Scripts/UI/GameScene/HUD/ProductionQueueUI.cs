using System.Collections.Generic;
using UnityEngine;

public class ProductionQueueUI : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private ProductionQueueItemUI queueItemPrefab;

    private readonly List<ProductionQueueItemUI> spawnedItems = new List<ProductionQueueItemUI>();
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        HideQueue();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        ProductionBuilding productionBuilding = GetSelectedProductionBuilding();

        if (productionBuilding == null)
        {
            HideQueue();
            return;
        }

        ShowQueue();

        int queueCount = Mathf.Min(productionBuilding.BuildQueue.Count, 5);

        SetSpawnedItemCount(queueCount);

        for (int i = 0; i < queueCount; i++)
        {
            BuildQueueItemNet item = productionBuilding.BuildQueue[i];
            Sprite icon = productionBuilding.GetIconForQueueItem(item);

            spawnedItems[i].Setup(
                productionBuilding,
                i,
                item,
                icon
            );
        }
    }

    private ProductionBuilding GetSelectedProductionBuilding()
    {
        if (BuildingManager.instance == null)
            return null;

        if (BuildingManager.instance.SelectedBuildings.Count != 1)
            return null;

        GameObject selectedBuilding = BuildingManager.instance.SelectedBuildings[0];

        if (selectedBuilding == null)
            return null;

        Building building = selectedBuilding.GetComponent<Building>();

        if (building == null)
            return null;

        if (!building.BelongsToLocalPlayer())
            return null;

        return selectedBuilding.GetComponent<ProductionBuilding>();
    }

    private void ShowQueue()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void HideQueue()
    {
        SetSpawnedItemCount(0);

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void SetSpawnedItemCount(int wantedCount)
    {
        if (queueItemPrefab == null)
            return;

        while (spawnedItems.Count < wantedCount)
        {
            ProductionQueueItemUI item = Instantiate(queueItemPrefab, transform);
            spawnedItems.Add(item);
        }

        while (spawnedItems.Count > wantedCount)
        {
            int lastIndex = spawnedItems.Count - 1;

            if (spawnedItems[lastIndex] != null)
                Destroy(spawnedItems[lastIndex].gameObject);

            spawnedItems.RemoveAt(lastIndex);
        }
    }
}