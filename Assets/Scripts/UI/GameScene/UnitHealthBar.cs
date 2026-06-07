using UnityEngine;
using UnityEngine.UI;

public class UnitHealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private Unit unit;
    private Camera mainCamera;

    private void Start()
    {
        unit = GetComponentInParent<Unit>();
        mainCamera = Camera.main;

        if (unit != null)
        {
            unit.Health.OnValueChanged += OnHealthChanged;
            UpdateHealthBar();
        }
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

    private void OnDestroy()
    {
        if (unit != null)
        {
            unit.Health.OnValueChanged -= OnHealthChanged;
        }
    }

    private void OnHealthChanged(float previousValue, float newValue)
    {
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (unit == null || fillImage == null)
            return;

        float maxHealth = Mathf.Max(1f, unit.MaxHealth.Value);
        fillImage.fillAmount = unit.Health.Value / maxHealth;
    }
}