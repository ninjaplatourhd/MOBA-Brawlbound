using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductionButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image iconImage;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void SetupUnit(BuildableUnit unit, ProductionBuilding productionBuilding)
    {
        if (labelText != null)
            labelText.text = unit.DisplayName;

        if (costText != null)
            costText.text = $"{unit.MineralCost}M\n{unit.PowerUpkeep}P";

        if (iconImage != null)
        {
            iconImage.sprite = unit.Icon;
            iconImage.enabled = unit.Icon != null;
            iconImage.raycastTarget = false;
        }

        if (button == null)
            button = GetComponent<Button>();

        button.onClick.RemoveAllListeners();

        button.onClick.AddListener(() =>
        {
            Debug.Log($"Kliknuto production dugme: {unit.DisplayName} / {unit.UnitId}");

            if (productionBuilding != null)
                productionBuilding.RequestBuildUnit(unit.UnitId);
            else
                Debug.LogWarning("ProductionBuilding reference je null na dugmetu.");
        });

        button.interactable = true;

        Debug.Log($"Setup dugmeta završen: {unit.DisplayName}");
    }

    public void SetupUpgrade(BuildableUpgrade upgrade)
    {
        if (labelText != null)
            labelText.text = upgrade.DisplayName;

        if (costText != null)
            costText.text = $"{upgrade.MineralCost}M";

        if (iconImage != null)
        {
            iconImage.sprite = upgrade.Icon;
            iconImage.enabled = upgrade.Icon != null;
        }

        button.onClick.RemoveAllListeners();
        button.interactable = false;
    }
}