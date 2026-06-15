using UnityEngine;
using UnityEngine.UI;

public class UnitEntryUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Slider healthBar;

    private Unit unit;
    private UnitData data;

    public void Setup(GameObject obj, Sprite sprite)
    {
        icon.sprite = sprite;

        data = obj.GetComponent<UnitData>();
        unit = obj.GetComponent<Unit>();

        UpdateUI();
    }

    private void Update()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (unit == null) return;

        float hp = unit.Health.Value;
        float maxHp = unit.MaxHealth.Value;

        if (maxHp > 0)
            healthBar.value = hp / maxHp;
    }
}