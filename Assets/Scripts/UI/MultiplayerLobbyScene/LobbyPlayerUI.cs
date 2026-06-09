using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text readyText;

    [Header("Controls")]
    [SerializeField] private TMP_Dropdown teamDropdown;
    [SerializeField] private TMP_Dropdown colorDropdown;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;

    [Header("Optional Visuals")]
    [SerializeField] private Image colorImage;

    private Action<string> onTeamChanged;
    private Action<string> onColorChanged;
    private Action onReadyClicked;

    private bool isLoading;

    private void Awake()
    {
        if (teamDropdown != null)
        {
            teamDropdown.interactable = false;

            teamDropdown.onValueChanged.AddListener(index =>
            {
                if (isLoading)
                    return;

                if (index < 0 || index >= teamDropdown.options.Count)
                    return;

                string selectedTeam = teamDropdown.options[index].text;
                onTeamChanged?.Invoke(selectedTeam);
            });
        }
        else
        {
            Debug.LogError($"{gameObject.name}: Team Dropdown nije povezan u LobbyPlayerUI.");
        }

        if (colorDropdown != null)
        {
            colorDropdown.interactable = false;

            colorDropdown.onValueChanged.AddListener(index =>
            {
                if (isLoading)
                    return;

                if (index < 0 || index >= colorDropdown.options.Count)
                    return;

                string selectedColor = colorDropdown.options[index].text;

                if (colorImage != null)
                    colorImage.color = GetColorFromName(selectedColor);

                onColorChanged?.Invoke(selectedColor);
            });
        }
        else
        {
            Debug.LogError($"{gameObject.name}: Color Dropdown nije povezan u LobbyPlayerUI.");
        }

        if (readyButton != null)
        {
            readyButton.interactable = false;

            readyButton.onClick.AddListener(() =>
            {
                if (isLoading)
                    return;

                onReadyClicked?.Invoke();
            });
        }
        else
        {
            Debug.LogError($"{gameObject.name}: Ready Button nije povezan u LobbyPlayerUI.");
        }
    }

    public void Load(
        string playerName,
        string team,
        string color,
        bool ready,
        bool isHost,
        bool isLocalPlayer,
        Action<string> onTeamChanged,
        Action<string> onColorChanged,
        Action onReadyClicked)
    {
        this.onTeamChanged = onTeamChanged;
        this.onColorChanged = onColorChanged;
        this.onReadyClicked = onReadyClicked;

        isLoading = true;

        if (playerNameText != null)
            playerNameText.text = isHost ? $"{playerName} (Host)" : playerName;

        if (readyText != null)
            readyText.text = ready ? "Ready" : "Not Ready";

        if (teamDropdown != null)
        {
            int teamIndex = FindOptionIndex(teamDropdown, team);
            teamDropdown.SetValueWithoutNotify(teamIndex);
            teamDropdown.RefreshShownValue();

            teamDropdown.interactable = isLocalPlayer;
        }

        if (colorDropdown != null)
        {
            int colorIndex = FindOptionIndex(colorDropdown, color);
            colorDropdown.SetValueWithoutNotify(colorIndex);
            colorDropdown.RefreshShownValue();

            string selectedColor = colorDropdown.options.Count > 0
                ? colorDropdown.options[colorIndex].text
                : color;

            if (colorImage != null)
                colorImage.color = GetColorFromName(selectedColor);

            colorDropdown.interactable = isLocalPlayer;
        }

        if (readyButton != null)
        {
            readyButton.interactable = isLocalPlayer;
            readyButton.image.color = ready ? Color.green : Color.gray;
        }

        if (readyButtonText != null)
            readyButtonText.text = ready ? "Unready" : "Ready";

        isLoading = false;
    }

    private int FindOptionIndex(TMP_Dropdown dropdown, string value)
    {
        if (dropdown == null || dropdown.options.Count == 0)
            return 0;

        for (int i = 0; i < dropdown.options.Count; i++)
        {
            if (dropdown.options[i].text == value)
                return i;
        }

        return 0;
    }

    private Color GetColorFromName(string colorName)
    {
        switch (colorName)
        {
            case "Red": return Color.red;
            case "Blue": return Color.blue;
            case "Green": return Color.green;
            case "Yellow": return Color.yellow;
            case "Purple": return new Color(0.5f, 0f, 1f);
            case "Orange": return new Color(1f, 0.5f, 0f);
            default: return Color.white;
        }
    }
}