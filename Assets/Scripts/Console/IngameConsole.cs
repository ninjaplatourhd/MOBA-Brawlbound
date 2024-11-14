using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Text;

public class IngameConsole : MonoBehaviour {

    public string LocalClientName { get => client; set { client = value; } }

    [SerializeField]
    private TMP_InputField inputField;

    [SerializeField]
    private TMP_Text content;

    [SerializeField]
    private Scrollbar scrollbar;

    private List<string> messages = new List<string>();
    private string client = "Anon";

    public void Start() {
        AppendSystemMessage("Started Game...");
    }

    public void Update() {
        if (inputField.text != "" && Input.GetKeyUp(KeyCode.Return)) {
            AppendMessage(client, inputField.text);
            inputField.text = "";         
        }
    }

    public void AppendMessage(string sender, string msg) {
        messages.Add("[" + sender + "]: " + msg + "\n");
        RefreshChat();
    }

    public void AppendSystemMessage(string msg) {
        messages.Add("[System]: " + msg + "\n");
        RefreshChat();
    }

    public void ClearChat() {
        messages.Clear();
        RefreshChat();
    }

    public void RemoveMessage(int index) {
        messages.RemoveAt(index);
        RefreshChat();
    }

    public void LogChatHistoryToFile() {

        string filePath = Path.Combine("Logs/IngameLogs/", "ingame-log-" + DateTime.Now.ToString().Replace("/", "-").Replace(" ", "-").Replace(":", "-") + ".txt");

        Directory.CreateDirectory("Logs/IngameLogs");

        using (StreamWriter writer = new(filePath)) {
            foreach(string msg in messages)
                writer.Write(msg);
        }
    }

    public async void RefreshChat() {
        content.text = "";

        foreach (string msg in messages)
            content.text += msg;

        await Task.Delay(10); // Snap to last msg won't work if this is removed :(
        scrollbar.value = 0.000001f;
    }
}
