using Unity.Netcode;
using UnityEngine;

public class Building : NetworkBehaviour, ISelectableObject, IDamageable, IOwnedObject
{
    public bool Selected => BuildingManager.instance != null &&
                            BuildingManager.instance.SelectedBuildings.Contains(gameObject);

    [SerializeField] private GameObject _markerObject;

    public ulong OwnerClientId => PlayerClientId.Value;
    public BuildingData Data { get; private set; }

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
        Data = GetComponent<BuildingData>();
    }

    public override void OnNetworkSpawn()
    {
        if (Data == null)
            Data = GetComponent<BuildingData>();

        if (Data == null)
        {
            Debug.LogError($"{gameObject.name} nema BuildingData.");
            return;
        }

        if (IsServer)
        {
            MaxHealth.Value = Data.MaxHealth;
            Health.Value = Data.MaxHealth;
        }

        if (BuildingManager.instance != null)
        {
            BuildingManager.instance.AllBuildingsList.Add(gameObject);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (BuildingManager.instance != null)
        {
            BuildingManager.instance.AllBuildingsList.Remove(gameObject);
            BuildingManager.instance.SelectedBuildings.Remove(gameObject);
        }
    }

    public bool BelongsToLocalPlayer()
    {
        if (NetworkManager.Singleton == null)
            return false;

        return PlayerClientId.Value == NetworkManager.Singleton.LocalClientId;
    }

    public void Damage(float amount, Unit attacker)
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

    public void Select()
    {
        if (_markerObject != null)
            _markerObject.SetActive(true);
    }

    public void DeSelect()
    {
        if (_markerObject != null)
            _markerObject.SetActive(false);
    }
}