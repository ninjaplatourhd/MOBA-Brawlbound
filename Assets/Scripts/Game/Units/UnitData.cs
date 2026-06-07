using System.Collections.Generic;
using UnityEngine;

public class UnitData : MonoBehaviour
{
    [Header("General")]
    public string UnitId = "super_heavy_tank_malj";
    public string DisplayName = "Super Heavy Tank (Malj)";

    [Header("Combat")]
    public bool MovesGun = false;
    public float Range = 50f;
    public float DamageBonus = 0f;
    public float Armor = 10f;
    public float AttackSpeed = 1f;

    [Header("Health")]
    public float MaxHealth = 100f;

    [Header("Movement")]
    public float MaxSpeed = 5f;
    public float MaxAngularSpeed = 120f;
    public float MaxAcceleration = 8f;

    [Header("Weapons")]
    public List<Weapon> Weapons = new List<Weapon>();

}

