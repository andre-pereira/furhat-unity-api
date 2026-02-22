using Newtonsoft.Json;
using System.Collections.Generic;

namespace Furhat.Runtime {
    public class FurhatResponse {
        [JsonProperty("type")] public string Type { get; set; }
    }

    #region Speech Events
    public class SpeakStartEvent : FurhatResponse {
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("gen_time")] public float GenTime { get; set; } 
    }

    public class SpeakEndEvent : FurhatResponse {
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("aborted")] public bool Aborted { get; set; }
        [JsonProperty("failed")] public bool Failed { get; set; }
    }

    public class SpeakWordEvent : FurhatResponse {
        [JsonProperty("word")] public string Word { get; set; }
        [JsonProperty("index")] public int Index { get; set; }
    }

    public class SpeakAudioBufferEvent : FurhatResponse {
        [JsonProperty("played")] public int Played { get; set; }
        [JsonProperty("received")] public int Received { get; set; }
    }
    #endregion

    #region Listening Events
    public class ListenStartEvent : FurhatResponse { }

    public class ListenEndEvent : FurhatResponse {
        [JsonProperty("cause")] public string Cause { get; set; } // 'stopped', 'robot_speak', 'speech_end', 'silence_timeout'
    }

    public class HearStartEvent : FurhatResponse { }

    public class HearEndEvent : FurhatResponse {
        [JsonProperty("text")] public string Text { get; set; }
    }

    public class HearPartialEvent : FurhatResponse {
        [JsonProperty("text")] public string Text { get; set; }
    }
    #endregion

    #region Status Events
    public class VoiceStatusEvent : FurhatResponse {
        [JsonProperty("voice_id")] public string VoiceId { get; set; }
        [JsonProperty("voice_list")] public List<object> VoiceList { get; set; }
    }

    public class AttendStatusEvent : FurhatResponse {
        [JsonProperty("target")] public string Target { get; set; } // 'nobody', 'location', 'closest', or user ID
        [JsonProperty("current")] public string Current { get; set; } // 'nobody', 'location', or user ID
    }

    public class GestureStartEvent : FurhatResponse { }
    public class GestureEndEvent : FurhatResponse { }

    public class FaceStatusEvent : FurhatResponse {
        [JsonProperty("face_id")] public string FaceId { get; set; }
        [JsonProperty("face_list")] public List<string> FaceList { get; set; }
    }
    #endregion

    #region Sensor Data Events
    public class UsersDataEvent : FurhatResponse {
        [JsonProperty("users")] public List<FurhatUser> Users { get; set; }
    }

    public class FurhatUser {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("z")] public float Z { get; set; }
        // Add additional user properties if needed (e.g., yaw, pitch)
    }

    public class AudioDataEvent : FurhatResponse {
        [JsonProperty("microphone")] public string MicrophoneBase64 { get; set; }
        [JsonProperty("speaker")] public string SpeakerBase64 { get; set; }
    }

    public class CameraDataEvent : FurhatResponse {
        [JsonProperty("image")] public string ImageBase64 { get; set; } // JPEG format
    }
    #endregion
}