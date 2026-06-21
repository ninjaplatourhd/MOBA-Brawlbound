using TMPro;
using Unity.Netcode;
using UnityEngine;

public class TopBarUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text resourceText;

    [Header("Update")]
    [SerializeField] private float refreshInterval = 0.1f;

    private float refreshTimer;

    private void Awake()
    {
        if (resourceText == null)
            resourceText = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;

        if (refreshTimer < refreshInterval)
            return;

        refreshTimer = 0f;
        Refresh();
    }

    private void Refresh()
    {
        if (resourceText == null)
            return;

        if (NetworkManager.Singleton == null || PlayerEconomyManager.Instance == null)
        {
            resourceText.text = "Minerali: -\nStruja: - / -\nTech: -";
            return;
        }

        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (!PlayerEconomyManager.Instance.TryGetPlayerState(localClientId, out PlayerGameData data))
        {
            resourceText.text = "Ekonomija nije spremna...";
            return;
        }

        resourceText.text =
            $"Minerali: {data.Minerals}\n" +
            $"Struja: {data.PowerUsed} / {data.PowerProduced}  ({data.PowerAvailable} slobodno)\n" +
            $"Tech: {data.TechTier}";
    }
}