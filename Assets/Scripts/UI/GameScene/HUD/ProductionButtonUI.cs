using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ProductionButtonUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image iconImage;

    private BuildableUnit currentUnit;
    private BuildableUpgrade currentUpgrade;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void SetupUnit(BuildableUnit unit, ProductionBuilding productionBuilding)
    {
        currentUnit = unit;
        currentUpgrade = null;

        if (unit == null)
            return;

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
            Debug.Log($"Kliknuto unit production dugme: {unit.DisplayName} / {unit.UnitId}");

            if (productionBuilding != null)
                productionBuilding.RequestBuildUnit(unit.UnitId);
            else
                Debug.LogWarning("ProductionBuilding reference je null na unit dugmetu.");
        });

        button.interactable = true;
    }

    public void SetupUpgrade(BuildableUpgrade upgrade, ProductionBuilding productionBuilding)
    {
        currentUpgrade = upgrade;
        currentUnit = null;

        if (upgrade == null)
            return;

        if (labelText != null)
            labelText.text = upgrade.DisplayName;

        if (costText != null)
            costText.text = $"{upgrade.MineralCost}M";

        if (iconImage != null)
        {
            iconImage.sprite = upgrade.Icon;
            iconImage.enabled = upgrade.Icon != null;
            iconImage.raycastTarget = false;
        }

        if (button == null)
            button = GetComponent<Button>();

        button.onClick.RemoveAllListeners();

        button.onClick.AddListener(() =>
        {
            Debug.Log($"Kliknuto upgrade dugme: {upgrade.DisplayName} / {upgrade.UpgradeId}");

            if (productionBuilding != null)
                productionBuilding.RequestBuildUpgrade(upgrade.UpgradeId);
            else
                Debug.LogWarning("ProductionBuilding reference je null na upgrade dugmetu.");
        });

        button.interactable = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("ENTER " + name);

        if (currentUnit != null)
        {
            TooltipUI.Instance.Show(
                currentUnit.DisplayName,
                $"Cost: {currentUnit.MineralCost} Minerals\n" +
                $"Power: {currentUnit.PowerUpkeep}\n" +
                $"Build Time: {currentUnit.BuildTime:F1}s"
            );

            return;
        }

        if (currentUpgrade != null)
        {
            TooltipUI.Instance.Show(
                currentUpgrade.DisplayName,
                $"Cost: {currentUpgrade.MineralCost} Minerals\n" +
                $"Research: {currentUpgrade.ResearchTime:F1}s"
            );
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("EXIT " + name);
        TooltipUI.Instance.Hide();
    }
}