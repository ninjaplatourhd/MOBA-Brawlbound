using System.Collections.Generic;
using UnityEngine;

public class MapData : MonoBehaviour
{
    public static MapData Instance { get; private set; }

    [Header("Starting Bases")]
    [SerializeField] public List<GameObject> StartingBaseLocations = new List<GameObject>();

    [Header("Map Bounds")]
    [SerializeField] public Vector2 MapOrigin = Vector2.zero;
    [SerializeField] public Vector2 MapSize = new Vector2(200f, 200f);

    public Vector2 MapMin => MapOrigin;
    public Vector2 MapMax => MapOrigin + MapSize;
    public Vector2 MapCenter => MapOrigin + MapSize * 0.5f;

    private void Awake()
    {
        Instance = this;
    }

    public Vector3 GetMapCenterWorld(float y = 0f)
    {
        return new Vector3(MapCenter.x, y, MapCenter.y);
    }

    public Vector3 ClampWorldPosition(Vector3 worldPosition, float padding = 0f)
    {
        Vector2 min = MapMin;
        Vector2 max = MapMax;

        worldPosition.x = Mathf.Clamp(worldPosition.x, min.x + padding, max.x - padding);
        worldPosition.z = Mathf.Clamp(worldPosition.z, min.y + padding, max.y - padding);

        return worldPosition;
    }

    public Vector2 WorldToMap01(Vector3 worldPosition)
    {
        float x = Mathf.InverseLerp(MapMin.x, MapMax.x, worldPosition.x);
        float y = Mathf.InverseLerp(MapMin.y, MapMax.y, worldPosition.z);

        return new Vector2(x, y);
    }

    public Vector3 Map01ToWorld(Vector2 map01, float y = 0f)
    {
        float worldX = Mathf.Lerp(MapMin.x, MapMax.x, map01.x);
        float worldZ = Mathf.Lerp(MapMin.y, MapMax.y, map01.y);

        return new Vector3(worldX, y, worldZ);
    }

    public GameObject GetStartingBaseLocation(int index)
    {
        if (StartingBaseLocations == null || StartingBaseLocations.Count == 0)
            return null;

        index = Mathf.Clamp(index, 0, StartingBaseLocations.Count - 1);
        return StartingBaseLocations[index];
    }

    public Vector2 WorldToNormalizedMapPosition(Vector3 worldPosition)
    {
        float x = Mathf.InverseLerp(MapMin.x, MapMax.x, worldPosition.x);
        float y = Mathf.InverseLerp(MapMin.y, MapMax.y, worldPosition.z);

        return new Vector2(x, y);
    }

    public Vector3 NormalizedMapPositionToWorld(Vector2 normalizedPosition, float y = 0f)
    {
        float worldX = Mathf.Lerp(MapMin.x, MapMax.x, normalizedPosition.x);
        float worldZ = Mathf.Lerp(MapMin.y, MapMax.y, normalizedPosition.y);

        return new Vector3(worldX, y, worldZ);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = new Vector3(
            MapOrigin.x + MapSize.x * 0.5f,
            0f,
            MapOrigin.y + MapSize.y * 0.5f
        );

        Vector3 size = new Vector3(MapSize.x, 1f, MapSize.y);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);
    }


}