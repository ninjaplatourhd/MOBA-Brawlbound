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

    [Header("Known Enemy Building Visual")]
    [SerializeField] private float knownBuildingAlpha = 0.45f;

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

        if (refreshTimer < refreshInterval)
            return;

        refreshTimer = 0f;

        RefreshMinimap();
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

        SyncIcons(units, unitIcons, unitIconPrefab, true);
        UpdateIconPositions(unitIcons, true);
    }

    private void RefreshBuildingIcons()
    {
        List<GameObject> buildings = BuildingManager.instance != null
            ? BuildingManager.instance.AllBuildingsList
            : null;

        SyncIcons(buildings, buildingIcons, buildingIconPrefab, false);
        UpdateIconPositions(buildingIcons, false);
    }

    private void SyncIcons(
        List<GameObject> sourceObjects,
        Dictionary<GameObject, MinimapIconUI> existingIcons,
        MinimapIconUI prefab,
        bool isUnit)
    {
        List<GameObject> toRemove = new List<GameObject>();

        foreach (var pair in existingIcons)
        {
            GameObject obj = pair.Key;

            if (obj == null || sourceObjects == null || !sourceObjects.Contains(obj) || !ShouldShowObject(obj, isUnit))
            {
                if (pair.Value != null)
                    Destroy(pair.Value.gameObject);

                toRemove.Add(obj);
            }
        }

        foreach (GameObject obj in toRemove)
        {
            existingIcons.Remove(obj);
        }

        if (sourceObjects == null || prefab == null || iconContainer == null)
            return;

        foreach (GameObject obj in sourceObjects)
        {
            if (obj == null)
                continue;

            if (!ShouldShowObject(obj, isUnit))
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

            Vector2 normalized = mapData.WorldToNormalizedMapPosition(obj.transform.position);

            normalized.x = Mathf.Clamp01(normalized.x);
            normalized.y = Mathf.Clamp01(normalized.y);

            Vector2 anchoredPosition = new Vector2(
                (normalized.x - pivot.x) * width,
                (normalized.y - pivot.y) * height
            );

            icon.SetAnchoredPosition(anchoredPosition);

            Color color = GetObjectColor(obj, isUnit);
            icon.SetColor(color);

            if (isUnit)
                icon.SetSize(new Vector2(8f, 8f));
            else
                icon.SetSize(new Vector2(10f, 10f));
        }
    }

    private bool ShouldShowObject(GameObject obj, bool isUnit)
    {
        NetworkObject networkObject = obj.GetComponent<NetworkObject>();

        if (networkObject != null && !networkObject.IsSpawned)
            return false;

        FogOfWar fog = FogOfWar.Instance;

        if (fog == null)
            return true;

        if (isUnit)
            return fog.ShouldShowUnit(obj);

        return fog.ShouldShowBuilding(obj);
    }

    private Color GetObjectColor(GameObject obj, bool isUnit)
    {
        IOwnedObject ownedObject = obj.GetComponent<IOwnedObject>();

        if (ownedObject == null)
            return Color.white;

        Color color = GetPlayerColor(ownedObject.OwnerClientId);

        FogOfWar fog = FogOfWar.Instance;

        if (fog != null && !isUnit)
        {
            bool friendly = fog.IsFriendly(ownedObject);
            bool visibleNow = fog.IsVisibleNow(obj.transform.position);
            bool knownBuilding = fog.IsKnownEnemyBuilding(obj);

            if (!friendly && knownBuilding && !visibleNow)
            {
                color.a = knownBuildingAlpha;
            }
        }

        color.a = Mathf.Clamp01(color.a);

        return color;
    }

    private Color GetPlayerColor(ulong clientId)
    {
        for (int i = 0; i < playerColors.Count; i++)
        {
            if (playerColors[i].ClientId == clientId)
            {
                Color color = playerColors[i].Color;
                color.a = 1f;
                return color;
            }
        }

        Color fallbackColor;

        int fallbackIndex = (int)(clientId % 4);

        switch (fallbackIndex)
        {
            case 0:
                fallbackColor = fallbackColorA;
                break;
            case 1:
                fallbackColor = fallbackColorB;
                break;
            case 2:
                fallbackColor = fallbackColorC;
                break;
            case 3:
                fallbackColor = fallbackColorD;
                break;
            default:
                fallbackColor = Color.white;
                break;
        }

        fallbackColor.a = 1f;
        return fallbackColor;
    }
}