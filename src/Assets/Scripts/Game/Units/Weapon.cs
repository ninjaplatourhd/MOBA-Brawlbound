using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Weapon
{
    public string Name;

    [Header("Stats")]
    public float Damage = 20f;
    public float FireRate = 1f;
    public float Range = 50f;
    public float RotationSpeed = 90f;
    public float FiringArc = 25f;

    [Header("Aiming")]
    public bool MovesGun = false;
    public Transform GunPivot;
    public float WeaponYawOffset = 0f;

    [Header("Barrels")]
    public List<Transform> Barrels = new List<Transform>();

    [Header("Projectile")]
    public DamageType DamageType = DamageType.Kinetic;
    public ProjectileType ProjectileType = ProjectileType.Projectile;
    public string ProjectileName = "VelikiMetak12";

    public float ProjectileSpeed = 35f;
    public float ProjectileRadius = 0.4f;
    public float ProjectileLifeTime = 5f;
}