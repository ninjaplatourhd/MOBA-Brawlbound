using UnityEngine;

public class UnitData : MonoBehaviour
{
    public float Range { get; set; } = 50;
    public float BaseDamage { get; set; } = 20;
    public float MaxHealth { get; set; } = 100;
    public float Health { get; set; } = 100;
    public float Armor { get; set; } = 10;
    public bool MovesGun { get; set; } = false;
    public float Speed { get; set; } = 0;
    public float MaxSpeed { get; set; } = 5;
    public float AngularSpeed { get; set; } = 0;
    public float MaxAngularSpeed { get; set; } = 3;
    public float Acceleration { get; set; } = 0;
    public float MaxAcceleration { get; set; } = 1;
    public string WeaponTypeName { get; set; } = "Cannon";
    public float AttackSpeed { get; set; } = 60;

}

