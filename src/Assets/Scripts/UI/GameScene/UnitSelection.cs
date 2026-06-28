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

    private bool _additiveClick;

    private bool _isDragging;
    private float _dragThreshold = 10f;

    private float lastClickTime = 0f;
    private const float doubleClickThreshold = 0.3f;
    private GameObject lastClickedUnit = null;

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

            _additiveClick = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);


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
                HandleLeftClick();
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

    private void HandleUnitClick(Unit unit)
    {


        if (!unit.BelongsToLocalPlayer())
            return;

        GameObject clickedUnit = unit.gameObject;

        // double click → select same type
        if (Time.time - lastClickTime < doubleClickThreshold &&
            lastClickedUnit == clickedUnit)
        {
            SelectAllSameUnits(unit);

            lastClickTime = 0f;
            lastClickedUnit = null;
            InputBlocker.SelectionConsumed = true;
            return;
        }

        lastClickTime = Time.time;
        lastClickedUnit = clickedUnit;

        // CTRL ADDITIVE SELECTION (now reliable)
        if (_additiveClick)
        {
            ToggleUnitSelection(clickedUnit);
            InputBlocker.SelectionConsumed = true;
            return;
        }

        UnitManager.instance.SelectUnit(clickedUnit, _additiveClick);
        InputBlocker.SelectionConsumed = true;
        return;



        // empty space click
        if (!_additiveClick)
        {
            UnitManager.instance.DeSelectAll();
            InputBlocker.SelectionConsumed = true;
        }
    }

    // metoda za selekt istih jedinica duplim klikom/Savo
    private void SelectAllSameUnits(Unit clickedUnit)
    {
        List<GameObject> result = new List<GameObject>();

        foreach (var obj in UnitManager.instance.AllUnitsList)
        {
            if (obj == null)
                continue;

            if (!obj.TryGetComponent<Unit>(out Unit unit))
                continue;

            if (!unit.BelongsToLocalPlayer())
                continue;

            if (unit.Data == null || clickedUnit.Data == null)
                continue;

            if (unit.Data.UnitId != clickedUnit.Data.UnitId)
                continue;

            Vector3 screenPos = Camera.main.WorldToViewportPoint(obj.transform.position);

            bool onScreen =
                screenPos.z > 0 &&
                screenPos.x >= 0 && screenPos.x <= 1 &&
                screenPos.y >= 0 && screenPos.y <= 1;

            if (!onScreen)
                continue;

            result.Add(obj);
        }

        UnitManager.instance.SelectUnits(result);
    }

    // metoda za dodavanje ili uklanjanje jedinice iz selekcije
    private void ToggleUnitSelection(GameObject unitObj)
    {
        if (UnitManager.instance.SelectedUnits.Contains(unitObj))
        {
            // remove from selection
            UnitManager.instance.SelectedUnits.Remove(unitObj);

            if (unitObj.TryGetComponent<ISelectableObject>(out ISelectableObject selectable))
                selectable.DeSelect();

            return;
        }

        // add to selection
        UnitManager.instance.SelectedUnits.Add(unitObj);

        if (unitObj.TryGetComponent<ISelectableObject>(out ISelectableObject selectableAdd))
            selectableAdd.Select();
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

    private void HandleLeftClick()
    {
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Clickable")))
        {
            if (hit.collider.TryGetComponent<Unit>(out Unit unit))
            {
                HandleUnitClick(unit);
                return;
            }

            Building building = hit.collider.GetComponentInParent<Building>();

            if (building != null)
            {
                if (building.BelongsToLocalPlayer())
                {
                    UnitManager.instance.DeSelectAll();

                    if (BuildingManager.instance != null)
                        BuildingManager.instance.SelectBuilding(building.gameObject);
                }

                return;
            }
        }

        if (!_additiveClick)
        {
            UnitManager.instance.DeSelectAll();

            if (BuildingManager.instance != null)
                BuildingManager.instance.DeSelectAll();
        }
    }


}

