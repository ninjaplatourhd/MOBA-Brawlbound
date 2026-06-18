using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MinimapUIController : MonoBehaviour
{
    [Serializable]
    public class PlayerMinimapColor
    {
        public ulong ClientId;
        public Color Color = Color.white;
    }

    [Header("References")]
    [SerializeField] private MapData mapData;
    [SerializeField] private RectTransform iconContainer;

    [Header("Prefabs")]
    [SerializeField] private MinimapIconUI unitIconPrefab;
    [SerializeField] private MinimapIconUI buildingIconPrefab;

    [Header("Update")]
    [SerializeField] private float refreshInterval = 0.05f;

    [Header("Colors")]
    [SerializeField] private List<PlayerMinimapColor> playerColors = new List<PlayerMinimapColor>();
    [SerializeField] private Color fallbackColorA = Color.cyan;
    [SerializeField] private Color fallbackColorB = Color.red;
    [SerializeField] private Color fallbackColorC = Color.green;
    [SerializeField] private Color fallbackColorD = Color.yellow;

    [Header("Future")]
    [SerializeField] private bool useVisibilityFiltering = false;

    private readonly Dictionary<GameObject, MinimapIconUI> unitIcons = new Dictionary<GameObject, MinimapIconUI>();
    private readonly Dictionary<GameObject, MinimapIconUI> buildingIcons = new Dictionary<GameObject, MinimapIconUI>();

    private float refreshTimer;

    private void Awake()
    {
        if (mapData == null)
            mapData = MapData.Instance;
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;

        if (refreshTimer >= refreshInterval)
        {
            refreshTimer = 0f;
            RefreshMinimap();
        }
    }

    private void RefreshMinimap()
    {
        if (mapData == null)
            return;

        RefreshUnitIcons();
        RefreshBuildingIcons();
    }

    private void RefreshUnitIcons()
    {
        List<GameObject> units = UnitManager.instance != null
            ? UnitManager.instance.AllUnitsList
            : null;

        SyncIcons(units, unitIcons, unitIconPrefab);
        UpdateIconPositions(unitIcons, true);
    }

    private void RefreshBuildingIcons()
    {
        List<GameObject> buildings = BuildingManager.instance != null
            ? BuildingManager.instance.AllBuildingsList
            : null;

        SyncIcons(buildings, buildingIcons, buildingIconPrefab);
        UpdateIconPositions(buildingIcons, false);
    }

    private void SyncIcons(
        List<GameObject> sourceObjects,
        Dictionary<GameObject, MinimapIconUI> existingIcons,
        MinimapIconUI prefab)
    {
        List<GameObject> toRemove = new List<GameObject>();

        foreach (var pair in existingIcons)
        {
            GameObject obj = pair.Key;

            if (obj == null || !ShouldShowObject(obj) || sourceObjects == null || !sourceObjects.Contains(obj))
            {
                if (pair.Value != null)
                    Destroy(pair.Value.gameObject);

                toRemove.Add(obj);
            }
        }

        foreach (GameObject key in toRemove)
        {
            existingIcons.Remove(key);
        }

        if (sourceObjects == null || prefab == null || iconContainer == null)
            return;

        foreach (GameObject obj in sourceObjects)
        {
            if (obj == null)
                continue;

            if (!ShouldShowObject(obj))
                continue;

            if (existingIcons.ContainsKey(obj))
                continue;

            MinimapIconUI icon = Instantiate(prefab, iconContainer);
            existingIcons.Add(obj, icon);
        }
    }

    private void UpdateIconPositions(Dictionary<GameObject, MinimapIconUI> iconMap, bool isUnit)
    {
        if (iconContainer == null)
            return;

        float width = iconContainer.rect.width;
        float height = iconContainer.rect.height;
        Vector2 pivot = iconContainer.pivot;

        foreach (var pair in iconMap)
        {
            GameObject obj = pair.Key;
            MinimapIconUI icon = pair.Value;

            if (obj == null || icon == null)
                continue;

            Vector2 normalized = WorldToNormalizedMapPosition(obj.transform.position);

            normalized.x = Mathf.Clamp01(normalized.x);
            normalized.y = Mathf.Clamp01(normalized.y);

            Vector2 anchoredPosition = new Vector2(
                (normalized.x - pivot.x) * width,
                (normalized.y - pivot.y) * height
            );

            icon.SetAnchoredPosition(anchoredPosition);
            icon.SetColor(GetObjectColor(obj));

            if (isUnit)
                icon.SetSize(new Vector2(8f, 8f));
            else
                icon.SetSize(new Vector2(10f, 10f));
        }
    }

    private Vector2 WorldToNormalizedMapPosition(Vector3 worldPosition)
    {
        return mapData.WorldToMap01(worldPosition);
    }

    private bool ShouldShowObject(GameObject obj)
    {
        if (obj == null)
            return false;

        NetworkObject networkObject = obj.GetComponent<NetworkObject>();
        if (networkObject != null && !networkObject.IsSpawned)
            return false;

        if (useVisibilityFiltering)
        {
            // Za kasnije:
            // ovde ces vezati proveru za fog of war / vision sistem
            // trenutno vracamo true
            return true;
        }

        return true;
    }

    private Color GetObjectColor(GameObject obj)
    {
        IOwnedObject ownedObject = obj.GetComponent(typeof(IOwnedObject)) as IOwnedObject;

        if (ownedObject == null)
            return Color.white;

        return GetPlayerColor(ownedObject.OwnerClientId);
    }

    private Color GetPlayerColor(ulong clientId)
    {
        for (int i = 0; i < playerColors.Count; i++)
        {
            if (playerColors[i].ClientId == clientId)
                return playerColors[i].Color;
        }

        int fallbackIndex = (int)(clientId % 4);

        switch (fallbackIndex)
        {
            case 0: return fallbackColorA;
            case 1: return fallbackColorB;
            case 2: return fallbackColorC;
            case 3: return fallbackColorD;
            default: return Color.white;
        }
    }
}