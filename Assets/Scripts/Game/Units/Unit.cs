using Unity.Netcode;
using UnityEngine;

public class Unit : NetworkBehaviour, ISelectableObject
{
    public bool Selected => UnitManager.instance.SelectedUnits.Contains(gameObject);

    [SerializeField]
    private GameObject _markerObject;

    [SerializeField]
    public string Player;

    public NetworkVariable<ulong> PlayerClientId = new NetworkVariable<ulong>(
       0,
       NetworkVariableReadPermission.Everyone,
       NetworkVariableWritePermission.Server
    );

    public bool BelongsToLocalPlayer()
    {
        return PlayerClientId.Value == NetworkManager.Singleton.LocalClientId;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        UnitManager.instance.AllUnitsList.Add(gameObject);


    }

    private void OnDestroy()
    {
        UnitManager.instance.AllUnitsList.Remove(gameObject);

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void DeSelect()
    {
        _markerObject.SetActive(false);
    }

    public void Select()
    {
        _markerObject.SetActive(true);
    }
}
