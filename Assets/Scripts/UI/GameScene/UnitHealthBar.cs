using UnityEngine;
using UnityEngine.UI;

public class UnitHealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private Unit unit;
    private Building building;
    private Camera mainCamera;

    private void Start()
    {
        unit = GetComponentInParent<Unit>();
        building = GetComponentInParent<Building>();

        mainCamera = Camera.main;

        if (fillImage == null)
        {
            Debug.LogError($"{gameObject.name} nema dodeljen Fill Image.");
        }

        if (unit == null && building == null)
        {
            Debug.LogError($"{gameObject.name} nije child ni Unit-a ni Building-a.");
        }
    }

    private void Update()
    {
        UpdateHealthBar();
    }

    private void LateUpdate()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }

    private void UpdateHealthBar()
    {
        if (fillImage == null)
            return;

        if (unit != null)
        {
            float maxHealth = Mathf.Max(1f, unit.MaxHealth.Value);
            fillImage.fillAmount = unit.Health.Value / maxHealth;
            return;
        }

        if (building != null)
        {
            float maxHealth = Mathf.Max(1f, building.MaxHealth.Value);
            fillImage.fillAmount = building.Health.Value / maxHealth;
            return;
        }
    }
}