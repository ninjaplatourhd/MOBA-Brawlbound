using System;
using UnityEngine;

[Serializable]
public class BuildableUnit
{
    public string UnitId = "worker";
    public string DisplayName = "Worker";
    public GameObject UnitPrefab;
    public float BuildTime = 5f;
}