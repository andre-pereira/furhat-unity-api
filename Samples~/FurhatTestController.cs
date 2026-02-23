using UnityEngine;
using UnityEngine.UIElements;
using Furhat.Runtime;
using System.Collections.Generic;
using System;
using Newtonsoft.Json.Linq;

public class FurhatTestController : MonoBehaviour {
    private FurhatClient _client;
    private Label _statusLog;
    private TextField _ipField;
    private DropdownField _requestSelector;
    private ScrollView _requestScroll;
    private ScrollView _systemScroll;
    private ScrollView _sensorScroll;
    private VisualElement _root;

    private void OnEnable() {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _ipField = _root.Q<TextField>("IpField");
        _statusLog = _root.Q<Label>("StatusLog");
        _requestSelector = _root.Q<DropdownField>("RequestTypeSelector");
        _requestScroll = _root.Q<ScrollView>("RequestScroll");
        _systemScroll = _root.Q<ScrollView>("SystemScroll");
        _sensorScroll = _root.Q<ScrollView>("SensorScroll"); 
        
        _root.Q<Button>("ConnectBtn").clicked += OnConnectClicked;
        _root.Q<Button>("OpenLogsBtn").clicked += () => Application.OpenURL("file://" + Application.persistentDataPath);

        _client = new FurhatClient();
        
        _client.OnMessageSent += msg => ProcessLogEntry("REQ", msg, _requestScroll);
        
        _client.OnMessageReceived += msg => {
            try {
                var data = JObject.Parse(msg);
                string type = data["type"]?.ToString() ?? "";
                
                bool isSensor = (type == "response.users.data" || type == "response.camera.data" || type == "response.audio.data");
                ProcessLogEntry("RES", msg, isSensor ? _sensorScroll : _systemScroll);
            } catch {
                // FALLBACK: If the payload (like the camera stream) is too massive and breaks the parser, 
                // we safely route it to the sensor panel without crashing Unity.
                ProcessLogEntry("RES", "{\"type\":\"response.camera.data\", \"message\":\"[Large Sensor Payload Received]\"}", _sensorScroll);
            }
        };

        ApplyStartupDefaults(_root);
        SetupDynamicUI(_root);
        SetupPanelButtons(_root);
        SetupFilterCallbacks(_root);
    }

    private void SetupFilterCallbacks(VisualElement root) {
        string[] reqToggles = { "LogSpeak", "LogListen", "LogGesture", "LogGaze", "LogLed", "LogSensors" };
        foreach (var t in reqToggles) root.Q<Toggle>(t)?.RegisterValueChangedCallback(_ => RefreshLogVisibility());

        string[] resToggles = { "SubSpeakStart", "SubSpeakEnd", "SubWords", "SubListenStart", "SubListenEnd", "SubHearStart", "SubHearEnd", "SubGestureStart", "SubGestureEnd", "SubVoice", "SubAttend", "SubFace" };
        foreach (var t in resToggles) root.Q<Toggle>(t)?.RegisterValueChangedCallback(_ => RefreshLogVisibility());
    }

    private void ProcessLogEntry(string direction, string json, ScrollView targetScroll) {
        string fullType = "unknown";
        string displayTitle = "";
        string categoryClass = "";

        try {
            var data = JObject.Parse(json);
            fullType = data["type"]?.ToString() ?? "unknown";

            displayTitle = fullType.Replace("request.", "").Replace("response.", "");
            categoryClass = direction == "REQ" ? GetReqCategory(fullType) : fullType.Replace(".", "-");

            if (fullType == "request.speak.text") displayTitle = $"Speak: {data["text"]}";
            else if (fullType == "response.speak.word") displayTitle = $"speak.word (\"{data["word"]}\")";
            else if (fullType == "request.speak.stop") displayTitle = "Speech Stop Requested";
        } catch {
            fullType = "parse_error";
            displayTitle = "Fragmented Payload";
            categoryClass = direction == "REQ" ? "log-type-led" : "response-fragment";
        }
        
        FurhatFileLogger.Append(direction, fullType, json);

        var entry = new VisualElement();
        entry.AddToClassList("log-entry-container");
        entry.AddToClassList(categoryClass); 

        var titleLabel = new Label($"[{DateTime.Now:HH:mm:ss}] {displayTitle}");
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.fontSize = 11;
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
        if (type.Contains("attend") || type.Contains("gaze") || type.Contains("face")) return "log-type-gaze";
        if (type.Contains("users") || type.Contains("camera") || type.Contains("audio.start")) return "log-type-sensors";
        return "log-type-led";
    }

    private void RefreshLogVisibility() {
        _requestScroll.Query<VisualElement>(className: "log-type-speak").ForEach(e => e.style.display = _root.Q<Toggle>("LogSpeak").value ? DisplayStyle.Flex : DisplayStyle.None);
        _requestScroll.Query<VisualElement>(className: "log-type-listen").ForEach(e => e.style.display = _root.Q<Toggle>("LogListen").value ? DisplayStyle.Flex : DisplayStyle.None);
        _requestScroll.Query<VisualElement>(className: "log-type-gesture").ForEach(e => e.style.display = _root.Q<Toggle>("LogGesture").value ? DisplayStyle.Flex : DisplayStyle.None);
        _requestScroll.Query<VisualElement>(className: "log-type-gaze").ForEach(e => e.style.display = _root.Q<Toggle>("LogGaze").value ? DisplayStyle.Flex : DisplayStyle.None);
        _requestScroll.Query<VisualElement>(className: "log-type-led").ForEach(e => e.style.display = _root.Q<Toggle>("LogLed").value ? DisplayStyle.Flex : DisplayStyle.None);
        _requestScroll.Query<VisualElement>(className: "log-type-sensors").ForEach(e => e.style.display = _root.Q<Toggle>("LogSensors").value ? DisplayStyle.Flex : DisplayStyle.None);

        _systemScroll.Query<VisualElement>(className: "response-speak-start").ForEach(e => e.style.display = _root.Q<Toggle>("SubSpeakStart").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-speak-end").ForEach(e => e.style.display = _root.Q<Toggle>("SubSpeakEnd").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-speak-word").ForEach(e => e.style.display = _root.Q<Toggle>("SubWords").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-listen-start").ForEach(e => e.style.display = _root.Q<Toggle>("SubListenStart").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-listen-end").ForEach(e => e.style.display = _root.Q<Toggle>("SubListenEnd").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-hear-start").ForEach(e => e.style.display = _root.Q<Toggle>("SubHearStart").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-hear-end").ForEach(e => e.style.display = _root.Q<Toggle>("SubHearEnd").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-gesture-start").ForEach(e => e.style.display = _root.Q<Toggle>("SubGestureStart").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-gesture-end").ForEach(e => e.style.display = _root.Q<Toggle>("SubGestureEnd").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-voice-status").ForEach(e => e.style.display = _root.Q<Toggle>("SubVoice").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-attend-status").ForEach(e => e.style.display = _root.Q<Toggle>("SubAttend").value ? DisplayStyle.Flex : DisplayStyle.None);
        _systemScroll.Query<VisualElement>(className: "response-face-status").ForEach(e => e.style.display = _root.Q<Toggle>("SubFace").value ? DisplayStyle.Flex : DisplayStyle.None);
        
        _requestScroll.verticalScroller.value = _requestScroll.verticalScroller.highValue;
        _systemScroll.verticalScroller.value = _systemScroll.verticalScroller.highValue;
        _sensorScroll.verticalScroller.value = _sensorScroll.verticalScroller.highValue;
    }

    private void SetupDynamicUI(VisualElement root) {
        var panels = new Dictionary<string, VisualElement> {
            { "Speak Text", root.Q<VisualElement>("PanelSpeakText") },
            { "Speak Audio", root.Q<VisualElement>("PanelSpeakAudio") },
            { "Speak Stop", root.Q<VisualElement>("PanelStopOnly") },
            { "Listen Config", root.Q<VisualElement>("PanelListenConfig") },
            { "Listen Start", root.Q<VisualElement>("PanelListenStart") },
            { "Listen Stop", root.Q<VisualElement>("PanelStopOnly") },
            { "Voice Config", root.Q<VisualElement>("PanelVoiceConfig") },
            { "Voice Status", root.Q<VisualElement>("PanelVoice") },
            { "Gesture", root.Q<VisualElement>("PanelGesture") },
            { "Attend Location", root.Q<VisualElement>("PanelAttend") },
            { "Attend User/Nobody", root.Q<VisualElement>("PanelAttendUser") },
            { "Face Config", root.Q<VisualElement>("PanelFaceConfig") },
            { "Face Control", root.Q<VisualElement>("PanelFace") },
            { "Sensors & System", root.Q<VisualElement>("PanelSensors") },
            { "LED Control", root.Q<VisualElement>("PanelLed") }
        };

        foreach (var p in panels.Values) if (p != null) p.style.display = DisplayStyle.None;
        panels["Speak Text"].style.display = DisplayStyle.Flex;

        _requestSelector.RegisterValueChangedCallback(evt => {
            foreach (var panel in panels.Values) if (panel != null) panel.style.display = DisplayStyle.None;
            if (panels.ContainsKey(evt.newValue) && panels[evt.newValue] != null) panels[evt.newValue].style.display = DisplayStyle.Flex;
        });
    }

    private void SetupPanelButtons(VisualElement root) {
        // -- Core --
        root.Q<Button>("SendSpeakText").clicked += async () => {
            var txt = root.Q<TextField>("SpeakTextInput").value;
            if (string.IsNullOrWhiteSpace(txt)) { LogWarning("Text cannot be empty!"); return; }
            await _client.Speak(txt, root.Q<Toggle>("SpeakTextAbort").value, root.Q<Toggle>("SpeakTextMonitor").value);
        };

        root.Q<Button>("SendSpeakAudio").clicked += async () => {
            var url = root.Q<TextField>("AudioUrl").value;
            if (string.IsNullOrWhiteSpace(url)) { LogWarning("URL cannot be empty!"); return; }
            await _client.SpeakAudio(url, root.Q<Toggle>("AudioAbort").value, root.Q<Toggle>("AudioLipsync").value, root.Q<TextField>("AudioDisplayText").value);
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

        root.Q<Button>("SendGenericStop").clicked += async () => {
            if (_requestSelector.value.Contains("Listen")) await _client.StopListening();
            else await _client.StopSpeaking();
        };

        // -- Configs (Using your actual API models!) --
        root.Q<Button>("SendListenConfig").clicked += async () => {
            string rawLangs = root.Q<TextField>("ListenLanguages").value;
            string rawPhrases = root.Q<TextField>("ListenPhrases").value;
            var langList = new List<string>(rawLangs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            var phraseList = new List<string>(rawPhrases.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            await _client.SetListenConfig(langList, phraseList);
        };

        root.Q<Button>("SendVoiceConfig").clicked += async () => {
            var config = new VoiceConfigRequest {
                VoiceId = string.IsNullOrEmpty(root.Q<TextField>("VoiceId").value) ? null : root.Q<TextField>("VoiceId").value,
                Name = string.IsNullOrEmpty(root.Q<TextField>("VoiceName").value) ? null : root.Q<TextField>("VoiceName").value,
                Provider = string.IsNullOrEmpty(root.Q<TextField>("VoiceProvider").value) ? null : root.Q<TextField>("VoiceProvider").value,
                Language = string.IsNullOrEmpty(root.Q<TextField>("VoiceLang").value) ? null : root.Q<TextField>("VoiceLang").value,
                Gender = string.IsNullOrEmpty(root.Q<TextField>("VoiceGender").value) ? null : root.Q<TextField>("VoiceGender").value,
                InputLanguage = root.Q<Toggle>("VoiceInputLang").value
            };
            await _client.SetVoice(config);
        };

        root.Q<Button>("SendFaceConfig").clicked += async () => {
            var config = new FaceConfigRequest {
                FaceId = string.IsNullOrEmpty(root.Q<TextField>("FaceId").value) ? "KEEP" : root.Q<TextField>("FaceId").value,
                Visibility = root.Q<Toggle>("FaceVisibility").value,
                Microexpressions = root.Q<Toggle>("FaceMicro").value,
                Blinking = root.Q<Toggle>("FaceBlink").value,
                HeadSway = root.Q<Toggle>("FaceSway").value
            };
            await _client.SetFaceConfig(config);
        };

        // -- Voice & Face Control --
        root.Q<Button>("ReqVoiceStatus").clicked += async () => await _client.RequestVoiceStatus();

        root.Q<Button>("SendFaceHeadpose").clicked += async () => {
            await _client.SetFaceHeadpose(
                root.Q<FloatField>("FaceYaw").value, root.Q<FloatField>("FacePitch").value, root.Q<FloatField>("FaceRoll").value
            );
        };
        
        root.Q<Button>("ReqFaceStatus").clicked += async () => await _client.RequestFaceStatus();
        root.Q<Button>("ReqFaceReset").clicked += async () => await _client.ResetFace();

        // -- Gestures & Attention --
        root.Q<Button>("SendGesture").clicked += async () => {
            var gName = root.Q<TextField>("GestureName").value;
            if (!string.IsNullOrWhiteSpace(gName)) await _client.Gesture(gName, Mathf.Clamp01(root.Q<FloatField>("GestureIntensity").value), Mathf.Max(0.1f, root.Q<FloatField>("GestureDuration").value), root.Q<Toggle>("GestureMonitor").value);
        };

        root.Q<Button>("SendAttend").clicked += async () => {
            await _client.AttendLocation(root.Q<FloatField>("AttendX").value, root.Q<FloatField>("AttendY").value, root.Q<FloatField>("AttendZ").value, root.Q<DropdownField>("AttendSpeed").value);
        };

        root.Q<Button>("SendAttendUser").clicked += async () => await _client.AttendUser(root.Q<TextField>("AttendUserId").value, root.Q<DropdownField>("AttendUserSpeed").value);
        root.Q<Button>("SendAttendNobody").clicked += async () => await _client.AttendNobody();

        // -- Sensors & System --
        root.Q<Button>("SendUsersStart").clicked += async () => await _client.StartUserDetection();
        root.Q<Button>("SendUsersStop").clicked += async () => await _client.StopUserDetection();
        root.Q<Button>("SendUsersOnce").clicked += async () => await _client.DetectUsersOnce();
        root.Q<Button>("SendCamStart").clicked += async () => await _client.StartCameraStream();
        root.Q<Button>("SendCamStop").clicked += async () => await _client.StopCameraStream();

        root.Q<Button>("SendLed").clicked += async () => await _client.SetLed(root.Q<TextField>("LedColorHex").value);
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
        
        var hexField = root.Q<TextField>("LedColorHex");
        var swatch = root.Q<VisualElement>("LedColorSwatch");
        if (hexField != null && swatch != null) {
            hexField.RegisterValueChangedCallback(evt => {
                if (ColorUtility.TryParseHtmlString(evt.newValue, out Color col)) swatch.style.backgroundColor = col;
            });
        }
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