using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UnitSelection : MonoBehaviour
{
    [SerializeField]
    private GameObject _selectionArea;

    [SerializeField]
    private string _player;

    private Vector2 _startingPoint;
    private RectTransform rectTransform;

    private bool MouseStartedOnUI;

    private bool _isDragging;
    private float _dragThreshold = 10f;

    private Camera _camera;
    private void Awake()
    {
        rectTransform = _selectionArea.GetComponent<RectTransform>();
    }

    private void Start()
    {
        _camera = Camera.main;
    }
    //private void Update()
    //{
    //    if (Input.GetMouseButtonDown(0))
    //    {
    //        _selectionArea.gameObject.SetActive(true);
    //        _startingPoint = Input.mousePosition;

    //        rectTransform.position = _startingPoint;

    //        rectTransform.sizeDelta = Vector2.zero;
    //    }
    //    else if (Input.GetMouseButton(0))
    //    {
    //        Vector2 mousePosition = Input.mousePosition;
    //        Vector2 size = new Vector2(
    //         Mathf.Abs(mousePosition.x - _startingPoint.x),
    //         Mathf.Abs(mousePosition.y - _startingPoint.y)
    //     );

    //        rectTransform.pivot = new Vector2(
    //            mousePosition.x < _startingPoint.x ? 1 : 0,
    //            mousePosition.y < _startingPoint.y ? 1 : 0
    //        );

    //        rectTransform.sizeDelta = size;
    //    }
    //    if (Input.GetMouseButtonUp(0))
    //    {
    //        SelectUnitsInRectangle();
    //        _selectionArea.SetActive(false);
    //    }
    //}

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            MouseStartedOnUI = EventSystem.current != null &&
                               EventSystem.current.IsPointerOverGameObject();

            _isDragging = false;
            _startingPoint = Input.mousePosition;

            _selectionArea.SetActive(true);
            rectTransform.position = _startingPoint;
            rectTransform.sizeDelta = Vector2.zero;
        }

        else if (Input.GetMouseButton(0))
        {
            Vector2 mousePosition = Input.mousePosition;

            if (!_isDragging)
            {
                if (Vector2.Distance(mousePosition, _startingPoint) > _dragThreshold)
                {
                    _isDragging = true;
                }
            }

            if (_isDragging)
            {
                Vector2 size = new Vector2(
                    Mathf.Abs(mousePosition.x - _startingPoint.x),
                    Mathf.Abs(mousePosition.y - _startingPoint.y)
                );

                rectTransform.pivot = new Vector2(
                    mousePosition.x < _startingPoint.x ? 1 : 0,
                    mousePosition.y < _startingPoint.y ? 1 : 0
                );

                rectTransform.sizeDelta = size;
            }
        }

        else if (Input.GetMouseButtonUp(0))
        {
            //if (InputBlocker.IsPointerOverUI())
            //{
            //    _isDragging = false;
            //    _selectionArea.SetActive(false);
            //    return;
            //}

            //hard code ako bloker skript ne bude radio
            if (MouseStartedOnUI)
            {
                _isDragging = false;
                _selectionArea.SetActive(false);
                return;
            }

            _selectionArea.SetActive(false);

            if (_isDragging)
            {
                SelectUnitsInRectangle();
            }
            else
            {
                SelectSingleUnit();
            }

            _isDragging = false;
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
        }
    }

    private void LateUpdate()
    {
        if (!Input.GetMouseButton(0))
        {
            _selectionArea.SetActive(false);
            _isDragging = false;
        }
    }

    private void SelectSingleUnit()
    {
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Clickable")))
        {
            if (hit.collider.TryGetComponent<Unit>(out Unit unit))
            {
                if (unit.BelongsToLocalPlayer())
                {
                    UnitManager.instance.SelectUnit(unit.gameObject);
                    return;
                }
            }
        }

        UnitManager.instance.DeSelectAll();
    }

    private void SelectUnitsInRectangle()
    {
        Vector3[] worldCorners = new Vector3[4];
        rectTransform.GetWorldCorners(worldCorners);

        Vector3 bottomLeft = worldCorners[0];
        Vector3 topRight = worldCorners[2];

        List<GameObject> selectableUnits = UnitManager.instance.AllUnitsList;
        List<GameObject> selectedUnits = new List<GameObject>();

        foreach (var unit in selectableUnits)
        {
            if (unit == null)
                continue;

            Vector2 unitScreenPosition = RectTransformUtility.WorldToScreenPoint(
                _camera,
                unit.transform.position
            );

            if (unitScreenPosition.x >= bottomLeft.x &&
                unitScreenPosition.x <= topRight.x &&
                unitScreenPosition.y >= bottomLeft.y &&
                unitScreenPosition.y <= topRight.y)
            {
                if (unit.TryGetComponent<Unit>(out Unit unitObject))
                {
                    if (unitObject.BelongsToLocalPlayer())
                    {
                        selectedUnits.Add(unit);
                    }
                }
            }
        }

        UnitManager.instance.SelectUnits(selectedUnits);
    }
}

