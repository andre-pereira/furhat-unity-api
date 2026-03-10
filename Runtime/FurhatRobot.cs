using System;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Furhat.Runtime {
    public enum StartupAudioLoggingMode {
        None,
        Microphone,
        Speaker,
        Both
    }

    public class FurhatRobot : MonoBehaviour {
        private const int SensorAudioSampleRate = 16000;

        [Header("Connection Settings")]
        [SerializeField] private string ipAddress = "127.0.0.1";
        [SerializeField] private string apiKey = "";
        [SerializeField] private bool connectOnStart;

        [Header("Startup Logging")]
        [SerializeField] private bool startWithVideoLogging;
        [SerializeField] private StartupAudioLoggingMode startWithAudioLoggingMode = StartupAudioLoggingMode.None;
        [SerializeField] private bool startWithUserDataLogging;

        [Header("Logging")]
        [SerializeField] private string logRootDirectory = "";

        [Header("Startup Robot Configuration")]
        [SerializeField] private string startingVoiceId = "";
        [SerializeField] private string startingFaceModel = "KEEP";
        [SerializeField] private string startingTexture = "";
        [SerializeField] private string startingLedColorHex = "#FFFFFF";
        [SerializeField] private bool startingFaceVisibility = true;
        [SerializeField] private bool startingMicroexpressions = true;
        [SerializeField] private bool startingBlinking = true;
        [SerializeField] private bool startingHeadSway;

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

        public FurhatClient Client => _client;
        public bool IsConnected => _client != null && _client.IsConnected;

        public string IpAddress {
            get => ipAddress;
            set => ipAddress = string.IsNullOrWhiteSpace(value) ? ipAddress : value;
        }

        public string LogRootDirectory => ResolveLogRootDirectory();

        public string CurrentAudioLoggingMode => _sessionAudioMode;
        public bool CurrentVideoLogging => _sessionLogVideo;
        public bool CurrentUserLogging => _sessionLogUsers;
        public bool StartWithVideoLogging => startWithVideoLogging;
        public StartupAudioLoggingMode StartWithAudioLoggingMode => startWithAudioLoggingMode;
        public bool StartWithUserDataLogging => startWithUserDataLogging;

        private void Awake() {
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
            _sessionLogVideo = logVideoOverride ?? startWithVideoLogging;
            _sessionAudioMode = string.IsNullOrWhiteSpace(audioModeOverride)
                ? MapStartupAudioModeToDropdown(startWithAudioLoggingMode)
                : audioModeOverride;
            _sessionLogUsers = logUsersOverride ?? startWithUserDataLogging;

            try {
                OnStatusChanged?.Invoke("Connecting...", Color.yellow);
                await _client.Connect(targetIp, apiKey);

                await ApplyStartupRobotConfigurationAsync();

                FurhatLoggerBridge.StartSession(_sessionAudioMode, _sessionLogVideo, _sessionLogUsers, SensorAudioSampleRate, ResolveLogRootDirectory());

                if (_sessionLogVideo) {
                    await SetCameraStreamAsync(true);
                }

                await SetAudioCaptureModeAsync(_sessionAudioMode);

                if (_sessionLogUsers) {
                    await _client.StartUserDetection();
                    _userDetectionActive = true;
                }

                ipAddress = targetIp;
                OnStatusChanged?.Invoke("Connected", Color.green);
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

            FurhatLoggerBridge.StopSession();

            DisposeClient();
            EnsureClient();
            OnStatusChanged?.Invoke("Disconnected", new Color(0.91f, 0.3f, 0.24f));
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

        private async Task ApplyStartupRobotConfigurationAsync() {
            if (_client == null || !_client.IsConnected) return;

            if (!string.IsNullOrWhiteSpace(startingVoiceId)) {
                await _client.SetVoice(new VoiceConfigRequest { VoiceId = startingVoiceId });
            }

            await _client.SetFaceConfig(new FaceConfigRequest {
                FaceId = string.IsNullOrWhiteSpace(startingFaceModel) ? "KEEP" : startingFaceModel,
                Visibility = startingFaceVisibility,
                Microexpressions = startingMicroexpressions,
                Blinking = startingBlinking,
                HeadSway = startingHeadSway
            });

            if (!string.IsNullOrWhiteSpace(startingLedColorHex)) {
                await _client.SetLed(startingLedColorHex);
            }

            if (!string.IsNullOrWhiteSpace(startingTexture)) {
                Debug.Log("FurhatRobot: startingTexture is reserved for future API support.");
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
            FurhatLoggerBridge.Append("REQ", ExtractType(msg), msg);
            OnMessageSent?.Invoke(msg);
        }

        private void HandleMessageReceived(string msg) {
            string type = ExtractType(msg);
            if (type == "response.users.data" && _sessionLogUsers) {
                FurhatLoggerBridge.AppendUserData(msg);
            }
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

        private static string MapStartupAudioModeToDropdown(StartupAudioLoggingMode mode) {
            switch (mode) {
                case StartupAudioLoggingMode.Microphone: return "Microphone";
                case StartupAudioLoggingMode.Speaker: return "Speaker";
                case StartupAudioLoggingMode.Both: return "Both";
                default: return "None";
            }
        }
    }
}
