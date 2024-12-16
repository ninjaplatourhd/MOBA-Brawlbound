using System.Collections.Generic;
using UnityEngine;

public class UnitManager : MonoBehaviour
{

    public static UnitManager instance { get; set; }


    public List<GameObject> AllUnitsList = new List<GameObject>();

    public List<GameObject> SelectedUnits = new List<GameObject>();

    private Camera _camera;

    [SerializeField]
    private LayerMask _clickable;
    [SerializeField]
    private LayerMask _ground;

    [SerializeField]
    private string _player;

    public GameObject groundMarker;


    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
        }

    }
    void Start()
    {
        _camera = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, _clickable))
            {

                if (hit.collider.gameObject.TryGetComponent<Unit>(out Unit unit))
                {
                    if (unit.Player == _player)
                        SelectUnit(hit.collider.gameObject);
                }

            }
            else
            {

                DeSelectAll();

            }
        }
    }

    public void DeSelectAll()
    {
        foreach (GameObject unit in SelectedUnits)
        {
            if (unit.TryGetComponent<ISelectableObject>(out ISelectableObject selectable))
            {
                selectable.DeSelect();
            }
        }
        SelectedUnits.Clear();
    }

    public void SelectUnit(GameObject unit)
    {
        if (Input.GetKey(KeyCode.LeftControl) == false)
            DeSelectAll();
        SelectedUnits.Add(unit);
        if (unit.TryGetComponent<ISelectableObject>(out ISelectableObject selectable))
        {
            selectable.Select();
        }
    }

    public void SelectUnits(List<GameObject> units)
    {
        DeSelectAll();
        foreach (GameObject unit in units)
        {
            SelectedUnits.Add(unit);
            if (unit.TryGetComponent<ISelectableObject>(out ISelectableObject selectable))
            {
                selectable.Select();
            }
        }
    }
}
