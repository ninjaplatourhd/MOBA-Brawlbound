using TMPro;
using UnityEngine;

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    private RectTransform rect;

    private void Awake()
    {
        Instance = this;
        rect = GetComponent<RectTransform>();

        gameObject.SetActive(false);
    }

    private void Update()
    {
        rect.position = Input.mousePosition + new Vector3(60, -30);
    }

    public void Show(string title, string body)
    {
        titleText.text = title;
        bodyText.text = body;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }


}