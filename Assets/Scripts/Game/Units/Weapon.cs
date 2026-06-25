using System;

[Serializable]
public class Weapon
{
    public string Name;
    public float Damage = 20f;
    public float FireRate = 1f;
    public float Range = 50f;
    public float RotationSpeed = 90f;
    public float FiringArc = 25f;
    public float WeaponYawOffset = 0f;

    public DamageType DamageType = DamageType.Kinetic;
    public ProjectileType ProjectileType = ProjectileType.Projectile;

    public string ProjectileName = "VelikiMetak12";

    public float ProjectileSpeed = 35f;
    public float ProjectileRadius = 0.4f;
    public float ProjectileLifeTime = 5f;
}