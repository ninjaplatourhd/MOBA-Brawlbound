using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class FogOfWar : MonoBehaviour
{
    public static FogOfWar Instance { get; private set; }

    [Header("References")]
    [SerializeField] private MapData mapData;
    [SerializeField] private RawImage minimapFogImage;

    [Header("Texture")]
    [SerializeField] private int textureSize = 256;
    [SerializeField] private float updateInterval = 0.1f;

    [Header("Sight Falloff")]
    [SerializeField] private float fullVisionPercent = 0.8f;

    [Header("Fog Colors")]
    [SerializeField] private Color unexploredColor = new Color(0f, 0f, 0f, 0.95f);
    [SerializeField] private Color exploredColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color visibleColor = new Color(0f, 0f, 0f, 0f);

    [Header("Fallback Sight")]
    [SerializeField] private float defaultUnitSightRadius = 40f;
    [SerializeField] private float defaultBuildingSightRadius = 60f;

    [Header("Debug")]
    [SerializeField] private bool debugFog = false;

    private Texture2D fogTexture;
    private Color32[] texturePixels;

    private byte[] visibleNow;
    private byte[] explored;

    private readonly HashSet<ulong> knownEnemyBuildingIds = new HashSet<ulong>();

    private float updateTimer;

    public Texture2D FogTexture => fogTexture;

    private void Awake()
    {
        Instance = this;

        if (mapData == null)
            mapData = MapData.Instance;

        CreateFogTexture();
    }

    private void Start()
    {
        if (minimapFogImage != null)
            minimapFogImage.texture = fogTexture;

        RebuildFog();
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;

        if (updateTimer < updateInterval)
            return;

        updateTimer = 0f;
        RebuildFog();
    }

    private void CreateFogTexture()
    {
        textureSize = Mathf.Max(32, textureSize);

        fogTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        fogTexture.filterMode = FilterMode.Bilinear;
        fogTexture.wrapMode = TextureWrapMode.Clamp;

        texturePixels = new Color32[textureSize * textureSize];
        visibleNow = new byte[textureSize * textureSize];
        explored = new byte[textureSize * textureSize];

        for (int i = 0; i < texturePixels.Length; i++)
        {
            texturePixels[i] = unexploredColor;
            visibleNow[i] = 0;
            explored[i] = 0;
        }

        fogTexture.SetPixels32(texturePixels);
        fogTexture.Apply(false);
    }

    private void RebuildFog()
    {
        if (mapData == null)
            return;

        ClearVisibleNow();

        AddFriendlyUnitVision();
        AddFriendlyBuildingVision();

        ApplyVisibleToExplored();
        UpdateKnownEnemyBuildings();
        UpdateTexture();

        if (debugFog)
            Debug.Log("Fog rebuilt.");
    }

    private void ClearVisibleNow()
    {
        for (int i = 0; i < visibleNow.Length; i++)
        {
            visibleNow[i] = 0;
        }
    }

    private void AddFriendlyUnitVision()
    {
        if (UnitManager.instance == null)
            return;

        foreach (GameObject unitObj in UnitManager.instance.AllUnitsList)
        {
            if (unitObj == null)
                continue;

            Unit unit = unitObj.GetComponent<Unit>();

            if (unit == null)
                continue;

            if (!IsFriendly(unit))
                continue;

            float sightRadius = GetUnitSightRadius(unitObj);

            RevealCircle(unitObj.transform.position, sightRadius);
        }
    }

    private void AddFriendlyBuildingVision()
    {
        if (BuildingManager.instance == null)
            return;

        foreach (GameObject buildingObj in BuildingManager.instance.AllBuildingsList)
        {
            if (buildingObj == null)
                continue;

            Building building = buildingObj.GetComponent<Building>();

            if (building == null)
                continue;

            if (!IsFriendly(building))
                continue;

            float sightRadius = GetBuildingSightRadius(buildingObj);

            RevealCircle(buildingObj.transform.position, sightRadius);
        }
    }

    private void RevealCircle(Vector3 worldPosition, float radius)
    {
        if (radius <= 0f)
            return;

        Vector2 centerNormalized = mapData.WorldToNormalizedMapPosition(worldPosition);

        int centerX = Mathf.RoundToInt(centerNormalized.x * (textureSize - 1));
        int centerY = Mathf.RoundToInt(centerNormalized.y * (textureSize - 1));

        Vector2 mapSize = mapData.MapSize;

        float pixelWorldSizeX = mapSize.x / textureSize;
        float pixelWorldSizeY = mapSize.y / textureSize;

        int radiusPixelsX = Mathf.CeilToInt(radius / pixelWorldSizeX);
        int radiusPixelsY = Mathf.CeilToInt(radius / pixelWorldSizeY);

        int minX = Mathf.Clamp(centerX - radiusPixelsX, 0, textureSize - 1);
        int maxX = Mathf.Clamp(centerX + radiusPixelsX, 0, textureSize - 1);
        int minY = Mathf.Clamp(centerY - radiusPixelsY, 0, textureSize - 1);
        int maxY = Mathf.Clamp(centerY + radiusPixelsY, 0, textureSize - 1);

        Vector2 sourceXZ = new Vector2(worldPosition.x, worldPosition.z);
        float fullVisionRadius = radius * fullVisionPercent;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 normalized = new Vector2(
                    x / (float)(textureSize - 1),
                    y / (float)(textureSize - 1)
                );

                Vector3 pixelWorldPosition = mapData.NormalizedMapPositionToWorld(normalized, worldPosition.y);
                Vector2 pixelXZ = new Vector2(pixelWorldPosition.x, pixelWorldPosition.z);

                float distance = Vector2.Distance(sourceXZ, pixelXZ);

                if (distance > radius)
                    continue;

                float visibilityStrength;

                if (distance <= fullVisionRadius)
                {
                    visibilityStrength = 1f;
                }
                else
                {
                    float fadeRange = radius - fullVisionRadius;
                    float fadeT = fadeRange <= 0.01f ? 1f : (distance - fullVisionRadius) / fadeRange;
                    visibilityStrength = 1f - Mathf.Clamp01(fadeT);
                }

                byte value = (byte)Mathf.RoundToInt(visibilityStrength * 255f);
                int index = GetIndex(x, y);

                if (value > visibleNow[index])
                    visibleNow[index] = value;
            }
        }
    }

    private void ApplyVisibleToExplored()
    {
        for (int i = 0; i < visibleNow.Length; i++)
        {
            if (visibleNow[i] > 0)
                explored[i] = 255;
        }
    }

    private void UpdateKnownEnemyBuildings()
    {
        if (BuildingManager.instance == null)
            return;

        foreach (GameObject buildingObj in BuildingManager.instance.AllBuildingsList)
        {
            if (buildingObj == null)
                continue;

            Building building = buildingObj.GetComponent<Building>();
            NetworkObject networkObject = buildingObj.GetComponent<NetworkObject>();

            if (building == null || networkObject == null)
                continue;

            if (IsFriendly(building))
                continue;

            if (IsVisibleNow(buildingObj.transform.position))
            {
                knownEnemyBuildingIds.Add(networkObject.NetworkObjectId);
            }
        }
    }

    private void UpdateTexture()
    {
        for (int i = 0; i < texturePixels.Length; i++)
        {
            float visibleStrength = visibleNow[i] / 255f;

            Color baseColor = explored[i] > 0
                ? exploredColor
                : unexploredColor;

            Color finalColor = Color.Lerp(baseColor, visibleColor, visibleStrength);

            texturePixels[i] = finalColor;
        }

        fogTexture.SetPixels32(texturePixels);
        fogTexture.Apply(false);
    }

    public bool IsVisibleNow(Vector3 worldPosition)
    {
        if (!TryWorldToTextureIndex(worldPosition, out int index))
            return false;

        return visibleNow[index] > 5;
    }

    public bool IsExplored(Vector3 worldPosition)
    {
        if (!TryWorldToTextureIndex(worldPosition, out int index))
            return false;

        return explored[index] > 0;
    }

    public bool IsKnownEnemyBuilding(GameObject buildingObj)
    {
        if (buildingObj == null)
            return false;

        NetworkObject networkObject = buildingObj.GetComponent<NetworkObject>();

        if (networkObject == null)
            return false;

        return knownEnemyBuildingIds.Contains(networkObject.NetworkObjectId);
    }

    public bool ShouldShowUnit(GameObject unitObj)
    {
        if (unitObj == null)
            return false;

        Unit unit = unitObj.GetComponent<Unit>();

        if (unit == null)
            return false;

        if (IsFriendly(unit))
            return true;

        return IsVisibleNow(unitObj.transform.position);
    }

    public bool ShouldShowBuilding(GameObject buildingObj)
    {
        if (buildingObj == null)
            return false;

        Building building = buildingObj.GetComponent<Building>();

        if (building == null)
            return false;

        if (IsFriendly(building))
            return true;

        if (IsVisibleNow(buildingObj.transform.position))
            return true;

        return IsKnownEnemyBuilding(buildingObj);
    }

    public bool IsFriendly(IOwnedObject ownedObject)
    {
        if (ownedObject == null)
            return false;

        return ownedObject.OwnerClientId == GetLocalClientId();
    }

    public ulong GetLocalClientId()
    {
        if (NetworkManager.Singleton == null)
            return 0;

        return NetworkManager.Singleton.LocalClientId;
    }

    private bool TryWorldToTextureIndex(Vector3 worldPosition, out int index)
    {
        Vector2 normalized = mapData.WorldToNormalizedMapPosition(worldPosition);

        if (normalized.x < 0f || normalized.x > 1f || normalized.y < 0f || normalized.y > 1f)
        {
            index = -1;
            return false;
        }

        int x = Mathf.Clamp(Mathf.RoundToInt(normalized.x * (textureSize - 1)), 0, textureSize - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(normalized.y * (textureSize - 1)), 0, textureSize - 1);

        index = GetIndex(x, y);
        return true;
    }

    private int GetIndex(int x, int y)
    {
        return y * textureSize + x;
    }

    private float GetUnitSightRadius(GameObject unitObj)
    {
        UnitData unitData = unitObj.GetComponent<UnitData>();

        if (unitData != null)
            return unitData.SightRadius;

        return defaultUnitSightRadius;
    }

    private float GetBuildingSightRadius(GameObject buildingObj)
    {
        BuildingData buildingData = buildingObj.GetComponent<BuildingData>();

        if (buildingData != null)
            return buildingData.SightRadius;

        return defaultBuildingSightRadius;
    }
}