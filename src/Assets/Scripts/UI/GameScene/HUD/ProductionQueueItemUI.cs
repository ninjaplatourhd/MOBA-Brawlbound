using UnityEngine;
using UnityEngine.UI;

public class ProductionQueueItemUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image progressFillImage;

    private ProductionBuilding productionBuilding;
    private int queueIndex = -1;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void Setup(
        ProductionBuilding productionBuilding,
        int queueIndex,
        BuildQueueItemNet item,
        Sprite icon)
    {
        this.productionBuilding = productionBuilding;
        this.queueIndex = queueIndex;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.raycastTarget = false;
        }

        if (progressFillImage != null)
        {
            float progress01 = 0f;

            if (item.BuildTime > 0.01f)
                progress01 = 1f - Mathf.Clamp01(item.RemainingTime / item.BuildTime);

            progressFillImage.fillAmount = progress01;
            progressFillImage.raycastTarget = false;
        }

        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(CancelQueueItem);
        }
    }

    private void CancelQueueItem()
    {
        if (productionBuilding == null)
            return;

        if (queueIndex < 0)
            return;

        productionBuilding.RequestCancelQueueItem(queueIndex);
    }
}