using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Unit : NetworkBehaviour, ISelectableObject, IDamageable, IOwnedObject
{
    public bool Selected => UnitManager.instance != null &&
                            UnitManager.instance.SelectedUnits.Contains(gameObject);

    [SerializeField] private GameObject _markerObject;
    [SerializeField] private List<Transform> _barrels = new List<Transform>();
    [SerializeField] private Transform _gunPivot;
    [SerializeField] private List<GameObject> playerColorObjects;
    [SerializeField] private string colorPropertyName = "_BaseColor";

    public ulong OwnerClientId => PlayerClientId.Value;
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

        PlayerClientId.OnValueChanged += HandleOwnerChanged;

        RefreshPlayerColor();
    }

    public override void OnNetworkDespawn()
    {
        PlayerClientId.OnValueChanged -= HandleOwnerChanged;

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

    public void Damage(float amount, Unit attacker)
    {
        if (!IsServer)
            return;

        float armor = Data != null ? Data.Armor : 0f;
        float reducedDamage = Mathf.Max(1f, amount - armor);

        Health.Value = Mathf.Max(0f, Health.Value - reducedDamage);

        UnitCombat combat = GetComponent<UnitCombat>();

        if (combat != null && attacker != null && attacker != this)
        {
            combat.ServerAggroOn(attacker);
        }

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

        RefreshPlayerColor();
    }

    private void HandleOwnerChanged(ulong oldValue, ulong newValue)
    {
        RefreshPlayerColor();
    }

    public void RefreshPlayerColor()
    {
        Color color = PlayerRegistry.GetPlayerColor(PlayerClientId.Value);

        ApplyColorToGameObjects(playerColorObjects, color);

        if (_markerObject != null)
            ApplyColorToGameObject(_markerObject, color);
    }

    private void ApplyColorToGameObjects(List<GameObject> objects, Color color)
    {
        foreach (GameObject obj in objects)
        {
            if (obj == null)
                continue;

            ApplyColorToGameObject(obj, color);
        }
    }

    private void ApplyColorToGameObject(GameObject obj, Color color)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                    continue;

                if (materials[i].HasProperty(colorPropertyName))
                    materials[i].SetColor(colorPropertyName, color);
                else if (materials[i].HasProperty("_Color"))
                    materials[i].SetColor("_Color", color);
            }
        }

        Image[] images = obj.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image != null)
                image.color = color;
        }
    }
}