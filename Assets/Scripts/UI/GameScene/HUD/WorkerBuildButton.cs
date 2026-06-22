using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorkerBuildButtonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image iconImage;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void Setup(BuildableBuilding buildableBuilding, WorkerBuilder workerBuilder)
    {
        if (buildableBuilding == null)
            return;

        if (labelText != null)
            labelText.text = buildableBuilding.DisplayName;

        if (costText != null)
        {
            string cost = $"{buildableBuilding.MineralCost}M";

            if (buildableBuilding.RequiredFreePower > 0)
                cost += $"\n{buildableBuilding.RequiredFreePower}P";

            costText.text = cost;
        }

        if (iconImage != null)
        {
            iconImage.sprite = buildableBuilding.Icon;
            iconImage.enabled = buildableBuilding.Icon != null;
            iconImage.raycastTarget = false;
        }

        if (button == null)
            button = GetComponent<Button>();

        button.onClick.RemoveAllListeners();

        bool canBuild = workerBuilder != null && workerBuilder.CanBuild(buildableBuilding);

        button.interactable = canBuild;

        button.onClick.AddListener(() =>
        {
            if (workerBuilder == null)
                return;

            if (!workerBuilder.CanBuild(buildableBuilding))
            {
                Debug.LogWarning($"Ne možeš trenutno da gradiš: {buildableBuilding.DisplayName}");
                return;
            }

            if (BuildingPlacementSystem.Instance == null)
            {
                Debug.LogWarning("BuildingPlacementSystem ne postoji u sceni.");
                return;
            }

            BuildingPlacementSystem.Instance.StartPlacement(buildableBuilding, workerBuilder);
        });
    }
}