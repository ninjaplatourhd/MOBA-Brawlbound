using System.Collections.Generic;
using UnityEngine;

public class BuildingData : MonoBehaviour
{
    [Header("General")]
    public string BuildingId = "komandni_centar";
    public string DisplayName = "Komandni Centar";

    [Header("Vision")]
    public float SightRadius = 60f;

    [Header("Health")]
    public float MaxHealth = 1000f;
    public float Armor = 20f;

    [Header("Production")]
    public bool CanBuildUnits = true;

    public List<BuildableUnit> BuildableUnits = new List<BuildableUnit>();

    [Header("UI")]
    public Sprite Icon;
}