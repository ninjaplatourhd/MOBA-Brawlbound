using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Unit : NetworkBehaviour, ISelectableObject, IDamageable
{
    public bool Selected => UnitManager.instance != null &&
                            UnitManager.instance.SelectedUnits.Contains(gameObject);

    [SerializeField] private GameObject _markerObject;
    [SerializeField] private List<Transform> _barrels = new List<Transform>();
    [SerializeField] private Transform _gunPivot;

    public Transform GunPivot => _gunPivot;
    public IReadOnlyList<Transform> Barrels => _barrels;

    public UnitData Data { get; private set; }

    public NetworkVariable<ulong> PlayerClientId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> Health = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> MaxHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );



    private void Awake()
    {
        Data = GetComponent<UnitData>();
    }

    public override void OnNetworkSpawn()
    {
        if (Data == null)
            Data = GetComponent<UnitData>();

        if (IsServer)
        {
            MaxHealth.Value = Data.MaxHealth;
            Health.Value = Data.MaxHealth;
        }

        if (UnitManager.instance != null)
        {
            UnitManager.instance.AllUnitsList.Add(gameObject);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (UnitManager.instance != null)
        {
            UnitManager.instance.AllUnitsList.Remove(gameObject);
            UnitManager.instance.SelectedUnits.Remove(gameObject);
        }
    }

    public bool BelongsToLocalPlayer()
    {
        if (NetworkManager.Singleton == null)
            return false;

        return PlayerClientId.Value == NetworkManager.Singleton.LocalClientId;
    }

    public void Damage(float amount)
    {
        if (!IsServer)
            return;

        float armor = Data != null ? Data.Armor : 0f;
        float reducedDamage = Mathf.Max(1f, amount - armor);

        Health.Value = Mathf.Max(0f, Health.Value - reducedDamage);

        if (Health.Value <= 0f)
        {
            Die();
        }
    }

    public void Die()
    {
        if (!IsServer)
            return;

        NetworkObject.Despawn(true);
    }

    public void DeSelect()
    {
        if (_markerObject != null)
            _markerObject.SetActive(false);
    }

    public void Select()
    {
        if (_markerObject != null)
            _markerObject.SetActive(true);
    }
}