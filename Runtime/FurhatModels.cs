using Newtonsoft.Json;

namespace Furhat.Runtime {
    public abstract class FurhatRequest {
        [JsonProperty("type")] public abstract string Type { get; }
    }

    public class SpeakRequest : FurhatRequest {
        public override string Type => "request.speak.text"; 
        [JsonProperty("text")] public string Text { get; set; }
    }

    public class GestureRequest : FurhatRequest {
        public override string Type => "request.gesture"; 
        [JsonProperty("name")] public string Name { get; set; }
    }
}