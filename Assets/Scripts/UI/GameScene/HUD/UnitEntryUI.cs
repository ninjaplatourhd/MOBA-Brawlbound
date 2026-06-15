using UnityEngine;
using UnityEngine.UI;

public class UnitEntryUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Slider healthBar;

    public void Setup(Sprite sprite, float hp, float maxHp)
    {
        icon.sprite = sprite;
        healthBar.value = hp / maxHp;
    }
}