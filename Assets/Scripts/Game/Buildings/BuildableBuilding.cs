using System;
using UnityEngine;

[Serializable]
public class BuildableBuilding
{
    [Header("Identity")]
    public string BuildingId = "coal_powerplant";
    public string DisplayName = "Coal Powerplant";
    public Sprite Icon;

    [Header("Result")]
    public GameObject FinalBuildingPrefab;
    public GameObject ConstructionSitePrefab;
    public GameObject PreviewPrefab;

    [Header("Cost")]
    public int MineralCost = 150;
    public int RequiredFreePower = 0;
    public int PowerUpkeep = 0;

    [Header("Requirements")]
    public int RequiredTechTier = 1;

    [Header("Construction")]
    public float BuildTime = 25f;
    public Vector2 FootprintSize = new Vector2(6f, 6f);

    [Header("Placement")]
    public bool RequiresVisibleArea = true;
}