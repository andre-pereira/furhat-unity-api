using Newtonsoft.Json;
using System.Collections.Generic;

namespace Furhat.Runtime {
    public abstract class FurhatRequest {
        [JsonProperty("type")] public abstract string Type { get; }
    }

    #region Speaking
    public class SpeakRequest : FurhatRequest {
        public override string Type => "request.speak.text"; 
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("abort")] public bool Abort { get; set; } = false;
        [JsonProperty("monitor_words")] public bool MonitorWords { get; set; } = false;
    }

    public class SpeakAudioRequest : FurhatRequest {
        public override string Type => "request.speak.audio";
        [JsonProperty("url")] public string Url { get; set; }
        [JsonProperty("abort")] public bool Abort { get; set; } = false;
        [JsonProperty("lipsync")] public bool Lipsync { get; set; } = true;
        [JsonProperty("text")] public string Text { get; set; } = "AUDIO"; 
    }

    public class StopSpeakingRequest : FurhatRequest {
        public override string Type => "request.speak.stop";
    }

    // Streaming Audio Requests
    public class SpeakAudioStartRequest : FurhatRequest {
        public override string Type => "request.speak.audio.start";
        [JsonProperty("sample_rate")] public int SampleRate { get; set; } = 16000;
        [JsonProperty("lipsync")] public bool Lipsync { get; set; } = true;
    }

    public class SpeakAudioDataRequest : FurhatRequest {
        public override string Type => "request.speak.audio.data";
        [JsonProperty("audio")] public string AudioBase64 { get; set; }
    }

    public class SpeakAudioEndRequest : FurhatRequest {
        public override string Type => "request.speak.audio.end";
    }
    #endregion

    #region Listening
    public class ListenConfigRequest : FurhatRequest {
        public override string Type => "request.listen.config";
        [JsonProperty("languages")] public List<string> Languages { get; set; } = new List<string> { "en-US" };
        [JsonProperty("phrases")] public List<string> Phrases { get; set; } = new List<string>();
    }

    public class ListenRequest : FurhatRequest {
        public override string Type => "request.listen.start";
        [JsonProperty("partial")] public bool Partial { get; set; } = false;
        [JsonProperty("concat")] public bool Concat { get; set; } = true;
        [JsonProperty("stop_no_speech")] public bool StopNoSpeech { get; set; } = true;
        [JsonProperty("stop_robot_start")] public bool StopRobotStart { get; set; } = true;
        [JsonProperty("stop_user_end")] public bool StopUserEnd { get; set; } = true;
        [JsonProperty("resume_robot_end")] public bool ResumeRobotEnd { get; set; } = false;
        [JsonProperty("no_speech_timeout")] public float NoSpeechTimeout { get; set; } = 8.0f;
        [JsonProperty("end_speech_timeout")] public float EndSpeechTimeout { get; set; } = 1.0f;
    }

    public class StopListenRequest : FurhatRequest {
        public override string Type => "request.listen.stop";
    }
    #endregion

    #region Voice
    public class VoiceConfigRequest : FurhatRequest {
        public override string Type => "request.voice.config";
        [JsonProperty("voice_id")] public string VoiceId { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("provider")] public string Provider { get; set; }
        [JsonProperty("language")] public string Language { get; set; }
        [JsonProperty("gender")] public string Gender { get; set; }
        [JsonProperty("input_language")] public bool InputLanguage { get; set; } = true;
    }

    public class VoiceStatusRequest : FurhatRequest {
        public override string Type => "request.voice.status";
        [JsonProperty("voice_id")] public bool CurrentVoiceId { get; set; } = true;
        [JsonProperty("voice_list")] public bool VoiceList { get; set; } = true;
    }
    #endregion

    #region Attention
    public class AttendUserRequest : FurhatRequest {
        public override string Type => "request.attend.user";
        [JsonProperty("user_id")] public string UserId { get; set; } = "closest";
        [JsonProperty("slack_pitch")] public float SlackPitch { get; set; } = 15f;
        [JsonProperty("slack_yaw")] public float SlackYaw { get; set; } = 5f;
        [JsonProperty("slack_timeout")] public int SlackTimeout { get; set; } = 3000;
        [JsonProperty("speed")] public string Speed { get; set; } = "medium";
    }

    public class AttendLocationRequest : FurhatRequest {
        public override string Type => "request.attend.location";
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("z")] public float Z { get; set; }
        [JsonProperty("slack_pitch")] public float SlackPitch { get; set; } = 15f;
        [JsonProperty("slack_yaw")] public float SlackYaw { get; set; } = 5f;
        [JsonProperty("slack_timeout")] public int SlackTimeout { get; set; } = 3000;
        [JsonProperty("speed")] public string Speed { get; set; } = "medium";
    }

    public class AttendNobodyRequest : FurhatRequest {
        public override string Type => "request.attend.nobody";
    }
    #endregion

    #region Face & Gestures
    public class GestureRequest : FurhatRequest {
        public override string Type => "request.gesture.start"; 
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("intensity")] public float Intensity { get; set; } = 1.0f;
        [JsonProperty("duration")] public float Duration { get; set; } = 1.0f;
        [JsonProperty("monitor")] public bool Monitor { get; set; } = false;
    }

    public class FaceParamsRequest : FurhatRequest {
        public override string Type => "request.face.params";
        [JsonProperty("params")] public Dictionary<string, float> Params { get; set; }
    }

    public class FaceHeadposeRequest : FurhatRequest {
        public override string Type => "request.face.headpose";
        [JsonProperty("yaw")] public float Yaw { get; set; } = 0f;
        [JsonProperty("pitch")] public float Pitch { get; set; } = 0f;
        [JsonProperty("roll")] public float Roll { get; set; } = 0f;
        [JsonProperty("relative")] public bool Relative { get; set; } = false;
        [JsonProperty("speed")] public string Speed { get; set; } = "medium";
    }

    public class FaceConfigRequest : FurhatRequest {
        public override string Type => "request.face.config";
        [JsonProperty("face_id")] public string FaceId { get; set; } = "KEEP";
        [JsonProperty("visibility")] public bool Visibility { get; set; } = true;
        [JsonProperty("microexpressions")] public bool Microexpressions { get; set; } = true;
        [JsonProperty("blinking")] public bool Blinking { get; set; } = true;
        [JsonProperty("head_sway")] public bool HeadSway { get; set; } = false;
    }

    public class FaceStatusRequest : FurhatRequest {
        public override string Type => "request.face.status";
        [JsonProperty("face_id")] public bool CurrentFaceId { get; set; } = true;
        [JsonProperty("face_list")] public bool FaceList { get; set; } = true;
    }

    public class FaceResetRequest : FurhatRequest {
        public override string Type => "request.face.reset";
    }
    #endregion

    #region LED & Sensors
    public class LedRequest : FurhatRequest {
        public override string Type => "request.led.set";
        [JsonProperty("color")] public string Color { get; set; } 
    }

    public class UsersOnceRequest : FurhatRequest { public override string Type => "request.users.once"; }
    public class UsersStartRequest : FurhatRequest { public override string Type => "request.users.start"; }
    public class UsersStopRequest : FurhatRequest { public override string Type => "request.users.stop"; }

    public class AudioStartRequest : FurhatRequest {
        public override string Type => "request.audio.start";
        [JsonProperty("sample_rate")] public int SampleRate { get; set; } = 16000;
        [JsonProperty("microphone")] public bool Microphone { get; set; } = true;
        [JsonProperty("speaker")] public bool Speaker { get; set; } = false;
    }

    public class AudioStopRequest : FurhatRequest { public override string Type => "request.audio.stop"; }

    public class CameraOnceRequest : FurhatRequest { public override string Type => "request.camera.once"; }
    public class CameraStartRequest : FurhatRequest { public override string Type => "request.camera.start"; }
    public class CameraStopRequest : FurhatRequest { public override string Type => "request.camera.stop"; }
    #endregion
}