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

        // --- THE EASY ONE-LINERS ---
        public async Task Speak(string text) => await Send(new SpeakRequest { Text = text });
        public async Task SpeakAudio(string url, string label = "AUDIO") => 
            await Send(new SpeakAudioRequest { Url = url, Text = label });
        public async Task Gesture(string name) => await Send(new GestureRequest { Name = name });


        // --- INTERNAL LOGIC ---
        private async Task Send<T>(T request) where T : FurhatRequest {
            string json = JsonConvert.SerializeObject(request);
            await SendRaw(json);
        }

        private async Task SendRaw(string json) { // This is the method that was missing!
            if (_ws?.State != WebSocketState.Open) return;
            var buffer = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token);
        }

        private async Task ReceiveLoop() {
            var buffer = new byte[4096];
            while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested) {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _mainThreadQueue.Enqueue(() => OnMessageReceived?.Invoke(message));
            }
        }

        public void Update() { // Processes messages on Unity's main thread
            while (_mainThreadQueue.TryDequeue(out var action)) action?.Invoke();
        }

        public void Dispose() { _cts?.Cancel(); _ws?.Dispose(); }
    }
}