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

    [SerializeField] private LayerMask resourceMask;

    public GameObject groundMarker;

    // upravljanje dugmadima za jedinice, da li su u move, attack ili patrol modu
    public CommandMode CurrentCommandMode = CommandMode.None;
    private bool _patrolMode;
    private Vector3 _patrolA;

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

        // selecting units
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

            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (TryHandleResourceRightClick(ray))
                return;


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
                if (_patrolMode)
                {
                    HandlePatrolClick(groundHit.point);
                    return;
                }

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

    // prebacio sam u public da mogu da pozovem preko dugmadi /Savo
    public void AttackSelectedUnits(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        foreach (GameObject unitObj in SelectedUnits)
        {
            if (unitObj == null)
                continue;

            CancelGatheringIfWorker(unitObj);

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

    // prebacio sam u public da mogu da pozovem preko dugmadi /Savo
    public void MoveSelectedUnits(Vector3 targetPoint)
    {
        if (SelectedUnits.Count == 0)
            return;

        Vector3 centerPosition = GetSelectedUnitCenter();

        foreach (GameObject unitObj in SelectedUnits)
        {
            Vector3 positionDiff = unitObj.transform.position - centerPosition;
            positionDiff.y = 0;

            Vector3 targetPosition = targetPoint + positionDiff;

            CancelGatheringIfWorker(unitObj);

            if (unitObj.TryGetComponent<UnitMovement>(out UnitMovement movement))
            {
                movement.RequestMove(targetPosition);
            }
        }
    }

    // Patrol hadle za klik /Savo
    private void HandlePatrolClick(Vector3 point)
    {
        if (!_patrolMode)
            return;

        if (_patrolA == Vector3.zero)
        {
            _patrolA = point;
            IngameConsole.print("Patrol Point A set");
            return;
        }

        foreach (var unitObj in SelectedUnits)
        {
            CancelGatheringIfWorker(unitObj);

            if (unitObj.TryGetComponent<UnitMovement>(out UnitMovement movement))
            {
                movement.RequestPatrol(_patrolA, point);
            }
        }

        _patrolMode = false;
        _patrolA = Vector3.zero;
    }

    public void EnablePatrolMode()
    {
        _patrolMode = true;
    }

    public void GuardSelectedUnits()
    {
        foreach (GameObject unitObj in SelectedUnits)
        {
            if (unitObj == null)
                continue;

            CancelGatheringIfWorker(unitObj);

            if (unitObj.TryGetComponent<UnitCombat>(out UnitCombat combat))
            {
                combat.RequestGuard();
            }
        }

        CurrentCommandMode = CommandMode.None;
    }

    private bool TryHandleResourceRightClick(Ray ray)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, resourceMask))
            return false;

        MineralCrystal crystal = hit.collider.GetComponentInParent<MineralCrystal>();

        if (crystal == null)
            return false;

        bool commandSent = false;

        foreach (GameObject selectedUnit in SelectedUnits)
        {
            if (selectedUnit == null)
                continue;

            WorkerGathering workerGathering = selectedUnit.GetComponent<WorkerGathering>();

            if (workerGathering == null)
                continue;



            workerGathering.RequestGather(crystal);
            commandSent = true;
        }

        return commandSent;
    }

    private void CancelGatheringIfWorker(GameObject unitObj)
    {
        if (unitObj == null)
            return;

        if (unitObj.TryGetComponent<WorkerGathering>(out WorkerGathering gathering))
        {
            gathering.RequestCancelGathering();
        }
    }

}
