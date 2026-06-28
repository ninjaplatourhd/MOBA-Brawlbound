using System;
using UnityEngine;

[Serializable]
public class BuildableUnit
{
    [Header("Identity")]
    public string UnitId = "worker";
    public string DisplayName = "Worker";
    public Sprite Icon;

    [Header("Result")]
    public GameObject UnitPrefab;

    [Header("Cost")]
    public int MineralCost = 50;
    public int PowerCost = 10;
    public int PowerUpkeep = 1;

    [Header("Requirements")]
    public int RequiredTechTier = 1;

    [Header("Production")]
    public float BuildTime = 5f;
}