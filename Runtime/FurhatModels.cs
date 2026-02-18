using Newtonsoft.Json;

namespace Furhat.Runtime {
    public abstract class FurhatRequest {
        [JsonProperty("type")] public abstract string Type { get; }
    }

    public class SpeakRequest : FurhatRequest {
        public override string Type => "request.speak.text"; 
        [JsonProperty("text")] public string Text { get; set; }
    }

    public class SpeakAudioRequest : FurhatRequest {
        public override string Type => "request.speak.audio";

        // This field is REQUIRED by the robot
        [JsonProperty("url")] 
        public string Url { get; set; }

        // Optional text for display purposes
        [JsonProperty("text")] 
        public string Text { get; set; } = "AUDIO";
    }

    public class GestureRequest : FurhatRequest {
        public override string Type => "request.gesture"; 
        [JsonProperty("name")] public string Name { get; set; }
    }
}