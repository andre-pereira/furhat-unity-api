using UnityEngine;
using UnityEngine.UIElements;
using Furhat.Runtime;
using System.Collections.Generic;
using System;


public class FurhatTestController : MonoBehaviour {
    private FurhatClient _client;
    private Label _statusLog;
    private TextField _ipField;
    private DropdownField _requestSelector;

    private void OnEnable() {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // 1. Global Header Elements
        _ipField = root.Q<TextField>("IpField");
        _statusLog = root.Q<Label>("StatusLog");
        _requestSelector = root.Q<DropdownField>("RequestTypeSelector");
        root.Q<Button>("ConnectBtn").clicked += OnConnectClicked;

        // 2. Initialize Client
        _client = new FurhatClient();
        // Log basic events to console for debugging
        _client.OnMessageReceived += msg => Debug.Log($"Robot: {msg}");
        _client.OnMessageSent += msg => LogRequestToUI(msg);

        // 3. Initialize UI Sections
        ApplyStartupDefaults(root);
        SetupDynamicUI(root);
        SetupPanelButtons(root);
    }

    private void SetupDynamicUI(VisualElement root) {
        var panels = new Dictionary<string, VisualElement> {
            { "Speak Text", root.Q<VisualElement>("PanelSpeakText") },
            { "Speak Audio", root.Q<VisualElement>("PanelSpeakAudio") },
            { "Speak Stop", root.Q<VisualElement>("PanelStopOnly") },
            { "Listen Start", root.Q<VisualElement>("PanelListenStart") },
            { "Listen Stop", root.Q<VisualElement>("PanelStopOnly") },
            { "Gesture", root.Q<VisualElement>("PanelGesture") },
            { "Attend Location", root.Q<VisualElement>("PanelAttend") },
            { "LED Control", root.Q<VisualElement>("PanelLed") }
        };

        foreach (var p in panels.Values) p.style.display = DisplayStyle.None;
        panels["Speak Text"].style.display = DisplayStyle.Flex;

        _requestSelector.RegisterValueChangedCallback(evt => {
            foreach (var panel in panels.Values) panel.style.display = DisplayStyle.None;
            if (panels.ContainsKey(evt.newValue)) panels[evt.newValue].style.display = DisplayStyle.Flex;
        });
    }

    private void SetupPanelButtons(VisualElement root) {
        // --- Speak Text ---
        root.Q<Button>("SendSpeakText").clicked += async () => {
            var txt = root.Q<TextField>("SpeakTextInput").value;
            if (string.IsNullOrWhiteSpace(txt)) { LogWarning("Text cannot be empty!"); return; }
            await _client.Speak(txt, root.Q<Toggle>("SpeakTextAbort").value, root.Q<Toggle>("SpeakTextMonitor").value);
        };

        // --- Speak Audio ---
        root.Q<Button>("SendSpeakAudio").clicked += async () => {
            var url = root.Q<TextField>("AudioUrl").value;
            var label = root.Q<TextField>("AudioDisplayText").value;
            if (string.IsNullOrWhiteSpace(url)) { LogWarning("URL cannot be empty!"); return; }
            await _client.SpeakAudio(url, root.Q<Toggle>("AudioAbort").value, root.Q<Toggle>("AudioLipsync").value, label);
        };

        // --- Gesture ---
        root.Q<Button>("SendGesture").clicked += async () => {
            var gName = root.Q<TextField>("GestureName").value;
            if (string.IsNullOrWhiteSpace(gName)) { LogWarning("Gesture name required!"); return; }
            
            await _client.Gesture(
                gName,
                Mathf.Clamp01(root.Q<FloatField>("GestureIntensity").value),
                Mathf.Max(0.1f, root.Q<FloatField>("GestureDuration").value),
                root.Q<Toggle>("GestureMonitor").value
            );
        };

        // --- Listen ---
        root.Q<Button>("SendListenStart").clicked += async () => {
            var noSpeech = root.Q<FloatField>("ListenNoSpeechTime");
            var endSpeech = root.Q<FloatField>("ListenEndSpeechTime");
            if (noSpeech.value <= 0) noSpeech.value = 8.0f;
            if (endSpeech.value <= 0) endSpeech.value = 1.0f;

            var config = new ListenRequest {
                Partial = root.Q<Toggle>("ListenPartial").value,
                Concat = root.Q<Toggle>("ListenConcat").value,
                StopNoSpeech = root.Q<Toggle>("ListenStopNoSpeech").value,
                StopRobotStart = root.Q<Toggle>("ListenStopRobot").value,
                StopUserEnd = root.Q<Toggle>("ListenStopUser").value,
                ResumeRobotEnd = root.Q<Toggle>("ListenResume").value,
                NoSpeechTimeout = noSpeech.value,
                EndSpeechTimeout = endSpeech.value
            };
            await _client.StartListening(config);
        };

        // --- Attend ---
        root.Q<Button>("SendAttend").clicked += async () => {
            await _client.Attend(
                root.Q<FloatField>("AttendX").value,
                root.Q<FloatField>("AttendY").value,
                root.Q<FloatField>("AttendZ").value,
                root.Q<DropdownField>("AttendSpeed").value
            );
        };

        // --- LED ---
        root.Q<Button>("SendLed").clicked += async () => {
            await _client.SetLed(root.Q<TextField>("LedColorHex").value);
        };

        // --- Smart Stop Buttons ---
        root.Q<Button>("SendGenericStop").clicked += async () => {
            string currentPanel = _requestSelector.value;
            if (currentPanel.Contains("Listen")) {
                await _client.StopListening();
            } else {
                await _client.StopSpeaking();
            }
        };
    }

    private void ApplyStartupDefaults(VisualElement root) {
        // Defaults for Speak
        root.Q<TextField>("SpeakTextInput").value = "hello world";
        root.Q<Toggle>("SpeakTextAbort").value = true;
        root.Q<Toggle>("SpeakTextMonitor").value = true;

        // Defaults for Audio
        root.Q<TextField>("AudioDisplayText").value = "Audio";
        root.Q<Toggle>("AudioAbort").value = true;
        root.Q<Toggle>("AudioLipsync").value = true;

        // Defaults for Listening
        root.Q<Toggle>("ListenPartial").value = true;
        root.Q<Toggle>("ListenConcat").value = true;
        root.Q<Toggle>("ListenStopNoSpeech").value = true;
        root.Q<Toggle>("ListenStopRobot").value = true;
        root.Q<Toggle>("ListenStopUser").value = true;
        root.Q<Toggle>("ListenResume").value = false;

        // Defaults for Attend
        root.Q<FloatField>("AttendX").value = 0;
        root.Q<FloatField>("AttendY").value = 0;
        root.Q<FloatField>("AttendZ").value = 1;

        // Defaults for Gesture
        root.Q<Toggle>("GestureMonitor").value = false;
        root.Q<FloatField>("GestureIntensity").value = 1.0f;
        root.Q<FloatField>("GestureDuration").value = 1.0f;

        // LED Swatch Logic
        var hexField = root.Q<TextField>("LedColorHex");
        var swatch = root.Q<VisualElement>("LedColorSwatch");
        hexField.RegisterValueChangedCallback(evt => {
            if (ColorUtility.TryParseHtmlString(evt.newValue, out Color col)) {
                swatch.style.backgroundColor = col;
            }
        });
    }

    private void LogWarning(string msg) {
        _statusLog.text = "● " + msg;
        _statusLog.style.color = Color.red;
    }

private void LogRequestToUI(string json) {
    var root = GetComponent<UIDocument>().rootVisualElement;
    var scroll = root.Q<ScrollView>("ChatScroll");
    var data = Newtonsoft.Json.Linq.JObject.Parse(json);
    string type = data["type"]?.ToString() ?? "";

    bool shouldLog = false;
    string displayTitle = "";
    Color titleColor = new Color(0.1f, 0.1f, 0.1f); // Default near-black

    // 1. Filter and Format Logic
    if (type == "request.speak.text" && root.Q<Toggle>("LogSpeak").value) {
        shouldLog = true;
        displayTitle = $"Speech Text: \"{data["text"]}\"";
        titleColor = new Color(0f, 0.4f, 0f); // Dark Green
    }
    else if (type == "request.speak.audio" && root.Q<Toggle>("LogSpeak").value) {
        shouldLog = true;
        displayTitle = $"Speech Audio: \"{data["url"]}\"";
        titleColor = new Color(0f, 0.3f, 0.5f); // Darker Blue
    }
    else if (type == "request.listen" && root.Q<Toggle>("LogListen").value) {
        shouldLog = true;
        displayTitle = "Listen Requested";
        titleColor = new Color(0.5f, 0.4f, 0f); // Darker Gold/Olive
    }
    else if (type == "request.gesture" && root.Q<Toggle>("LogGesture").value) {
        shouldLog = true;
        displayTitle = $"Gesture: {data["name"]}";
        titleColor = new Color(0.4f, 0f, 0.4f); // Dark Purple
    }
    else if (type == "request.attend.location" && root.Q<Toggle>("LogGaze").value) {
        shouldLog = true;
        displayTitle = $"Gaze: ({data["x"]}, {data["y"]}, {data["z"]})";
        titleColor = new Color(0.6f, 0.2f, 0f); // Dark Rust/Orange
    }
    else if (type == "request.led.set" && root.Q<Toggle>("LogLed").value) {
        shouldLog = true;
        displayTitle = $"LED: {data["color"]}";
        titleColor = new Color(0.2f, 0.2f, 0.2f); // Dark Grey
    }

    // 2. Add to UI if it passed the filter
    if (shouldLog) {
        // Main Title Line
        var titleLabel = new Label($"[{DateTime.Now:HH:mm:ss}] {displayTitle}");
        titleLabel.AddToClassList("log-entry-label");
        titleLabel.style.color = titleColor;
        titleLabel.style.fontSize = 11;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        
        // Full JSON Detail Line (Bellow the title)
        var detailLabel = new Label($"  > {json}");
        detailLabel.AddToClassList("log-entry-label");
        detailLabel.style.color = new Color(0.4f, 0.4f, 0.4f); // Medium Grey for detail
        detailLabel.style.fontSize = 9;

        scroll.Add(titleLabel);
        scroll.Add(detailLabel);

        // Auto-scroll to bottom
        scroll.verticalScroller.value = scroll.verticalScroller.highValue;
    }
}

    private async void OnConnectClicked() {
        _statusLog.text = "● Connecting...";
        _statusLog.style.color = Color.yellow;
        await _client.Connect(_ipField.value);
        _statusLog.text = "● Connected";
        _statusLog.style.color = Color.green;
    }

    private void Update() => _client?.Update();
    private void OnDisable() => _client?.Dispose();
}