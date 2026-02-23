using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Furhat.Runtime {
    public class FurhatClient : IDisposable {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        
        // --- Low-Level Log Events ---
        public event Action<string> OnMessageSent;
        public event Action<string> OnMessageReceived;

        // --- Specialized High-Level Event Callbacks ---
        public event Action<SpeakStartEvent> OnSpeechStart;
        public event Action<SpeakEndEvent> OnSpeechEnd;
        public event Action<SpeakWordEvent> OnSpeechWord;
        public event Action<SpeakAudioBufferEvent> OnAudioBufferUpdate;
        public event Action<ListenStartEvent> OnListenStart;
        public event Action<ListenEndEvent> OnListenEnd;
        public event Action<HearStartEvent> OnHearStart;
        public event Action<HearEndEvent> OnHearEnd;
        public event Action<HearPartialEvent> OnHearPartial;
        public event Action<VoiceStatusEvent> OnVoiceStatus;
        public event Action<AttendStatusEvent> OnAttendChanged;
        public event Action<GestureStartEvent> OnGestureStart;
        public event Action<GestureEndEvent> OnGestureEnd;
        public event Action<FaceStatusEvent> OnFaceStatus;
        public event Action<UsersDataEvent> OnUsersUpdate;
        public event Action<AudioDataEvent> OnAudioSensorData;
        public event Action<CameraDataEvent> OnCameraSensorData;

        public async Task Connect(string ip, string apiKey = null) {
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            Uri uri = new Uri($"ws://{ip}:9000/v1/events");
            await _ws.ConnectAsync(uri, _cts.Token);

            string authKey = string.IsNullOrEmpty(apiKey) ? "" : apiKey;
            await SendRaw($"{{\"type\": \"request.auth\", \"key\": \"{authKey}\"}}");
            _ = ReceiveLoop(); 
        }

        #region Speech APIs
        public async Task Speak(string text, bool abort = false, bool monitorWords = false) =>
            await Send(new SpeakRequest { Text = text, Abort = abort, MonitorWords = monitorWords });

        public async Task SpeakAudio(string url, bool abort = false, bool lipsync = true, string label = "AUDIO") =>
            await Send(new SpeakAudioRequest { Url = url, Abort = abort, Lipsync = lipsync, Text = label });

        public async Task StopSpeaking() => await Send(new StopSpeakingRequest());

        // Streaming Audio Commands
        public async Task StartAudioStream(int sampleRate = 16000, bool lipsync = true) =>
            await Send(new SpeakAudioStartRequest { SampleRate = sampleRate, Lipsync = lipsync });

        public async Task SendAudioStreamData(string base64Audio) =>
            await Send(new SpeakAudioDataRequest { AudioBase64 = base64Audio });

        public async Task EndAudioStream() => await Send(new SpeakAudioEndRequest());
        #endregion

        #region Listen & Voice APIs
        public async Task SetListenConfig(List<string> languages = null, List<string> phrases = null) =>
            await Send(new ListenConfigRequest { Languages = languages ?? new List<string>{"en-US"}, Phrases = phrases ?? new List<string>() });

        public async Task StartListening(ListenRequest config) => await Send(config);
        public async Task StopListening() => await Send(new StopListenRequest());

        public async Task SetVoice(VoiceConfigRequest config) => await Send(config);
        public async Task RequestVoiceStatus() => await Send(new VoiceStatusRequest());
        #endregion

        #region Attention & Gaze APIs
        public async Task AttendUser(string userId = "closest", string speed = "medium") =>
            await Send(new AttendUserRequest { UserId = userId, Speed = speed });

        public async Task AttendLocation(float x, float y, float z, string speed = "medium") =>
            await Send(new AttendLocationRequest { X = x, Y = y, Z = z, Speed = speed });

        public async Task AttendNobody() => await Send(new AttendNobodyRequest());
        #endregion

        #region Face & Gesture APIs
        public async Task Gesture(string name, float intensity = 1.0f, float duration = 1.0f, bool monitor = false) =>
            await Send(new GestureRequest { Name = name, Intensity = intensity, Duration = duration, Monitor = monitor });

        public async Task SetFaceHeadpose(float yaw, float pitch, float roll, bool relative = false, string speed = "medium") =>
            await Send(new FaceHeadposeRequest { Yaw = yaw, Pitch = pitch, Roll = roll, Relative = relative, Speed = speed });

        public async Task SetFaceParams(Dictionary<string, float> parameters) =>
            await Send(new FaceParamsRequest { Params = parameters });

        public async Task SetFaceConfig(FaceConfigRequest config) => await Send(config);
        public async Task RequestFaceStatus() => await Send(new FaceStatusRequest());
        public async Task ResetFace() => await Send(new FaceResetRequest());
        #endregion

        #region LED & Sensor APIs
        public async Task SetLed(string hexColor) {
            if (!hexColor.StartsWith("#")) hexColor = "#" + hexColor;
            await Send(new LedRequest { Color = hexColor });
        }

        public async Task StartUserDetection() => await Send(new UsersStartRequest());
        public async Task StopUserDetection() => await Send(new UsersStopRequest());
        public async Task DetectUsersOnce() => await Send(new UsersOnceRequest());

        public async Task StartAudioCapture(int sampleRate = 16000, bool mic = true, bool speaker = false) =>
            await Send(new AudioStartRequest { SampleRate = sampleRate, Microphone = mic, Speaker = speaker });

        public async Task StopAudioCapture() => await Send(new AudioStopRequest());

        public async Task StartCameraStream() => await Send(new CameraStartRequest());
        public async Task StopCameraStream() => await Send(new CameraStopRequest());
        public async Task CaptureCameraOnce() => await Send(new CameraOnceRequest());
        #endregion

        #region Internal Messaging Logic
        private async Task Send<T>(T request) where T : FurhatRequest {
            string json = JsonConvert.SerializeObject(request);
            _mainThreadQueue.Enqueue(() => OnMessageSent?.Invoke(json));
            await SendRaw(json);
        }

        private async Task SendRaw(string json) {
            if (_ws?.State != WebSocketState.Open) return;
            var buffer = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token);
        }

        private async Task ReceiveLoop() {
            var buffer = new byte[8192];
            while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested) {
                try {
                    using (var ms = new System.IO.MemoryStream()) {
                        WebSocketReceiveResult result;
                        do {
                            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                            ms.Write(buffer, 0, result.Count);
                        } while (!result.EndOfMessage); // Keep reading until the full payload arrives

                        if (result.MessageType == WebSocketMessageType.Close) break;

                        string msg = Encoding.UTF8.GetString(ms.ToArray());
                        _mainThreadQueue.Enqueue(() => OnMessageReceived?.Invoke(msg));
                    }
                } catch { break; }
            }
        }

        private void RouteEvent(string json) {
            OnMessageReceived?.Invoke(json);
            var jo = JObject.Parse(json);
            string type = jo["type"]?.ToString();
            if (string.IsNullOrEmpty(type)) return;

            // Exhaustive Event Routing
            switch (type) {
                case "response.speak.start": OnSpeechStart?.Invoke(jo.ToObject<SpeakStartEvent>()); break;
                case "response.speak.end": OnSpeechEnd?.Invoke(jo.ToObject<SpeakEndEvent>()); break;
                case "response.speak.word": OnSpeechWord?.Invoke(jo.ToObject<SpeakWordEvent>()); break;
                case "response.speak.audio.buffer": OnAudioBufferUpdate?.Invoke(jo.ToObject<SpeakAudioBufferEvent>()); break;
                case "response.listen.start": OnListenStart?.Invoke(jo.ToObject<ListenStartEvent>()); break;
                case "response.listen.end": OnListenEnd?.Invoke(jo.ToObject<ListenEndEvent>()); break;
                case "response.hear.start": OnHearStart?.Invoke(jo.ToObject<HearStartEvent>()); break;
                case "response.hear.end": OnHearEnd?.Invoke(jo.ToObject<HearEndEvent>()); break;
                case "response.hear.partial": OnHearPartial?.Invoke(jo.ToObject<HearPartialEvent>()); break;
                case "response.voice.status": OnVoiceStatus?.Invoke(jo.ToObject<VoiceStatusEvent>()); break;
                case "response.attend.status": OnAttendChanged?.Invoke(jo.ToObject<AttendStatusEvent>()); break;
                case "response.gesture.start": OnGestureStart?.Invoke(jo.ToObject<GestureStartEvent>()); break;
                case "response.gesture.end": OnGestureEnd?.Invoke(jo.ToObject<GestureEndEvent>()); break;
                case "response.face.status": OnFaceStatus?.Invoke(jo.ToObject<FaceStatusEvent>()); break;
                case "response.users.data": OnUsersUpdate?.Invoke(jo.ToObject<UsersDataEvent>()); break;
                case "response.audio.data": OnAudioSensorData?.Invoke(jo.ToObject<AudioDataEvent>()); break;
                case "response.camera.data": OnCameraSensorData?.Invoke(jo.ToObject<CameraDataEvent>()); break;
            }
        }

        public void Update() {
            while (_mainThreadQueue.TryDequeue(out var action)) action?.Invoke();
        }

        public void Dispose() {
            _cts?.Cancel();
            _ws?.Dispose();
        }
        #endregion
    }
}