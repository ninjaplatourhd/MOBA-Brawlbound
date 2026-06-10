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
        if (InputBlocker.IsPointerOverUI())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, _clickable))
            {

                if (hit.collider.gameObject.TryGetComponent<Unit>(out Unit unit))
                {
                    if (unit.BelongsToLocalPlayer())
                    {
                        SelectUnit(hit.collider.gameObject);
                    }
                }
                else
                {
                    Building building = hit.collider.GetComponentInParent<Building>();

                    if (building != null && building.BelongsToLocalPlayer())
                    {
                        DeSelectAll();

                        if (BuildingManager.instance != null)
                        {
                            BuildingManager.instance.SelectBuilding(building.gameObject);
                        }

                        return;
                    }
                }

            }
            else
            {

                DeSelectAll();
                BuildingManager.instance.DeSelectAll();
                IngameConsole.print(SelectedUnits.Count);
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit targetHit, Mathf.Infinity, _clickable))
            {
                if (TryGetOwnedObjectFromHit(targetHit, out GameObject targetObject, out IOwnedObject ownedObject))
                {
                    if (!ownedObject.BelongsToLocalPlayer())
                    {
                        AttackSelectedUnits(targetObject);
                        return;
                    }
                }
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, Mathf.Infinity, _ground))
            {
                MoveSelectedUnits(groundHit.point);
            }
        }
        /*
                if (Input.GetKeyDown(KeyCode.P))
                {
                    GameObject prefab = Resources.Load<GameObject>("Prefabs/Units/Super heavy tank (Malj)");
                    if (prefab != null)
                    {
                        prefab.GetComponent<Unit>().Player = _player;
                        RaycastHit hit;
                        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
                        if (Physics.Raycast(ray, out hit, Mathf.Infinity, _ground))
                        {
                            Instantiate(prefab, hit.point, Quaternion.identity);
                        }

                    }
                    else
                    {
                        IngameConsole.print("Nije pronadjen tenk");
                    }

                }*/
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

    //public void SelectUnit(GameObject unit)
    //{
    //    if (Input.GetKey(KeyCode.LeftControl) == false)
    //        DeSelectAll();
    //    SelectedUnits.Add(unit);
    //    if (unit.TryGetComponent<ISelectableObject>(out ISelectableObject selectable))
    //    {
    //        selectable.Select();
    //    }
    //}

    public void SelectUnit(GameObject unit, bool additive = false)
    {
        if (!additive)
            DeSelectAll();

        SelectedUnits.Add(unit);

        if (unit.TryGetComponent<ISelectableObject>(out ISelectableObject selectable))
            selectable.Select();
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

    public Vector3 GetSelectedUnitCenter()
    {
        Vector3 positionCenter = new Vector3(0, 0, 0);

        foreach (var unit in SelectedUnits)
        {
            positionCenter += unit.gameObject.transform.position;
        }

        positionCenter /= SelectedUnits.Count;
        positionCenter.y = 0;
        return positionCenter;
    }

    private void AttackSelectedUnits(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        foreach (GameObject unitObj in SelectedUnits)
        {
            if (unitObj == null)
                continue;

            if (unitObj.TryGetComponent<UnitCombat>(out UnitCombat combat))
            {
                combat.RequestAttack(targetObject);
            }
        }
    }

    private bool TryGetOwnedObjectFromHit(RaycastHit hit, out GameObject targetObject, out IOwnedObject ownedObject)
    {
        targetObject = null;
        ownedObject = null;

        Unit unit = hit.collider.GetComponentInParent<Unit>();

        if (unit != null)
        {
            targetObject = unit.gameObject;
            ownedObject = unit;
            return true;
        }

        Building building = hit.collider.GetComponentInParent<Building>();

        if (building != null)
        {
            targetObject = building.gameObject;
            ownedObject = building;
            return true;
        }

        return false;
    }
    private void MoveSelectedUnits(Vector3 targetPoint)
    {
        if (SelectedUnits.Count == 0)
            return;

        Vector3 centerPosition = GetSelectedUnitCenter();

        foreach (GameObject unitObj in SelectedUnits)
        {
            Vector3 positionDiff = unitObj.transform.position - centerPosition;
            positionDiff.y = 0;

            Vector3 targetPosition = targetPoint + positionDiff;

            if (unitObj.TryGetComponent<UnitMovement>(out UnitMovement movement))
            {
                movement.RequestMove(targetPosition);
            }
        }
    }
}
