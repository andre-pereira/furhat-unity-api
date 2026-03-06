using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Serialization;
using Furhat.Runtime;
using System.Collections.Generic;
using System;
using Newtonsoft.Json.Linq;

public enum StartupAudioLoggingMode {
    None,
    Microphone,
    Speaker,
    Both
}

public class FurhatTestController : MonoBehaviour {
    private FurhatClient _client;
    private Label _statusLog;
    private TextField _ipField;
    private DropdownField _requestSelector;
    private ScrollView _requestScroll;
    private ScrollView _systemScroll;
    private ScrollView _sensorScroll;
    private Toggle _collectCameraDataToggle;
    private DropdownField _collectAudioDataModeDropdown;
    private Toggle _collectUserDataToggle;
    private Toggle _audioPlaybackToggle;
    private Image _liveCameraImage;
    private Texture2D _cameraTexture;
    [SerializeField] private AudioSource _sensorAudioSource;
    private Button _connectButton;
    private Button _disconnectButton;
    private bool _cameraStreamActive;
    private bool _audioCaptureActive;
    private bool _userDetectionActive;
    private bool _sessionLogVideo;
    private bool _sessionLogUsers;
    private string _sessionAudioMode = "None";
    private VisualElement _root;
    [SerializeField] private string ipAddress = "127.0.0.1";
    [SerializeField] private bool startWithVideoLogging;
    [SerializeField] private StartupAudioLoggingMode startWithAudioLoggingMode = StartupAudioLoggingMode.None;
    [SerializeField, HideInInspector, FormerlySerializedAs("startWithAudioLogging")] private bool startWithAudioLoggingLegacy;
    [SerializeField] private bool startWithUserDataLogging;
    [SerializeField] private bool autoConnectOnEnable;
    private const int SensorAudioSampleRate = 16000;

    private void OnEnable() {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _ipField = _root.Q<TextField>("IpField");
        _statusLog = _root.Q<Label>("StatusLog");
        _requestSelector = _root.Q<DropdownField>("RequestTypeSelector");
        _requestScroll = _root.Q<ScrollView>("RequestScroll");
        _systemScroll = _root.Q<ScrollView>("SystemScroll");
        _sensorScroll = _root.Q<ScrollView>("SensorScroll");
        _collectCameraDataToggle = _root.Q<Toggle>("CollectCameraData");
        _collectAudioDataModeDropdown = _root.Q<DropdownField>("CollectAudioDataMode");
        _collectUserDataToggle = _root.Q<Toggle>("CollectUserData");
        _audioPlaybackToggle = _root.Q<Toggle>("AudioPlayback");
        _liveCameraImage = _root.Q<Image>("LiveCameraImage");
        _connectButton = _root.Q<Button>("ConnectBtn");
        _disconnectButton = _root.Q<Button>("DisconnectBtn");
        
        _connectButton.clicked += OnConnectClicked;
        _disconnectButton.clicked += OnDisconnectClicked;
        _root.Q<Button>("OpenLogsBtn").clicked += () => Application.OpenURL("file://" + Application.persistentDataPath);

        _client = new FurhatClient();
        if (_sensorAudioSource == null) _sensorAudioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        _sensorAudioSource.playOnAwake = false;
        _sensorAudioSource.loop = false;
        _sensorAudioSource.spatialBlend = 0f;

        // Keep UI input and Inspector default in sync.
        if (_ipField != null) _ipField.value = ipAddress;

        if (_collectCameraDataToggle != null) {
            _collectCameraDataToggle.RegisterValueChangedCallback(async evt => await ToggleCameraStreamAsync(evt.newValue));
        }

        if (_collectAudioDataModeDropdown != null) {
            _collectAudioDataModeDropdown.RegisterValueChangedCallback(async evt => await ApplyAudioCaptureSelectionAsync(evt.newValue));
        }
        
        _client.OnMessageSent += msg => ProcessLogEntry("REQ", msg, _requestScroll);
        _client.OnMessageReceived += msg => {
            try {
                var data = JObject.Parse(msg);
                string type = data["type"]?.ToString() ?? "";

                // Camera/audio payloads are handled directly by media handlers, not log panels.
                if (type == "response.camera.data" || type == "response.audio.data") return;

                if (type == "response.users.data" && _sessionLogUsers) {
                    FurhatFileLogger.AppendUserData(msg);
                }

                if (type == "response.users.data") {
                    UpdateLatestUserDataEntry(msg);
                    return;
                }

                ProcessLogEntry("RES", msg, _systemScroll);
            } catch {
                // Ignore malformed/fragmented payloads in the general log stream.
            }
        };

        _client.OnCameraSensorData += HandleCameraSensorData;
        _client.OnAudioSensorData += HandleAudioSensorData;
        ApplyStartupDefaults(_root);
        SetupDynamicUI(_root);
        SetupPanelButtons(_root);
        SetupFilterCallbacks(_root);

        if (autoConnectOnEnable) {
            _ = ConnectAsync();
        }
    }

    private void HandleCameraSensorData(CameraDataEvent data) {
        if (data == null || string.IsNullOrEmpty(data.ImageBase64)) return;

        if (_sessionLogVideo) {
            FurhatFileLogger.AppendCameraFrameBase64(data.ImageBase64);
        }

        if (_collectCameraDataToggle != null && !_collectCameraDataToggle.value) return;

        try {
            byte[] bytes = Convert.FromBase64String(data.ImageBase64);
            if (_cameraTexture == null) _cameraTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (_cameraTexture.LoadImage(bytes, false) && _liveCameraImage != null) {
                _liveCameraImage.image = _cameraTexture;
                _liveCameraImage.style.display = DisplayStyle.Flex;
            }
        } catch {
            // Ignore malformed sensor frames.
        }
    }

    private void HandleAudioSensorData(AudioDataEvent data) {
        if (data == null) return;

        string mode = _sessionAudioMode;
        bool allowSpeaker = mode == "Speaker" || mode == "Both";
        bool allowMic = mode == "Microphone" || mode == "Both";

        if (allowSpeaker && !string.IsNullOrEmpty(data.SpeakerBase64)) {
            FurhatFileLogger.AppendAudioBase64(data.SpeakerBase64);
            if (_audioPlaybackToggle == null || _audioPlaybackToggle.value) PlayAudioBase64(data.SpeakerBase64);
        }

        if (allowMic && !string.IsNullOrEmpty(data.MicrophoneBase64)) {
            FurhatFileLogger.AppendAudioBase64(data.MicrophoneBase64);
            if (_audioPlaybackToggle == null || _audioPlaybackToggle.value) PlayAudioBase64(data.MicrophoneBase64);
        }
    }

    private void PlayAudioBase64(string encodedAudio) {
        try {
            byte[] bytes = Convert.FromBase64String(encodedAudio);
            if (!TryDecodePcm16(bytes, out var samples, out int sampleRate, out int channels)) return;

            // Playback uses the payload's own PCM format to avoid speed/pitch drift.
            PlayAudioChunk(samples, sampleRate, channels);
        } catch {
            // Ignore malformed sensor audio chunks.
        }
    }

    private bool TryDecodePcm16(byte[] bytes, out float[] samples, out int sampleRate, out int channels) {
        samples = Array.Empty<float>();
        sampleRate = SensorAudioSampleRate;
        channels = 1;

        if (bytes == null || bytes.Length < 2) return false;

        // WAV payload (RIFF) with PCM16 data.
        if (bytes.Length > 44 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F') {
            int cursor = 12;
            int dataStart = -1;
            int dataLength = 0;

            while (cursor + 8 <= bytes.Length) {
                string chunk = System.Text.Encoding.ASCII.GetString(bytes, cursor, 4);
                int chunkSize = BitConverter.ToInt32(bytes, cursor + 4);
                cursor += 8;

                if (chunk == "fmt " && chunkSize >= 16 && cursor + chunkSize <= bytes.Length) {
                    channels = Mathf.Clamp(BitConverter.ToInt16(bytes, cursor + 2), 1, 2);
                    sampleRate = BitConverter.ToInt32(bytes, cursor + 4);
                }

                if (chunk == "data" && cursor + chunkSize <= bytes.Length) {
                    dataStart = cursor;
                    dataLength = chunkSize;
                    break;
                }

                cursor += chunkSize;
            }

            if (dataStart >= 0 && dataLength >= 2) {
                int count = dataLength / 2;
                samples = new float[count];
                for (int i = 0; i < count; i++) {
                    short pcm = BitConverter.ToInt16(bytes, dataStart + i * 2);
                    samples[i] = pcm / 32768f;
                }
                return true;
            }
        }

        // Fallback: treat as raw PCM16 mono at configured sample rate.
        int sampleCount = bytes.Length / 2;
        if (sampleCount == 0) return false;

        samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++) {
            short pcm = BitConverter.ToInt16(bytes, i * 2);
            samples[i] = pcm / 32768f;
        }

        return true;
    }

    private void PlayAudioChunk(float[] samples, int sampleRate, int channels) {
        if (_sensorAudioSource == null || samples == null || samples.Length == 0) return;

        int safeChannels = Mathf.Clamp(channels, 1, 2);
        int safeSampleRate = Mathf.Max(8000, sampleRate);
        int frameCount = samples.Length / safeChannels;
        if (frameCount <= 0) return;

        var clip = AudioClip.Create($"furhat-sensor-{Time.frameCount}", frameCount, safeChannels, safeSampleRate, false);
        clip.SetData(samples, 0);
        _sensorAudioSource.PlayOneShot(clip);

        Destroy(clip, Mathf.Max(0.25f, clip.length + 0.1f));
    }

    private void UpdateLatestUserDataEntry(string json) {
        if (_sensorScroll == null) return;

        _sensorScroll.Clear();

        var entry = new VisualElement();
        entry.AddToClassList("log-entry-container");

        string summary = "users.data";
        try {
            var data = JObject.Parse(json);
            int count = data["users"] is JArray users ? users.Count : 0;
            summary = $"users.data ({count} users)";
        } catch {
            // Keep fallback summary.
        }

        var titleLabel = new Label($"[{DateTime.Now:HH:mm:ss}] {summary}");
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.fontSize = 11;
        titleLabel.style.color = new Color(0.95f, 0.65f, 0.2f);

        var detailLabel = new Label($"  > {json}");
        detailLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
        detailLabel.style.fontSize = 9;

        entry.Add(titleLabel);
        entry.Add(detailLabel);
        _sensorScroll.Add(entry);
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
        if (type.Contains("users") || type.Contains("camera") || type.Contains("audio")) return "log-type-sensors";
        return "log-type-led";
    }

    private async System.Threading.Tasks.Task ToggleCameraStreamAsync(bool enabled) {
        if (_liveCameraImage != null) _liveCameraImage.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;

        if (_client == null || !_client.IsConnected) {
            _cameraStreamActive = false;
            return;
        }

        if (enabled) {
            if (_cameraStreamActive) return;
            await _client.StartCameraStream();
            _cameraStreamActive = true;
            return;
        }

        if (_cameraStreamActive) {
            await _client.StopCameraStream();
            _cameraStreamActive = false;
        }

        if (_liveCameraImage != null) _liveCameraImage.image = null;
    }

    private async System.Threading.Tasks.Task ApplyAudioCaptureSelectionAsync(string mode) {
        bool captureMic = mode == "Microphone" || mode == "Both";
        bool captureSpeaker = mode == "Speaker" || mode == "Both";
        bool wantsCapture = captureMic || captureSpeaker;

        if (_client == null || !_client.IsConnected) {
            _audioCaptureActive = false;
            return;
        }

        if (!wantsCapture) {
            if (_audioCaptureActive) {
                await _client.StopAudioCapture();
                _audioCaptureActive = false;
            }
            return;
        }

        if (_audioCaptureActive) {
            await _client.StopAudioCapture();
            _audioCaptureActive = false;
        }

        await _client.StartAudioCapture(SensorAudioSampleRate, mic: captureMic, speaker: captureSpeaker);
        _audioCaptureActive = true;
    }

    private void SetCollectionControlsLocked(bool connected) {
        if (_collectCameraDataToggle != null) _collectCameraDataToggle.SetEnabled(!connected);
        if (_collectAudioDataModeDropdown != null) _collectAudioDataModeDropdown.SetEnabled(!connected);
        if (_collectUserDataToggle != null) _collectUserDataToggle.SetEnabled(!connected);
        if (_ipField != null) _ipField.SetEnabled(!connected);
        if (_connectButton != null) _connectButton.SetEnabled(!connected);
        if (_disconnectButton != null) _disconnectButton.SetEnabled(connected);
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

        _requestSelector.SetValueWithoutNotify("Speak Text");
        foreach (var panel in panels.Values) if (panel != null) panel.style.display = DisplayStyle.None;
        if (panels["Speak Text"] != null) panels["Speak Text"].style.display = DisplayStyle.Flex;
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
        root.Q<Button>("SendCamStart").clicked += async () => {
            if (_collectCameraDataToggle != null) _collectCameraDataToggle.SetValueWithoutNotify(true);
            await ToggleCameraStreamAsync(true);
        };
        root.Q<Button>("SendCamStop").clicked += async () => {
            if (_collectCameraDataToggle != null) _collectCameraDataToggle.SetValueWithoutNotify(false);
            await ToggleCameraStreamAsync(false);
        };

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
        _requestSelector?.SetValueWithoutNotify("Speak Text");
        _collectCameraDataToggle?.SetValueWithoutNotify(startWithVideoLogging);
        StartupAudioLoggingMode resolvedAudioMode = startWithAudioLoggingMode;
        if (resolvedAudioMode == StartupAudioLoggingMode.None && startWithAudioLoggingLegacy) {
            resolvedAudioMode = StartupAudioLoggingMode.Both;
        }
        _collectAudioDataModeDropdown?.SetValueWithoutNotify(MapStartupAudioModeToDropdown(resolvedAudioMode));
        _collectUserDataToggle?.SetValueWithoutNotify(startWithUserDataLogging);
        _audioPlaybackToggle?.SetValueWithoutNotify(false);
        if (_collectCameraDataToggle != null && _liveCameraImage != null) {
            _liveCameraImage.style.display = _collectCameraDataToggle.value ? DisplayStyle.Flex : DisplayStyle.None;
        }
        SetCollectionControlsLocked(false);
        
        var hexField = root.Q<TextField>("LedColorHex");
        var swatch = root.Q<VisualElement>("LedColorSwatch");
        if (hexField != null && swatch != null) {
            hexField.RegisterValueChangedCallback(evt => {
                if (ColorUtility.TryParseHtmlString(evt.newValue, out Color col)) swatch.style.backgroundColor = col;
            });
        }
    }

    private static string MapStartupAudioModeToDropdown(StartupAudioLoggingMode mode) {
        switch (mode) {
            case StartupAudioLoggingMode.Microphone: return "Microphone";
            case StartupAudioLoggingMode.Speaker: return "Speaker";
            case StartupAudioLoggingMode.Both: return "Both";
            default: return "None";
        }
    }

    private void LogWarning(string msg) {
        _statusLog.text = "● " + msg;
        _statusLog.style.color = Color.red;
    }

    private async void OnConnectClicked() {
        await ConnectAsync();
    }

    private async void OnDisconnectClicked() {
        await DisconnectAsync();
    }

    private async System.Threading.Tasks.Task ConnectAsync() {
        if (_client == null || _client.IsConnected) return;

        _statusLog.text = "● Connecting...";
        _statusLog.style.color = Color.yellow;

        if (_ipField != null) ipAddress = _ipField.value;
        await _client.Connect(ipAddress);

        _sessionLogVideo = _collectCameraDataToggle != null && _collectCameraDataToggle.value;
        _sessionAudioMode = _collectAudioDataModeDropdown?.value ?? "None";
        _sessionLogUsers = _collectUserDataToggle != null && _collectUserDataToggle.value;
        FurhatFileLogger.StartSession(_sessionAudioMode != "None", _sessionLogVideo, _sessionLogUsers, SensorAudioSampleRate);
        SetCollectionControlsLocked(true);

        if (_collectCameraDataToggle != null) {
            await ToggleCameraStreamAsync(_collectCameraDataToggle.value);
        }

        if (_collectAudioDataModeDropdown != null) {
            await ApplyAudioCaptureSelectionAsync(_collectAudioDataModeDropdown.value ?? "None");
        }

        if (_collectUserDataToggle != null && _collectUserDataToggle.value) {
            await _client.StartUserDetection();
            _userDetectionActive = true;
        }

        _statusLog.text = "● Connected";
        _statusLog.style.color = Color.green;
    }

    private async System.Threading.Tasks.Task DisconnectAsync() {
        if (_client != null && _client.IsConnected) {
            if (_cameraStreamActive) {
                await _client.StopCameraStream();
                _cameraStreamActive = false;
            }

            if (_audioCaptureActive) {
                await _client.StopAudioCapture();
                _audioCaptureActive = false;
            }

            if (_userDetectionActive) {
                await _client.StopUserDetection();
                _userDetectionActive = false;
            }
        }

        _client?.Dispose();
        _client = new FurhatClient();
        _client.OnMessageSent += msg => ProcessLogEntry("REQ", msg, _requestScroll);
        _client.OnMessageReceived += msg => {
            try {
                var data = JObject.Parse(msg);
                string type = data["type"]?.ToString() ?? "";
                if (type == "response.camera.data" || type == "response.audio.data") return;
                if (type == "response.users.data") {
                    if (_sessionLogUsers) FurhatFileLogger.AppendUserData(msg);
                    UpdateLatestUserDataEntry(msg);
                    return;
                }
                ProcessLogEntry("RES", msg, _systemScroll);
            } catch { }
        };
        _client.OnCameraSensorData += HandleCameraSensorData;
        _client.OnAudioSensorData += HandleAudioSensorData;

        FurhatFileLogger.StopSession();
        SetCollectionControlsLocked(false);
        _sessionAudioMode = "None";
        _sessionLogVideo = false;
        _sessionLogUsers = false;

        _statusLog.text = "● Disconnected";
        _statusLog.style.color = new Color(0.91f, 0.3f, 0.24f);
    }

    private void Update() => _client?.Update();
    private void OnDisable() {
        FurhatFileLogger.StopSession();
        _client?.Dispose();
        _cameraStreamActive = false;
        _audioCaptureActive = false;
        _userDetectionActive = false;
        _sessionLogVideo = false;
        _sessionLogUsers = false;
        _sessionAudioMode = "None";

        if (_cameraTexture != null) {
            Destroy(_cameraTexture);
            _cameraTexture = null;
        }
    }
}