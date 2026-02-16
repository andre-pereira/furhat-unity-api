using UnityEngine;
using UnityEngine.UIElements;
using Furhat.Runtime; // Using the assembly we created!

public class FurhatTestController : MonoBehaviour {
    private FurhatClient _client;
    private TextField _ipField;
    private TextField _speechField;
    private Label _statusLog;

    private void OnEnable() {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // Query elements by name
        _ipField = root.Q<TextField>("IpField");
        _speechField = root.Q<TextField>("SpeechField");
        _statusLog = root.Q<Label>("StatusLog");

        root.Q<Button>("ConnectBtn").clicked += OnConnectClicked;
        root.Q<Button>("SpeakBtn").clicked += OnSpeakClicked;

        _client = new FurhatClient();
        _client.OnMessageReceived += msg => Debug.Log($"Robot says: {msg}");
    }

    private async void OnConnectClicked() {
        _statusLog.text = "Status: Connecting...";
        await _client.Connect(_ipField.value);
        _statusLog.text = "Status: Connected!";
    }

    private async void OnSpeakClicked() {
        await _client.Speak(_speechField.value);
    }

    private void OnDisable() => _client?.Dispose();
}