using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MinimapUIController : MonoBehaviour
{
    private enum MinimapIconType
    {
        Unit,
        Building,
        Mineral
    }

    [Header("References")]
    [SerializeField] private MapData mapData;
    [SerializeField] private RectTransform iconContainer;

    [Header("Prefabs")]
    [SerializeField] private MinimapIconUI unitIconPrefab;
    [SerializeField] private MinimapIconUI buildingIconPrefab;
    [SerializeField] private MinimapIconUI mineralIconPrefab;

    [Header("Update")]
    [SerializeField] private float refreshInterval = 0.05f;
    [SerializeField] private float mineralSearchInterval = 1f;

    [Header("Colors")]
    [SerializeField] private Color mineralColor = Color.cyan;

    [Header("Minerals")]
    [SerializeField] private bool showMineralsThroughFog = true;

    [Header("Known Enemy Building Visual")]
    [SerializeField] private float knownBuildingAlpha = 0.45f;

    [Header("Icon Sizes")]
    [SerializeField] private Vector2 unitIconSize = new Vector2(8f, 8f);
    [SerializeField] private Vector2 buildingIconSize = new Vector2(10f, 10f);
    [SerializeField] private Vector2 mineralIconSize = new Vector2(7f, 7f);

    private readonly Dictionary<GameObject, MinimapIconUI> unitIcons = new Dictionary<GameObject, MinimapIconUI>();
    private readonly Dictionary<GameObject, MinimapIconUI> buildingIcons = new Dictionary<GameObject, MinimapIconUI>();
    private readonly Dictionary<GameObject, MinimapIconUI> mineralIcons = new Dictionary<GameObject, MinimapIconUI>();

    private readonly List<GameObject> mineralObjects = new List<GameObject>();

    private float refreshTimer;
    private float mineralSearchTimer;

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

        RefreshMineralCache();

        RefreshUnitIcons();
        RefreshBuildingIcons();
        RefreshMineralIcons();
    }

    private void RefreshUnitIcons()
    {
        List<GameObject> units = UnitManager.instance != null
            ? UnitManager.instance.AllUnitsList
            : null;

        SyncIcons(units, unitIcons, unitIconPrefab, MinimapIconType.Unit);
        UpdateIconPositions(unitIcons, MinimapIconType.Unit);
    }

    private void RefreshBuildingIcons()
    {
        List<GameObject> buildings = BuildingManager.instance != null
            ? BuildingManager.instance.AllBuildingsList
            : null;

        SyncIcons(buildings, buildingIcons, buildingIconPrefab, MinimapIconType.Building);
        UpdateIconPositions(buildingIcons, MinimapIconType.Building);
    }

    private void RefreshMineralIcons()
    {
        MinimapIconUI prefab = mineralIconPrefab != null
            ? mineralIconPrefab
            : buildingIconPrefab;

        SyncIcons(mineralObjects, mineralIcons, prefab, MinimapIconType.Mineral);
        UpdateIconPositions(mineralIcons, MinimapIconType.Mineral);
    }

    private void RefreshMineralCache()
    {
        mineralSearchTimer += refreshInterval;

        if (mineralSearchTimer < mineralSearchInterval && mineralObjects.Count > 0)
            return;

        mineralSearchTimer = 0f;
        mineralObjects.Clear();

        MineralCrystal[] minerals = FindObjectsByType<MineralCrystal>(FindObjectsSortMode.None);

        foreach (MineralCrystal mineral in minerals)
        {
            if (mineral == null)
                continue;

            mineralObjects.Add(mineral.gameObject);
        }
    }

    private void SyncIcons(
        List<GameObject> sourceObjects,
        Dictionary<GameObject, MinimapIconUI> existingIcons,
        MinimapIconUI prefab,
        MinimapIconType iconType)
    {
        List<GameObject> toRemove = new List<GameObject>();

        foreach (var pair in existingIcons)
        {
            GameObject obj = pair.Key;

            if (obj == null ||
                sourceObjects == null ||
                !sourceObjects.Contains(obj) ||
                !ShouldShowObject(obj, iconType))
            {
                if (pair.Value != null)
                    Destroy(pair.Value.gameObject);

                toRemove.Add(obj);
            }
        }

        foreach (GameObject obj in toRemove)
            existingIcons.Remove(obj);

        if (sourceObjects == null || prefab == null || iconContainer == null)
            return;

        foreach (GameObject obj in sourceObjects)
        {
            if (obj == null)
                continue;

            if (!ShouldShowObject(obj, iconType))
                continue;

            if (existingIcons.ContainsKey(obj))
                continue;

            MinimapIconUI icon = Instantiate(prefab, iconContainer);
            existingIcons.Add(obj, icon);
        }
    }

    private void UpdateIconPositions(
        Dictionary<GameObject, MinimapIconUI> iconMap,
        MinimapIconType iconType)
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
            icon.SetColor(GetObjectColor(obj, iconType));
            icon.SetSize(GetIconSize(iconType));
        }
    }

    private bool ShouldShowObject(GameObject obj, MinimapIconType iconType)
    {
        if (obj == null)
            return false;

        NetworkObject networkObject = obj.GetComponent<NetworkObject>();

        if (networkObject != null && !networkObject.IsSpawned)
            return false;

        if (iconType == MinimapIconType.Mineral)
        {
            if (showMineralsThroughFog)
                return true;

            FogOfWar fog = FogOfWar.Instance;

            if (fog == null)
                return true;

            return fog.IsVisibleNow(obj.transform.position);
        }

        FogOfWar fogOfWar = FogOfWar.Instance;

        if (fogOfWar == null)
            return true;

        if (iconType == MinimapIconType.Unit)
            return fogOfWar.ShouldShowUnit(obj);

        return fogOfWar.ShouldShowBuilding(obj);
    }

    private Color GetObjectColor(GameObject obj, MinimapIconType iconType)
    {
        if (iconType == MinimapIconType.Mineral)
            return mineralColor;

        IOwnedObject ownedObject = obj.GetComponent<IOwnedObject>();

        if (ownedObject == null)
            return Color.white;

        Color color = PlayerRegistry.GetPlayerColor(ownedObject.OwnerClientId);
        color.a = 1f;

        FogOfWar fog = FogOfWar.Instance;

        if (fog != null && iconType == MinimapIconType.Building)
        {
            bool friendly = fog.IsFriendly(ownedObject);
            bool visibleNow = fog.IsVisibleNow(obj.transform.position);
            bool knownBuilding = fog.IsKnownEnemyBuilding(obj);

            if (!friendly && knownBuilding && !visibleNow)
                color.a = knownBuildingAlpha;
        }

        color.a = Mathf.Clamp01(color.a);
        return color;
    }

    private Vector2 GetIconSize(MinimapIconType iconType)
    {
        switch (iconType)
        {
            case MinimapIconType.Unit:
                return unitIconSize;

            case MinimapIconType.Building:
                return buildingIconSize;

            case MinimapIconType.Mineral:
                return mineralIconSize;

            default:
                return unitIconSize;
        }
    }
}