using UnityEngine;
using UnityEngine.AI;

public class UnitMovement : MonoBehaviour
{


    private Camera _camera;
    private NavMeshAgent _agent;
    [SerializeField]
    public LayerMask _ground;


    private bool shouldMove;
    private Vector3 _targetLocation;
    private Unit _unit;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _camera = Camera.main;
        _agent = GetComponent<NavMeshAgent>();
        _unit = gameObject.GetComponent<Unit>();
        _targetLocation = gameObject.transform.position;
    }



    private float _updatePathTimer;
    private const float _updatePathDelay = 0.5f;
    void Update()
    {
        //Timer Updates
        _updatePathTimer += Time.deltaTime;


        //Zadavanje komande za kretanje
        if (_unit.Selected && Input.GetMouseButton(1))
        {
            RaycastHit hit;
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, _ground))
            {
                var centerPosition = UnitManager.instance.GetSelectedUnitCenter();
                var positionDiff = gameObject.transform.position - centerPosition;
                positionDiff.y = 0;
                var targetPosition = hit.point + positionDiff;
                _targetLocation = targetPosition;
                shouldMove = true;
                _agent.SetDestination(_targetLocation);
            }

        }

        // if (shouldMove)
        //  {


        if (_updatePathTimer >= _updatePathDelay)
        {
            _updatePathTimer = 0;
            _agent.SetDestination(_targetLocation);
        }


        if (_targetLocation == gameObject.transform.position)
            shouldMove = false;
        //   }

    }



}
