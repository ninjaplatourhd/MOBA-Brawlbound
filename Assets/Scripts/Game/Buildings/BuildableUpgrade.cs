using System;
using UnityEngine;

[Serializable]
public class BuildableUpgrade
{
    [Header("Identity")]
    public string UpgradeId = "upgrade_id";
    public string DisplayName = "Upgrade";
    public Sprite Icon;

    [Header("Cost")]
    public int MineralCost = 100;
    public int RequiredFreePower = 0;

    [Header("Requirements")]
    public int RequiredTechTier = 1;

    [Header("Research")]
    public float ResearchTime = 10f;
}