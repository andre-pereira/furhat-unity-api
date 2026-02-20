using Newtonsoft.Json;

namespace Furhat.Runtime {
    public abstract class FurhatRequest {
        [JsonProperty("type")] public abstract string Type { get; }
    }

    public class SpeakRequest : FurhatRequest {
        public override string Type => "request.speak.text"; 
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("abort")] public bool Abort { get; set; } = false;
        [JsonProperty("monitor_words")] public bool MonitorWords { get; set; } = false;
    }

    public class SpeakAudioRequest : FurhatRequest {
        public override string Type => "request.speak.audio";

        [JsonProperty("url")] 
        public string Url { get; set; }
        [JsonProperty("abort")] public bool Abort { get; set; } = false;
        [JsonProperty("lipsync")] public bool Lipsync { get; set; } = true;
        
        // optional label for the audio, used in events and logs
        [JsonProperty("text")] public string Text { get; set; } = "AUDIO"; 
    }

    public class StopSpeakingRequest : FurhatRequest {
        public override string Type => "request.speak.stop";
    }

    public class ListenRequest : FurhatRequest {
        public override string Type => "request.listen";
        [JsonProperty("partial")] public bool Partial { get; set; } = false;
        [JsonProperty("concat")] public bool Concat { get; set; } = true;
        [JsonProperty("stop_no_speech")] public bool StopNoSpeech { get; set; } = true;
        [JsonProperty("stop_robot_start")] public bool StopRobotStart { get; set; } = true;
        [JsonProperty("stop_user_end")] public bool StopUserEnd { get; set; } = true;
        [JsonProperty("resume_robot_end")] public bool ResumeRobotEnd { get; set; } = false;
        [JsonProperty("no_speech_timeout")] public float NoSpeechTimeout { get; set; } = 8.0f;
        [JsonProperty("end_speech_timeout")] public float EndSpeechTimeout { get; set; } = 1.0f;
    }

    // Separate request for stopping the listening
    public class StopListenRequest : FurhatRequest {
        public override string Type => "request.listen.stop";
    }

    public class GestureRequest : FurhatRequest {
        public override string Type => "request.gesture"; 
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("intensity")] public float Intensity { get; set; } = 1.0f;
        [JsonProperty("duration")] public float Duration { get; set; } = 1.0f;
        [JsonProperty("monitor")] public bool Monitor { get; set; } = false;
    }

    public class AttendLocationRequest : FurhatRequest {
        public override string Type => "request.attend.location";
        //positions in meters relative to the robot
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("z")] public float Z { get; set; }

        // Speed of the head movement (xslow, slow, medium, fast, xfast)
        [JsonProperty("speed")] public string Speed { get; set; } = "medium";
    }

    //LED control request
    public class LedRequest : FurhatRequest {
        public override string Type => "request.led.set";
        [JsonProperty("color")] public string Color { get; set; } // hex code
    }

}