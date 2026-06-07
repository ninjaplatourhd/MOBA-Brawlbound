using System;

[Serializable]
public class Weapon
{
    public string Name;
    public float Damage = 20f;
    public float FireRate = 45f;
    public float Range = 50f;
    public float RotationSpeed = 8f;
    public float FiringArc = 25f;
    public DamageType DamageType = DamageType.Kinetic;
    public ProjectileType ProjectileType = ProjectileType.Projectile;
    public string ProjectileName = "VelikiMetak12";
}