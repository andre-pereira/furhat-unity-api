using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Furhat.Runtime {
    public enum StartupAudioLoggingMode {
        None,
        Microphone,
        Speaker,
        Both
    }

    public enum FurhatAttendTarget {
        Nobody,
        Location,
        Closest,
        UserId
    }

    public enum FurhatAttendSpeed {
        XSlow,
        Slow,
        Medium,
        Fast,
        XFast
    }

    [Serializable]
    public class FurhatVoiceInfo {
        public string voiceId;
        public string name;
        public string provider;
        public string language;
        public string gender;

        public string DisplayLabel => string.IsNullOrWhiteSpace(name) ? voiceId : $"{voiceId} ({name})";
    }

    [Serializable]
    public class FurhatFaceParameterValue {
        public string name;
        public float value;
    }

    [Serializable]
    internal class FurhatRobotStatusCache {
        public List<FurhatVoiceInfo> availableVoices = new List<FurhatVoiceInfo>();
        public List<string> availableFaceModels = new List<string>();
    }

    public class FurhatRobot : MonoBehaviour {
        private const int SensorAudioSampleRate = 16000;

        [SerializeField] private string ipAddress = "127.0.0.1";
        [SerializeField] private string authenticationKey = "";
        [SerializeField] private bool connectOnStart;

        [SerializeField] private bool enableLogging = true;
        [SerializeField] private bool startWithVideoLogging;
        [SerializeField] private StartupAudioLoggingMode startWithAudioLoggingMode = StartupAudioLoggingMode.None;
        [SerializeField] private bool startWithUserDataLogging;

        [SerializeField] private string logRootDirectory = "";

        [SerializeField] private string voiceProviderFilter = "";
        [SerializeField] private string voiceLanguageFilter = "";
        [SerializeField] private string voiceGenderFilter = "";
        [SerializeField] private string voiceId = "";
        [SerializeField] private string faceModel = "adult - default";
        [SerializeField] private string ledColorHex = "#FFFFFF";
        [SerializeField] private bool faceVisibility = true;
        [SerializeField] private bool microexpressions = true;
        [SerializeField] private bool blinking = true;
        [SerializeField] private bool headSway;

        [SerializeField] private string listenLanguages = "en-US";
        [SerializeField] private string boostedListenPhrases = "";

        [SerializeField] private FurhatAttendTarget attendTarget = FurhatAttendTarget.Nobody;
        [SerializeField] private Vector3 attendLocation = new Vector3(0f, 0f, 1f);
        [SerializeField] private string attendUserId = "closest";
        [SerializeField] private FurhatAttendSpeed attendSpeed = FurhatAttendSpeed.Medium;

        [SerializeField] private List<FurhatFaceParameterValue> basicFaceParameters = new List<FurhatFaceParameterValue>();
        [SerializeField] private List<FurhatFaceParameterValue> arkitFaceParameters = new List<FurhatFaceParameterValue>();
        [SerializeField] private List<FurhatFaceParameterValue> charFaceParameters = new List<FurhatFaceParameterValue>();

        [SerializeField, HideInInspector] private bool isSpeaking;
        [SerializeField, HideInInspector] private bool isListening;
        [SerializeField, HideInInspector] private bool isGesturing;
        [SerializeField, HideInInspector] private string lastSpokenText = "";
        [SerializeField, HideInInspector] private string lastHeardText = "";
        [SerializeField, HideInInspector] private string lastGesturePlayed = "";
        [SerializeField, HideInInspector] private string currentAttendUserId = "";
        [SerializeField, HideInInspector] private List<FurhatVoiceInfo> availableVoices = new List<FurhatVoiceInfo>();
        [SerializeField, HideInInspector] private List<string> availableFaceModels = new List<string>();
        [NonSerialized] private string _spokenWordProgress = "";

        private FurhatClient _client;
        private bool _cameraStreamActive;
        private bool _audioCaptureActive;
        private bool _userDetectionActive;
        private bool _sessionLogVideo;
        private bool _sessionLogUsers;
        private string _sessionAudioMode = "None";
        private bool _disconnectInProgress;

        public event Action<string> OnMessageSent;
        public event Action<string> OnMessageReceived;
        public event Action<AudioDataEvent> OnAudioSensorData;
        public event Action<CameraDataEvent> OnCameraSensorData;
        public event Action<string, Color> OnStatusChanged;
        public event Action OnInspectorStateChanged;

        public FurhatClient Client => _client;
        public bool IsConnected => _client != null && _client.IsConnected;
        public string IpAddress {
            get => ipAddress;
            set => ipAddress = string.IsNullOrWhiteSpace(value) ? ipAddress : value;
        }

        public string LogRootDirectory => ResolveLogRootDirectory();
        public bool EnableLogging {
            get => enableLogging;
            set => enableLogging = value;
        }
        public string CurrentAudioLoggingMode => _sessionAudioMode;
        public bool CurrentVideoLogging => _sessionLogVideo;
        public bool CurrentUserLogging => _sessionLogUsers;
        public bool StartWithVideoLogging => startWithVideoLogging;
        public StartupAudioLoggingMode StartWithAudioLoggingMode => startWithAudioLoggingMode;
        public bool StartWithUserDataLogging => startWithUserDataLogging;

        public string VoiceProviderFilter => voiceProviderFilter;
        public string VoiceLanguageFilter => voiceLanguageFilter;
        public string VoiceGenderFilter => voiceGenderFilter;
        public string VoiceId => voiceId;
        public string FaceModel => faceModel;
        public string LedColorHex => ledColorHex;
        public bool FaceVisibility => faceVisibility;
        public bool Microexpressions => microexpressions;
        public bool Blinking => blinking;
        public bool HeadSway => headSway;
        public string ListenLanguages => listenLanguages;
        public string BoostedListenPhrases => boostedListenPhrases;
        public FurhatAttendTarget AttendTarget => attendTarget;
        public Vector3 AttendLocation => attendLocation;
        public string AttendUserId => attendUserId;
        public FurhatAttendSpeed AttendSpeed => attendSpeed;
        public bool IsSpeakingNow => isSpeaking;
        public bool IsListeningNow => isListening;
        public bool IsGesturingNow => isGesturing;
        public string LastSpokenText => lastSpokenText;
        public string LastHeardText => lastHeardText;
        public string LastGesturePlayed => lastGesturePlayed;
        public string CurrentAttendUserId => currentAttendUserId;
        public IReadOnlyList<FurhatVoiceInfo> AvailableVoices => availableVoices;
        public IReadOnlyList<string> AvailableFaceModels => availableFaceModels;
        public IReadOnlyList<FurhatFaceParameterValue> BasicFaceParameters => basicFaceParameters;
        public IReadOnlyList<FurhatFaceParameterValue> ArkitFaceParameters => arkitFaceParameters;
        public IReadOnlyList<FurhatFaceParameterValue> CharFaceParameters => charFaceParameters;

        private void Awake() {
            LoadPersistedStatusCache();
            EnsureFaceParameterDefinitions();
            EnsureClient();
        }

        private void Start() {
            if (connectOnStart) {
                _ = ConnectAsync();
            }
        }

        private void Update() {
            _client?.Update();
        }

        private void OnDisable() {
            _ = DisconnectAsync();
        }

        private void OnApplicationQuit() {
            _ = DisconnectAsync();
        }

        public async Task ConnectAsync(string ipOverride = null, bool? logVideoOverride = null, string audioModeOverride = null, bool? logUsersOverride = null) {
            EnsureClient();
            if (_client.IsConnected) return;

            string targetIp = string.IsNullOrWhiteSpace(ipOverride) ? ipAddress : ipOverride;
            bool requestedLogVideo = logVideoOverride ?? startWithVideoLogging;
            string requestedAudioMode = string.IsNullOrWhiteSpace(audioModeOverride)
                ? MapStartupAudioModeToDropdown(startWithAudioLoggingMode)
                : audioModeOverride;
            bool requestedLogUsers = logUsersOverride ?? startWithUserDataLogging;

            _sessionLogVideo = enableLogging && requestedLogVideo;
            _sessionAudioMode = enableLogging ? requestedAudioMode : "None";
            _sessionLogUsers = enableLogging && requestedLogUsers;

            try {
                OnStatusChanged?.Invoke("Connecting...", Color.yellow);
                if (enableLogging) {
                    FurhatLoggerBridge.StartSession(_sessionAudioMode, _sessionLogVideo, _sessionLogUsers, SensorAudioSampleRate, ResolveLogRootDirectory());
                }
                await _client.Connect(targetIp, authenticationKey);

                ipAddress = targetIp;
                await ApplyStartupRobotConfigurationAsync();

                if (_sessionLogVideo) {
                    await SetCameraStreamAsync(true);
                }

                await SetAudioCaptureModeAsync(_sessionAudioMode);

                if (_sessionLogUsers) {
                    await _client.StartUserDetection();
                    _userDetectionActive = true;
                }

                OnStatusChanged?.Invoke("Connected", Color.green);
                NotifyInspectorStateChanged();
            } catch (Exception ex) {
                Debug.LogWarning($"Furhat connect failed: {ex.Message}");
                OnStatusChanged?.Invoke("Connection failed", Color.red);
                await DisconnectAsync();
            }
        }

        public async Task DisconnectAsync() {
            if (_disconnectInProgress) return;
            _disconnectInProgress = true;

            try {
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
            } catch {
                // Ignore disconnect errors during shutdown.
            }

            _sessionAudioMode = "None";
            _sessionLogVideo = false;
            _sessionLogUsers = false;
            isSpeaking = false;
            isListening = false;
            isGesturing = false;

            FurhatLoggerBridge.StopSession();

            DisposeClient();
            EnsureClient();
            OnStatusChanged?.Invoke("Disconnected", new Color(0.91f, 0.3f, 0.24f));
            NotifyInspectorStateChanged();
            _disconnectInProgress = false;
        }

        public async Task SetCameraStreamAsync(bool enabled) {
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
        }

        public async Task SetAudioCaptureModeAsync(string mode) {
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
            }

            await _client.StartAudioCapture(SensorAudioSampleRate, mic: captureMic, speaker: captureSpeaker);
            _audioCaptureActive = true;
        }

        public async Task SetUserDetectionAsync(bool enabled) {
            if (_client == null || !_client.IsConnected) {
                _userDetectionActive = false;
                return;
            }

            if (enabled && !_userDetectionActive) {
                await _client.StartUserDetection();
                _userDetectionActive = true;
                return;
            }

            if (!enabled && _userDetectionActive) {
                await _client.StopUserDetection();
                _userDetectionActive = false;
            }
        }

        public async Task ApplyListenConfigAsync() {
            if (_client == null || !_client.IsConnected) return;

            await _client.SetListenConfig(ParseCommaSeparated(listenLanguages, "en-US"), ParseCommaSeparated(boostedListenPhrases));
        }

        public async Task ApplyVoiceConfigAsync() {
            if (_client == null || !_client.IsConnected) return;
            if (string.IsNullOrWhiteSpace(voiceId)) return;

            await _client.SetVoice(new VoiceConfigRequest {
                VoiceId = voiceId
            });
        }

        public async Task ApplyFaceConfigAsync() {
            if (_client == null || !_client.IsConnected) return;

            await _client.SetFaceConfig(new FaceConfigRequest {
                FaceId = string.IsNullOrWhiteSpace(faceModel) ? "KEEP" : faceModel,
                Visibility = faceVisibility,
                Microexpressions = microexpressions,
                Blinking = blinking,
                HeadSway = headSway
            });
        }

        public async Task ApplyLedAsync() {
            if (_client == null || !_client.IsConnected) return;
            if (string.IsNullOrWhiteSpace(ledColorHex)) return;

            await _client.SetLed(ledColorHex);
        }

        public async Task ApplyAttendTargetAsync() {
            if (_client == null || !_client.IsConnected) return;

            switch (attendTarget) {
                case FurhatAttendTarget.Location:
                    await _client.AttendLocation(attendLocation.x, attendLocation.y, attendLocation.z, ToApiAttendSpeed(attendSpeed));
                    break;
                case FurhatAttendTarget.Closest:
                    await _client.AttendUser("closest", ToApiAttendSpeed(attendSpeed));
                    break;
                case FurhatAttendTarget.UserId:
                    await _client.AttendUser(string.IsNullOrWhiteSpace(attendUserId) ? "closest" : attendUserId, ToApiAttendSpeed(attendSpeed));
                    break;
                default:
                    await _client.AttendNobody();
                    break;
            }
        }

        public async Task ApplyFaceParametersAsync() {
            if (_client == null || !_client.IsConnected) return;

            var parameters = new Dictionary<string, float>();
            AddNonDefaultFaceParameters(parameters, basicFaceParameters);
            AddNonDefaultFaceParameters(parameters, arkitFaceParameters);
            AddNonDefaultFaceParameters(parameters, charFaceParameters);

            if (parameters.Count == 0) return;
            await _client.SetFaceParams(parameters);
        }

        public async Task ResetFaceParametersAsync() {
            ZeroFaceParameters(basicFaceParameters);
            ZeroFaceParameters(arkitFaceParameters);
            ZeroFaceParameters(charFaceParameters);
            NotifyInspectorStateChanged();

            if (_client == null || !_client.IsConnected) return;
            await _client.ResetFace();
        }

        public async Task RefreshCachedRobotStatusAsync() {
            if (_client == null || !_client.IsConnected) return;

            await _client.RequestFaceStatus();
            await _client.RequestVoiceStatus();
        }

        public async Task RefreshVoiceStatusAsync() {
            if (_client == null || !_client.IsConnected) return;
            await _client.RequestVoiceStatus();
        }

        public async Task RefreshFaceStatusAsync() {
            if (_client == null || !_client.IsConnected) return;
            await _client.RequestFaceStatus();
        }

        public void LoadPersistedStatusCache() {
            string json = PlayerPrefs.GetString(GetStatusCacheKey(), "");
            if (string.IsNullOrWhiteSpace(json)) return;

            try {
                var cache = JsonUtility.FromJson<FurhatRobotStatusCache>(json);
                if (cache == null) return;

                availableVoices = cache.availableVoices ?? new List<FurhatVoiceInfo>();
                availableFaceModels = cache.availableFaceModels ?? new List<string>();
            } catch {
                availableVoices = new List<FurhatVoiceInfo>();
                availableFaceModels = new List<string>();
            }
        }

        public IEnumerable<FurhatVoiceInfo> GetFilteredVoices() {
            IEnumerable<FurhatVoiceInfo> query = availableVoices;

            if (!string.IsNullOrWhiteSpace(voiceProviderFilter)) {
                query = query.Where(v => ContainsIgnoreCase(v.provider, voiceProviderFilter));
            }

            if (!string.IsNullOrWhiteSpace(voiceLanguageFilter)) {
                query = query.Where(v => ContainsIgnoreCase(v.language, voiceLanguageFilter));
            }

            if (!string.IsNullOrWhiteSpace(voiceGenderFilter)) {
                query = query.Where(v => ContainsIgnoreCase(v.gender, voiceGenderFilter));
            }

            return query.OrderBy(v => v.voiceId, StringComparer.OrdinalIgnoreCase);
        }

        public void EnsureFaceParameterDefinitions() {
            EnsureFaceParameterList(basicFaceParameters, FurhatFaceParameterCatalog.BasicParams);
            EnsureFaceParameterList(arkitFaceParameters, FurhatFaceParameterCatalog.ARKitParams);
            EnsureFaceParameterList(charFaceParameters, FurhatFaceParameterCatalog.CharParams);
        }

        public void NotifyInspectorStateChanged() {
            OnInspectorStateChanged?.Invoke();
        }

        private async Task ApplyStartupRobotConfigurationAsync() {
            EnsureFaceParameterDefinitions();

            await ApplyVoiceConfigAsync();
            await ApplyFaceConfigAsync();
            await ApplyLedAsync();
            await ApplyListenConfigAsync();
            await ApplyAttendTargetAsync();
            await ApplyFaceParametersAsync();
            if (availableVoices.Count == 0 || availableFaceModels.Count == 0) {
                await RefreshCachedRobotStatusAsync();
            }
        }

        private void EnsureClient() {
            if (_client != null) return;

            _client = new FurhatClient();
            _client.OnMessageSent += HandleMessageSent;
            _client.OnMessageReceived += HandleMessageReceived;
            _client.OnAudioSensorData += HandleAudioSensorData;
            _client.OnCameraSensorData += HandleCameraSensorData;
        }

        private void DisposeClient() {
            if (_client == null) return;

            _client.OnMessageSent -= HandleMessageSent;
            _client.OnMessageReceived -= HandleMessageReceived;
            _client.OnAudioSensorData -= HandleAudioSensorData;
            _client.OnCameraSensorData -= HandleCameraSensorData;
            _client.Dispose();
            _client = null;
            _cameraStreamActive = false;
            _audioCaptureActive = false;
            _userDetectionActive = false;
        }

        private void HandleMessageSent(string msg) {
            UpdateCachedStateFromSentMessage(msg);
            FurhatLoggerBridge.Append("REQ", ExtractType(msg), msg);
            OnMessageSent?.Invoke(msg);
        }

        private void HandleMessageReceived(string msg) {
            string type = ExtractType(msg);
            if (type == "response.users.data" && _sessionLogUsers) {
                FurhatLoggerBridge.AppendUserData(msg);
            }

            UpdateCachedStateFromMessage(msg, type);
            FurhatLoggerBridge.Append("RES", type, msg);
            OnMessageReceived?.Invoke(msg);
        }

        private void HandleAudioSensorData(AudioDataEvent data) {
            if (data != null) {
                bool allowSpeaker = _sessionAudioMode == "Speaker" || _sessionAudioMode == "Both";
                bool allowMic = _sessionAudioMode == "Microphone" || _sessionAudioMode == "Both";

                if (allowSpeaker && !string.IsNullOrEmpty(data.SpeakerBase64)) {
                    FurhatLoggerBridge.AppendSpeakerAudioBase64(data.SpeakerBase64);
                }

                if (allowMic && !string.IsNullOrEmpty(data.MicrophoneBase64)) {
                    FurhatLoggerBridge.AppendMicAudioBase64(data.MicrophoneBase64);
                }
            }

            OnAudioSensorData?.Invoke(data);
        }

        private void HandleCameraSensorData(CameraDataEvent data) {
            if (_sessionLogVideo && data != null && !string.IsNullOrEmpty(data.ImageBase64)) {
                FurhatLoggerBridge.AppendCameraFrameBase64(data.ImageBase64);
            }

            OnCameraSensorData?.Invoke(data);
        }

        private void UpdateCachedStateFromMessage(string json, string type) {
            if (string.IsNullOrWhiteSpace(json)) return;

            try {
                var jo = JObject.Parse(json);
                switch (type) {
                    case "response.speak.start":
                        isSpeaking = true;
                        _spokenWordProgress = "";
                        lastSpokenText = jo["text"]?.ToString() ?? lastSpokenText;
                        break;
                    case "response.speak.word":
                        string spokenWord = jo["word"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(spokenWord)) {
                            _spokenWordProgress = string.IsNullOrWhiteSpace(_spokenWordProgress)
                                ? spokenWord
                                : _spokenWordProgress + " " + spokenWord;
                            lastSpokenText = _spokenWordProgress;
                        }
                        break;
                    case "response.speak.end":
                        isSpeaking = false;
                        _spokenWordProgress = "";
                        lastSpokenText = jo["text"]?.ToString() ?? lastSpokenText;
                        break;
                    case "response.listen.start":
                        isListening = true;
                        break;
                    case "response.listen.end":
                        isListening = false;
                        break;
                    case "response.hear.partial":
                        lastHeardText = jo["text"]?.ToString() ?? lastHeardText;
                        break;
                    case "response.hear.end":
                        lastHeardText = jo["text"]?.ToString() ?? lastHeardText;
                        break;
                    case "response.gesture.start":
                        isGesturing = true;
                        lastGesturePlayed = jo["name"]?.ToString() ?? lastGesturePlayed;
                        break;
                    case "response.gesture.end":
                        isGesturing = false;
                        break;
                    case "response.attend.status":
                        string target = jo["target"]?.ToString();
                        string current = jo["current"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(target)) {
                            attendTarget = ParseAttendTarget(target);
                        }
                        currentAttendUserId = ExtractAttendUserId(target, current);
                        if (!string.IsNullOrWhiteSpace(currentAttendUserId)) {
                            attendUserId = currentAttendUserId;
                        }
                        break;
                    case "response.voice.status":
                        ParseVoiceStatus(jo);
                        break;
                    case "response.face.status":
                        ParseFaceStatus(jo);
                        break;
                }

                NotifyInspectorStateChanged();
            } catch {
                // Ignore malformed payloads in status cache updates.
            }
        }

        private void ParseVoiceStatus(JObject jo) {
            var voiceArray = jo["voice_list"] as JArray;
            if (voiceArray != null) {
                availableVoices = voiceArray
                    .Select(ParseVoiceToken)
                    .Where(v => !string.IsNullOrWhiteSpace(v.voiceId))
                    .OrderBy(v => v.voiceId, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                SavePersistedStatusCache();
            }

            string currentVoiceId = jo["voice_id"]?.ToString();
            if (!string.IsNullOrWhiteSpace(currentVoiceId)) {
                voiceId = currentVoiceId;
            }
        }

        private void ParseFaceStatus(JObject jo) {
            availableFaceModels = jo["face_list"] is JArray faceArray
                ? faceArray.Values<string>().Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList()
                : availableFaceModels;

            string currentFaceId = jo["face_id"]?.ToString();
            if (!string.IsNullOrWhiteSpace(currentFaceId)) {
                faceModel = currentFaceId;
            }

            foreach (string variant in ExtractStringArray(jo, "texture_list", "textures", "mask_list", "masks")) {
                if (!availableFaceModels.Contains(variant)) {
                    availableFaceModels.Add(variant);
                }
            }

            availableFaceModels = availableFaceModels
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();
            SavePersistedStatusCache();
        }

        private static FurhatVoiceInfo ParseVoiceInfo(JObject jo) {
            return new FurhatVoiceInfo {
                voiceId = jo["voice_id"]?.ToString() ?? jo["id"]?.ToString() ?? "",
                name = jo["name"]?.ToString() ?? "",
                provider = jo["provider"]?.ToString() ?? "",
                language = jo["language"]?.ToString() ?? "",
                gender = jo["gender"]?.ToString() ?? ""
            };
        }

        private static FurhatVoiceInfo ParseVoiceToken(JToken token) {
            if (token is JObject jo) {
                return ParseVoiceInfo(jo);
            }

            string voiceId = token?.ToString() ?? "";
            return new FurhatVoiceInfo {
                voiceId = voiceId,
                name = "",
                provider = "",
                language = "",
                gender = ""
            };
        }

        private static List<string> ExtractStringArray(JObject jo, params string[] propertyNames) {
            foreach (string propertyName in propertyNames) {
                if (jo[propertyName] is JArray array) {
                    return array.Values<string>()
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct()
                        .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }

            return new List<string>();
        }

        private static string ExtractAttendUserId(string target, string current) {
            if (string.IsNullOrWhiteSpace(current)) return "";
            if (string.Equals(current, "nobody", StringComparison.OrdinalIgnoreCase)) return "";
            if (string.Equals(current, "location", StringComparison.OrdinalIgnoreCase)) return "";
            if (string.Equals(target, "location", StringComparison.OrdinalIgnoreCase)) return "";
            return current;
        }

        private static void EnsureFaceParameterList(List<FurhatFaceParameterValue> target, IReadOnlyList<string> definitions) {
            var existing = target.ToDictionary(item => item.name ?? "", item => item.value, StringComparer.Ordinal);
            target.Clear();

            foreach (string definition in definitions) {
                target.Add(new FurhatFaceParameterValue {
                    name = definition,
                    value = existing.TryGetValue(definition, out float preservedValue) ? preservedValue : 0f
                });
            }
        }

        private static void AddNonDefaultFaceParameters(Dictionary<string, float> parameters, IEnumerable<FurhatFaceParameterValue> source) {
            foreach (var item in source) {
                if (item == null || string.IsNullOrWhiteSpace(item.name)) continue;
                if (Mathf.Approximately(item.value, 0f)) continue;
                parameters[item.name] = item.value;
            }
        }

        private static void ZeroFaceParameters(IEnumerable<FurhatFaceParameterValue> source) {
            foreach (var item in source) {
                if (item == null) continue;
                item.value = 0f;
            }
        }

        private static List<string> ParseCommaSeparated(string source, params string[] fallbackValues) {
            var sanitized = source?
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (sanitized.Count > 0) return sanitized;
            return fallbackValues.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        }

        private static bool ContainsIgnoreCase(string value, string filter) {
            return !string.IsNullOrWhiteSpace(value)
                && !string.IsNullOrWhiteSpace(filter)
                && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static FurhatAttendTarget ParseAttendTarget(string value) {
            if (string.Equals(value, "location", StringComparison.OrdinalIgnoreCase)) return FurhatAttendTarget.Location;
            if (string.Equals(value, "closest", StringComparison.OrdinalIgnoreCase)) return FurhatAttendTarget.Closest;
            if (string.Equals(value, "nobody", StringComparison.OrdinalIgnoreCase)) return FurhatAttendTarget.Nobody;
            if (string.IsNullOrWhiteSpace(value)) return FurhatAttendTarget.Nobody;
            return FurhatAttendTarget.UserId;
        }

        private static string ToApiAttendSpeed(FurhatAttendSpeed speed) {
            switch (speed) {
                case FurhatAttendSpeed.XSlow: return "xslow";
                case FurhatAttendSpeed.Slow: return "slow";
                case FurhatAttendSpeed.Fast: return "fast";
                case FurhatAttendSpeed.XFast: return "xfast";
                default: return "medium";
            }
        }

        private void UpdateCachedStateFromSentMessage(string json) {
            if (string.IsNullOrWhiteSpace(json)) return;

            try {
                var jo = JObject.Parse(json);
                string type = jo["type"]?.ToString();
                if (type == "request.gesture.start") {
                    lastGesturePlayed = jo["name"]?.ToString() ?? lastGesturePlayed;
                    NotifyInspectorStateChanged();
                }
            } catch {
                // Ignore malformed payloads in outgoing cache updates.
            }
        }

        private void SavePersistedStatusCache() {
            try {
                var cache = new FurhatRobotStatusCache {
                    availableVoices = availableVoices ?? new List<FurhatVoiceInfo>(),
                    availableFaceModels = availableFaceModels ?? new List<string>()
                };

                PlayerPrefs.SetString(GetStatusCacheKey(), JsonUtility.ToJson(cache));
                PlayerPrefs.Save();
            } catch {
                // Ignore cache persistence failures.
            }
        }

        private string GetStatusCacheKey() {
            string keyIp = string.IsNullOrWhiteSpace(ipAddress) ? "default" : ipAddress.Trim();
            return $"FurhatRobot.StatusCache.{keyIp}";
        }

        private static string ExtractType(string json) {
            if (string.IsNullOrWhiteSpace(json)) return "unknown";

            try {
                var jo = JObject.Parse(json);
                return jo["type"]?.ToString() ?? "unknown";
            } catch {
                return "parse_error";
            }
        }

        private string ResolveLogRootDirectory() {
            if (string.IsNullOrWhiteSpace(logRootDirectory)) {
                return System.IO.Path.Combine(Application.persistentDataPath, "Logs");
            }

            if (System.IO.Path.IsPathRooted(logRootDirectory)) {
                return logRootDirectory;
            }

            return System.IO.Path.Combine(Application.persistentDataPath, logRootDirectory);
        }

        private static string MapStartupAudioModeToDropdown(StartupAudioLoggingMode mode) {
            switch (mode) {
                case StartupAudioLoggingMode.Microphone: return "Microphone";
                case StartupAudioLoggingMode.Speaker: return "Speaker";
                case StartupAudioLoggingMode.Both: return "Both";
                default: return "None";
            }
        }

        private static class FurhatLoggerBridge {
            private const string LoggerTypeName = "FurhatFileLogger";
            private static Type _loggerType;

            private static Type LoggerType {
                get {
                    if (_loggerType != null) return _loggerType;

                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                        var candidate = assembly.GetType(LoggerTypeName, false);
                        if (candidate != null) {
                            _loggerType = candidate;
                            return _loggerType;
                        }
                    }

                    return null;
                }
            }

            public static void StartSession(string audioMode, bool logVideo, bool logUsers, int sampleRate, string rootDirectory) {
                Invoke("StartSession", audioMode, logVideo, logUsers, sampleRate, rootDirectory);
            }

            public static void StopSession() {
                Invoke("StopSession");
            }

            public static void Append(string direction, string type, string json) {
                Invoke("Append", direction, type, json);
            }

            public static void AppendUserData(string json) {
                Invoke("AppendUserData", json);
            }

            public static void AppendSpeakerAudioBase64(string base64Audio) {
                Invoke("AppendSpeakerAudioBase64", base64Audio);
            }

            public static void AppendMicAudioBase64(string base64Audio) {
                Invoke("AppendMicAudioBase64", base64Audio);
            }

            public static void AppendCameraFrameBase64(string base64Image) {
                Invoke("AppendCameraFrameBase64", base64Image);
            }

            private static void Invoke(string methodName, params object[] args) {
                var type = LoggerType;
                if (type == null) return;

                try {
                    type.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, args);
                } catch {
                    // Ignore logging errors so robot control path remains stable.
                }
            }
        }
    }
}
