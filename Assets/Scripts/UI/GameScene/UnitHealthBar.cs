using UnityEngine;
using UnityEngine.UI;

public class UnitHealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private Unit unit;
    private Camera mainCamera;

    private void Start()
    {
        unit = GetComponentInParent<Unit>();
        mainCamera = Camera.main;
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
        if (unit == null)
            unit = GetComponentInParent<Unit>();

        if (unit == null || fillImage == null)
            return;

        float maxHealth = Mathf.Max(1f, unit.MaxHealth.Value);
        fillImage.fillAmount = unit.Health.Value / maxHealth;
    }
}