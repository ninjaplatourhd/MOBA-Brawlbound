using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class IngameConsole : NetworkBehaviour
{
    public static bool IsTypingInConsole { get; private set; }

    public string LocalClientName
    {
        get => client;
        set => client = value;
    }

    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TMP_Text content;
    [SerializeField] private Scrollbar scrollbar;

    private readonly List<string> messages = new List<string>();

    private string client = "Anon";
    private int lastSubmitFrame = -1;
    private bool startedMessageAdded = false;

    private void Start()
    {
        if (!startedMessageAdded)
        {
            AppendSystemMessage("Started Game...");
            startedMessageAdded = true;
        }

        SetupInputField();
    }

    private void OnEnable()
    {
        //Application.logMessageReceived += HandleLog;

        if (inputField != null)
            SetupInputField();
    }

    private void OnDisable()
    {
        //Application.logMessageReceived -= HandleLog;

        if (inputField != null)
        {
            inputField.onSelect.RemoveListener(HandleInputSelected);
            inputField.onDeselect.RemoveListener(HandleInputDeselected);
            inputField.onSubmit.RemoveListener(SubmitMessage);
        }

        IsTypingInConsole = false;
    }

    private void Update()
    {
        if (!IsClient)
            return;

        if (inputField == null)
            return;

        IsTypingInConsole = inputField.isFocused;

        if (!inputField.isFocused)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            inputField.DeactivateInputField();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            IsTypingInConsole = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SubmitMessage(inputField.text);
        }
    }

    private void SetupInputField()
    {
        if (inputField == null)
            return;

        inputField.lineType = TMP_InputField.LineType.SingleLine;

        inputField.onSelect.RemoveListener(HandleInputSelected);
        inputField.onDeselect.RemoveListener(HandleInputDeselected);
        inputField.onSubmit.RemoveListener(SubmitMessage);

        inputField.onSelect.AddListener(HandleInputSelected);
        inputField.onDeselect.AddListener(HandleInputDeselected);
        inputField.onSubmit.AddListener(SubmitMessage);
    }

    private void HandleInputSelected(string value)
    {
        IsTypingInConsole = true;
    }

    private void HandleInputDeselected(string value)
    {
        IsTypingInConsole = false;
    }

    private void SubmitMessage(string rawMessage)
    {
        if (!IsClient)
            return;

        if (inputField == null)
            return;

        if (lastSubmitFrame == Time.frameCount)
            return;

        lastSubmitFrame = Time.frameCount;

        string message = rawMessage.Trim();

        if (!string.IsNullOrWhiteSpace(message))
            SendMessageToServerRpc(message);

        inputField.text = "";
        inputField.ActivateInputField();
        inputField.Select();

        IsTypingInConsole = true;
    }

    //private void HandleLog(string logString, string stackTrace, LogType type)
    //{
    //    string formattedMessage = $"[{type}] {logString}";
    //    AppendSystemMessage(formattedMessage);
    //}

    [ServerRpc(RequireOwnership = false)]
    private void SendMessageToServerRpc(string message, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        string senderName = GetPlayerName(senderId);

        ReceiveMessageClientRpc(senderName, message);
    }

    [ClientRpc]
    private void ReceiveMessageClientRpc(string sender, string message)
    {
        AppendMessage(sender, message);
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

    private string GetPlayerName(ulong clientId)
    {
        if (PlayerRegistry.Players != null &&
            PlayerRegistry.Players.TryGetValue(clientId, out var data))
        {
            return data.Name;
        }

        return $"Player {clientId}";
    }

    public void ClearChat()
    {
        messages.Clear();
        RefreshChat();
    }

    public void RemoveMessage(int index)
    {
        if (index < 0 || index >= messages.Count)
            return;

        messages.RemoveAt(index);
        RefreshChat();
    }

    public void LogChatHistoryToFile()
    {
        string fileName = "ingame-log-" + DateTime.Now.ToString()
            .Replace("/", "-")
            .Replace(" ", "-")
            .Replace(":", "-") + ".txt";

        string filePath = Path.Combine("Logs/IngameLogs/", fileName);

        Directory.CreateDirectory("Logs/IngameLogs");

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            foreach (string msg in messages)
                writer.Write(msg);
        }
    }

    public async void RefreshChat()
    {
        if (content == null)
            return;

        content.text = "";

        foreach (string msg in messages)
            content.text += msg;

        await Task.Delay(10);

        if (scrollbar != null)
            scrollbar.value = 0.000001f;
    }
}