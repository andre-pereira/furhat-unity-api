using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

namespace Furhat.Runtime {
    public class FurhatClient : IDisposable {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        public event Action<string> OnMessageSent;
        public event Action<string> OnMessageReceived;

        public async Task Connect(string ip, string apiKey = null) {
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            
            Uri uri = new Uri($"ws://{ip}:9000/v1/events");
            await _ws.ConnectAsync(uri, _cts.Token);

            string authKey = string.IsNullOrEmpty(apiKey) ? "" : apiKey;
            await SendRaw($"{{\"type\": \"request.auth\", \"key\": \"{authKey}\"}}");

            _ = ReceiveLoop(); 
        }

        // --- SPEECH APIs ---

        public async Task Speak(string text, bool abort = false, bool monitorWords = false) {
            await Send(new SpeakRequest { 
                Text = text, 
                Abort = abort, 
                MonitorWords = monitorWords 
            });
        }

        public async Task SpeakAudio(string url, bool abort = false, bool lipsync = true, string label = "AUDIO") {
            await Send(new SpeakAudioRequest { 
                Url = url, 
                Abort = abort, 
                Lipsync = lipsync, 
                Text = label 
            });
        }

        public async Task StopSpeaking() => await Send(new StopSpeakingRequest());

        // --- LISTENING APIs ---

        public async Task StartListening(ListenRequest config) => await Send(config);

        public async Task StopListening() => await Send(new StopListenRequest());

        // --- ANIMATION & CONTROL APIs ---

        public async Task Gesture(string name, float intensity = 1.0f, float duration = 1.0f, bool monitor = false) {
            await Send(new GestureRequest { 
                Name = name, 
                Intensity = intensity, 
                Duration = duration, 
                Monitor = monitor 
            });
        }

        public async Task Attend(float x, float y, float z, string speed = "medium") {
            await Send(new AttendLocationRequest { 
                X = x, 
                Y = y, 
                Z = z, 
                Speed = speed 
            });
        }

        public async Task SetLed(string hexColor) {
            if (!hexColor.StartsWith("#")) hexColor = "#" + hexColor;
            await Send(new LedRequest { Color = hexColor });
        }

        // --- INTERNAL LOGIC ---

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
            var buffer = new byte[4096];
            while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested) {
                try {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _mainThreadQueue.Enqueue(() => OnMessageReceived?.Invoke(message));
                } catch { break; }
            }
        }

        public void Update() {
            while (_mainThreadQueue.TryDequeue(out var action)) action?.Invoke();
        }

        public void Dispose() {
            _cts?.Cancel();
            _ws?.Dispose();
        }
    }
}