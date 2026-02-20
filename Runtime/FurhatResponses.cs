using Newtonsoft.Json;

namespace Furhat.Runtime {
    public class FurhatResponse {
        [JsonProperty("type")] public string Type { get; set; }
    }

    public class SpeakStartEvent : FurhatResponse {
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("gen_time")] public float GenTime { get; set; } //
    }

    public class SpeakEndEvent : FurhatResponse {
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("aborted")] public bool Aborted { get; set; } //
    }

    public class SpeakWordEvent : FurhatResponse {
        [JsonProperty("word")] public string Word { get; set; }
        [JsonProperty("index")] public int Index { get; set; } //
    }
}