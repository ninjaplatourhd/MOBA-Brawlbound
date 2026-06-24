using TMPro;
using Unity.Netcode;
using UnityEngine;

public class TopBarUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text mineraliText;
    [SerializeField] private TMP_Text techText;
    [SerializeField] private TMP_Text strujaText;

    [Header("Update")]
    [SerializeField] private float refreshInterval = 0.1f;

    private float refreshTimer;



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
        if (mineraliText == null || techText == null || strujaText == null)
            return;

        if (NetworkManager.Singleton == null || PlayerEconomyManager.Instance == null)
        {
            return;
        }

        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        if (!PlayerEconomyManager.Instance.TryGetPlayerState(localClientId, out PlayerGameData data))
        {
            return;
        }
        mineraliText.text = data.Minerals.ToString();
        strujaText.text = data.PowerUsed.ToString() + "/" + data.PowerProduced.ToString();
        techText.text = data.TechTier.ToString();

    }
}