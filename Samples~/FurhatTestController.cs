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
    private ScrollView _requestScroll;
    private ScrollView _systemScroll;
    private VisualElement _root;

    private void OnEnable() {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _ipField = _root.Q<TextField>("IpField");
        _statusLog = _root.Q<Label>("StatusLog");
        _requestSelector = _root.Q<DropdownField>("RequestTypeSelector");
        _requestScroll = _root.Q<ScrollView>("RequestScroll");
        _systemScroll = _root.Q<ScrollView>("SystemScroll");
        
        _root.Q<Button>("ConnectBtn").clicked += OnConnectClicked;
        _root.Q<Button>("OpenLogsBtn").clicked += () => Application.OpenURL("file://" + Application.persistentDataPath);

        _client = new FurhatClient();
        _client.OnMessageSent += msg => ProcessLogEntry("REQ", msg, _requestScroll);
        _client.OnMessageReceived += msg => ProcessLogEntry("RES", msg, _systemScroll);

        ApplyStartupDefaults(_root);
        SetupDynamicUI(_root);
        SetupPanelButtons(_root);
        SetupFilterCallbacks(_root);
    }

    private void SetupFilterCallbacks(VisualElement root) {
        string[] reqToggles = { "LogSpeak", "LogListen", "LogGesture", "LogGaze", "LogLed" };
        foreach (var t in reqToggles) root.Q<Toggle>(t).RegisterValueChangedCallback(_ => RefreshLogVisibility());

        string[] resToggles = { "SubSpeakStart", "SubSpeakEnd", "SubWords", "SubListenStart", "SubListenEnd", "SubHearStart", "SubHearEnd", "SubGestureStart", "SubGestureEnd" };
        foreach (var t in resToggles) root.Q<Toggle>(t).RegisterValueChangedCallback(_ => RefreshLogVisibility());
    }

    private void ProcessLogEntry(string direction, string json, ScrollView targetScroll) {
        var data = Newtonsoft.Json.Linq.JObject.Parse(json);
        string fullType = data["type"]?.ToString() ?? "unknown";
        
        FurhatFileLogger.Append(direction, fullType, json);

        // Display title is just the type name (stripped of "request." or "response.")
        string displayTitle = fullType.Replace("request.", "").Replace("response.", "");
        string categoryClass = direction == "REQ" ? GetReqCategory(fullType) : fullType.Replace(".", "-");

        // Add extra detail to the title if available
        if (fullType == "request.speak.text") displayTitle = $"Speak: {data["text"]}";
        else if (fullType == "response.speak.word") displayTitle = $"speak.word (\"{data["word"]}\")";
        else if (fullType == "request.speak.stop") displayTitle = "Speech Stop Requested";

        var entry = new VisualElement();
        entry.AddToClassList("log-entry-container");
        entry.AddToClassList(categoryClass); 

        // Removed the REQ | and RES | markers
        var titleLabel = new Label($"[{DateTime.Now:HH:mm:ss}] {displayTitle}");
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.fontSize = 11;
        // Keep text color consistent with panel (Black for Requests, Light Blue for Responses)
        titleLabel.style.color = direction == "REQ" ? Color.black : new Color(0.3f, 0.7f, 1f);
        
        var detailLabel = new Label($"  > {json}");
        detailLabel.style.color = direction == "REQ" ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
        detailLabel.style.fontSize = 9;

        entry.Add(titleLabel);
        entry.Add(detailLabel);
        targetScroll.Add(entry);

        RefreshLogVisibility();
    }

private string GetReqCategory(string type) {
        if (type.Contains("speak")) return "log-type-speak";
        if (type.Contains("listen")) return "log-type-listen";
        if (type.Contains("gesture")) return "log-type-gesture";
        if (type.Contains("attend") || type.Contains("gaze")) return "log-type-gaze";
        return "log-type-led";
    }
private void RefreshLogVisibility() {
        // --- Requests Column (Left) ---
        _requestScroll.Query<VisualElement>(className: "log-type-speak").ForEach(e => e.style.display = _root.Q<Toggle>("LogSpeak").value ? DisplayStyle.Flex : DisplayStyle.None);
        _requestScroll.Query<VisualElement>(className: "log-type-listen").ForEach(e => e.style.display = _root.Q<Toggle>("LogListen").value ? DisplayStyle.Flex : DisplayStyle.None);
        _requestScroll.Query<VisualElement>(className: "log-type-gesture").ForEach(e => e.style.display = _root.Q<Toggle>("LogGesture").value ? DisplayStyle.Flex : DisplayStyle.None);
        _requestScroll.Query<VisualElement>(className: "log-type-gaze").ForEach(e => e.style.display = _root.Q<Toggle>("LogGaze").value ? DisplayStyle.Flex : DisplayStyle.None);
        _requestScroll.Query<VisualElement>(className: "log-type-led").ForEach(e => e.style.display = _root.Q<Toggle>("LogLed").value ? DisplayStyle.Flex : DisplayStyle.None);

        // --- Responses Column (Right) ---
        _systemScroll.Query<VisualElement>(className: "response-speak-start").ForEach(e => e.style.display = _root.Q<Toggle>("SubSpeakStart").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-speak-end").ForEach(e => e.style.display = _root.Q<Toggle>("SubSpeakEnd").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-speak-word").ForEach(e => e.style.display = _root.Q<Toggle>("SubWords").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-listen-start").ForEach(e => e.style.display = _root.Q<Toggle>("SubListenStart").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-listen-end").ForEach(e => e.style.display = _root.Q<Toggle>("SubListenEnd").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-hear-start").ForEach(e => e.style.display = _root.Q<Toggle>("SubHearStart").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-hear-end").ForEach(e => e.style.display = _root.Q<Toggle>("SubHearEnd").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-gesture-start").ForEach(e => e.style.display = _root.Q<Toggle>("SubGestureStart").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-gesture-end").ForEach(e => e.style.display = _root.Q<Toggle>("SubGestureEnd").value ? DisplayStyle.Flex : DisplayStyle.None);
        
        _requestScroll.verticalScroller.value = _requestScroll.verticalScroller.highValue;
        _systemScroll.verticalScroller.value = _systemScroll.verticalScroller.highValue;
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
        root.Q<Button>("SendSpeakText").clicked += async () => {
            var txt = root.Q<TextField>("SpeakTextInput").value;
            if (string.IsNullOrWhiteSpace(txt)) { LogWarning("Text cannot be empty!"); return; }
            await _client.Speak(txt, root.Q<Toggle>("SpeakTextAbort").value, root.Q<Toggle>("SpeakTextMonitor").value);
        };

        root.Q<Button>("SendSpeakAudio").clicked += async () => {
            var url = root.Q<TextField>("AudioUrl").value;
            var label = root.Q<TextField>("AudioDisplayText").value;
            if (string.IsNullOrWhiteSpace(url)) { LogWarning("URL cannot be empty!"); return; }
            await _client.SpeakAudio(url, root.Q<Toggle>("AudioAbort").value, root.Q<Toggle>("AudioLipsync").value, label);
        };

        root.Q<Button>("SendGesture").clicked += async () => {
            var gName = root.Q<TextField>("GestureName").value;
            if (string.IsNullOrWhiteSpace(gName)) { LogWarning("Gesture name required!"); return; }
            await _client.Gesture(gName, Mathf.Clamp01(root.Q<FloatField>("GestureIntensity").value), Mathf.Max(0.1f, root.Q<FloatField>("GestureDuration").value), root.Q<Toggle>("GestureMonitor").value);
        };

        root.Q<Button>("SendListenStart").clicked += async () => {
            var config = new ListenRequest {
                Partial = root.Q<Toggle>("ListenPartial").value,
                Concat = root.Q<Toggle>("ListenConcat").value,
                StopNoSpeech = root.Q<Toggle>("ListenStopNoSpeech").value,
                StopRobotStart = root.Q<Toggle>("ListenStopRobot").value,
                StopUserEnd = root.Q<Toggle>("ListenStopUser").value,
                ResumeRobotEnd = root.Q<Toggle>("ListenResume").value,
                NoSpeechTimeout = root.Q<FloatField>("ListenNoSpeechTime").value,
                EndSpeechTimeout = root.Q<FloatField>("ListenEndSpeechTime").value
            };
            await _client.StartListening(config);
        };

        // FIXED: Renamed Attend() to AttendLocation() to match new Client
        root.Q<Button>("SendAttend").clicked += async () => {
            await _client.AttendLocation(
                root.Q<FloatField>("AttendX").value, 
                root.Q<FloatField>("AttendY").value, 
                root.Q<FloatField>("AttendZ").value, 
                root.Q<DropdownField>("AttendSpeed").value
            );
        };

        root.Q<Button>("SendLed").clicked += async () => {
            await _client.SetLed(root.Q<TextField>("LedColorHex").value);
        };

        root.Q<Button>("SendGenericStop").clicked += async () => {
            if (_requestSelector.value.Contains("Listen")) await _client.StopListening();
            else await _client.StopSpeaking();
        };
    }

    private void ApplyStartupDefaults(VisualElement root) {
        root.Q<TextField>("SpeakTextInput").value = "hello world";
        root.Q<Toggle>("SpeakTextAbort").value = true;
        root.Q<Toggle>("SpeakTextMonitor").value = true;
        root.Q<TextField>("AudioDisplayText").value = "Audio";
        root.Q<Toggle>("AudioAbort").value = true;
        root.Q<Toggle>("AudioLipsync").value = true;
        root.Q<Toggle>("ListenPartial").value = true;
        root.Q<Toggle>("ListenConcat").value = true;
        root.Q<Toggle>("ListenStopNoSpeech").value = true;
        root.Q<Toggle>("ListenStopRobot").value = true;
        root.Q<Toggle>("ListenStopUser").value = true;
        root.Q<Toggle>("ListenResume").value = false;
        root.Q<FloatField>("AttendX").value = 0;
        root.Q<FloatField>("AttendY").value = 0;
        root.Q<FloatField>("AttendZ").value = 1;
        root.Q<Toggle>("GestureMonitor").value = false;
        root.Q<FloatField>("GestureIntensity").value = 1.0f;
        root.Q<FloatField>("GestureDuration").value = 1.0f;

        var hexField = root.Q<TextField>("LedColorHex");
        var swatch = root.Q<VisualElement>("LedColorSwatch");
        hexField.RegisterValueChangedCallback(evt => {
            if (ColorUtility.TryParseHtmlString(evt.newValue, out Color col)) swatch.style.backgroundColor = col;
        });
    }

    private void LogWarning(string msg) {
        _statusLog.text = "● " + msg;
        _statusLog.style.color = Color.red;
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