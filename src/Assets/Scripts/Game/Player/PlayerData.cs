

using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public string LobbyPlayerId;
    public string Name;
    public string Team;
    public string Color;

    public static Color PlayerColorFromName(string colorName)
    {
        if (string.IsNullOrWhiteSpace(colorName))
            return new Color(0f, 0f, 0f);

        switch (colorName.Trim().ToLower())
        {
            case "blue":
                return new Color(0.1f, 0.35f, 1f);

            case "red":
                return new Color(1f, 0.15f, 0.1f);

            case "green":
                return new Color(0.1f, 0.85f, 0.2f);

            case "yellow":
                return new Color(1f, 0.85f, 0.1f);

            case "orange":
                return new Color(1f, 0.45f, 0.05f);

            case "purple":
                return new Color(0.6f, 0.15f, 1f);

            case "cyan":
                return new Color(0f, 0.85f, 1f);

            case "white":
                return new Color(0.9f, 0.9f, 0.9f);

            default:
                return new Color(0f, 0f, 0f);
        }
    }
}
