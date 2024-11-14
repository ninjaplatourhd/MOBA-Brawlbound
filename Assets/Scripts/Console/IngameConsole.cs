using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IngameConsole : MonoBehaviour
{

    public string LocalClientName { get => client; set { client = value; } }

    [SerializeField]
    private TMP_InputField inputField;

    [SerializeField]
    private TMP_Text content;

    [SerializeField]
    private Scrollbar scrollbar;

    private List<string> messages = new List<string>();
    private string client = "Anon";

    public void Start()
    {
        AppendSystemMessage("Started Game...");
    }
    private void OnEnable()
    {
        // Register to listen to log messages
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        // Unregister when the object is disabled
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {

        string formattedMessage = $"[{type}] {logString}";
        AppendSystemMessage(formattedMessage);
    }

    public void Update()
    {
        if (inputField.text != "" && Input.GetKeyUp(KeyCode.Return))
        {
            AppendMessage(client, inputField.text);
            inputField.text = "";
        }
    }

    public void AppendMessage(string sender, string msg)
    {
        messages.Add("[" + sender + "]: " + msg + "\n");
        RefreshChat();
    }

    public void AppendSystemMessage(string msg)
    {
        messages.Add("[System]: " + msg + "\n");
        RefreshChat();
    }

    public void ClearChat()
    {
        messages.Clear();
        RefreshChat();
    }

    public void RemoveMessage(int index)
    {
        messages.RemoveAt(index);
        RefreshChat();
    }

    public void LogChatHistoryToFile()
    {

        string filePath = Path.Combine("Logs/IngameLogs/", "ingame-log-" + DateTime.Now.ToString().Replace("/", "-").Replace(" ", "-").Replace(":", "-") + ".txt");

        Directory.CreateDirectory("Logs/IngameLogs");

        using (StreamWriter writer = new(filePath))
        {
            foreach (string msg in messages)
                writer.Write(msg);
        }
    }

    public async void RefreshChat()
    {
        content.text = "";

        foreach (string msg in messages)
            content.text += msg;

        await Task.Delay(10); // Snap to last msg won't work if this is removed :(
        scrollbar.value = 0.000001f;
    }
}
