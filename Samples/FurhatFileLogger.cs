using System;
using System.IO;
using System.Text;
using UnityEngine;
using Newtonsoft.Json.Linq;

public static class FurhatFileLogger {
    private static readonly object Sync = new object();
    private static string _sessionFolder;
    private static string _eventsPath;
    private static string _usersPath;
    private static string _audioPath;
    private static string _videoFramesFolder;
    private static StreamWriter _eventsWriter;
    private static StreamWriter _usersWriter;
    private static FileStream _audioStream;
    private static bool _sessionActive;
    private static int _videoFrameIndex;
    private static DateTime _sessionStartedAtUtc;
    private static int _audioSampleRate = 16000;
    private static short _audioChannels = 1;
    private static int _audioBytesWritten;
    private static bool _wavInitialized;

    public static string SessionFolder => _sessionFolder;

    public static void StartSession(bool logAudio, bool logVideo, bool logUsers, int sampleRate = 16000) {
        lock (Sync) {
            StopSession();

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _sessionFolder = Path.Combine(Application.persistentDataPath, "Logs", $"Session_{timestamp}");
            Directory.CreateDirectory(_sessionFolder);
            _sessionStartedAtUtc = DateTime.UtcNow;

            _eventsPath = Path.Combine(_sessionFolder, "events.jsonl");
            _usersPath = Path.Combine(_sessionFolder, "users_data.jsonl");
            _audioPath = Path.Combine(_sessionFolder, "audio.wav");
            _videoFramesFolder = Path.Combine(_sessionFolder, "video_frames");

            _eventsWriter = new StreamWriter(new FileStream(_eventsPath, FileMode.Create, FileAccess.Write, FileShare.Read));
            if (logUsers) {
                _usersWriter = new StreamWriter(new FileStream(_usersPath, FileMode.Create, FileAccess.Write, FileShare.Read));
            }

            _audioSampleRate = 16000;
            _audioChannels = 1;
            _audioBytesWritten = 0;
            _wavInitialized = false;
            if (logAudio) {
                _audioStream = new FileStream(_audioPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                WriteWavHeaderPlaceholder();
                _wavInitialized = true;
            }

            if (logVideo) {
                Directory.CreateDirectory(_videoFramesFolder);
                _videoFrameIndex = 0;
            } else {
                _videoFramesFolder = null;
            }

            _sessionActive = true;
            Debug.Log($"Furhat logging session: {_sessionFolder}");
        }
    }

    public static void StopSession() {
        lock (Sync) {
            if (!_sessionActive && _eventsWriter == null && _usersWriter == null && _audioStream == null) return;

            try {
                if (_audioStream != null && _wavInitialized) FinalizeWavHeader();
            } catch (Exception e) {
                Debug.LogWarning($"Failed finalizing WAV: {e.Message}");
            }

            _eventsWriter?.Flush();
            _usersWriter?.Flush();
            _audioStream?.Flush();

            _eventsWriter?.Dispose();
            _usersWriter?.Dispose();
            _audioStream?.Dispose();

            _eventsWriter = null;
            _usersWriter = null;
            _audioStream = null;
            _sessionActive = false;
        }
    }

    public static void Append(string direction, string type, string json) {
        if (type == "response.users.data") return;

        try {
            JToken payload;
            try {
                payload = JToken.Parse(json);
            } catch {
                payload = JValue.CreateString(json ?? string.Empty);
            }

            var entry = new JObject {
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["direction"] = direction,
                ["type"] = type,
                ["payload"] = payload
            };

            lock (Sync) {
                if (_eventsWriter == null) return;
                _eventsWriter.WriteLine(entry.ToString(Newtonsoft.Json.Formatting.None));
                _eventsWriter.Flush();
            }
        } catch (Exception e) {
            Debug.LogError($"Failed to write to log file: {e.Message}");
        }
    }

    public static void AppendUserData(string json) {
        try {
            JToken payload;
            try {
                payload = JToken.Parse(json);
            } catch {
                payload = JValue.CreateString(json ?? string.Empty);
            }

            var entry = new JObject {
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["payload"] = payload
            };

            lock (Sync) {
                if (_usersWriter == null) return;
                _usersWriter.WriteLine(entry.ToString(Newtonsoft.Json.Formatting.None));
                _usersWriter.Flush();
            }
        } catch (Exception e) {
            Debug.LogWarning($"Failed to write user data: {e.Message}");
        }
    }

    public static void AppendAudioBase64(string base64Audio) {
        if (string.IsNullOrEmpty(base64Audio)) return;

        try {
            byte[] bytes = Convert.FromBase64String(base64Audio);
            if (!TryExtractPcm16(bytes, out var pcmBytes, out int sourceSampleRate, out short sourceChannels)) return;

            byte[] normalized = NormalizeToMono16kPcm(pcmBytes, sourceSampleRate, sourceChannels);
            if (normalized == null || normalized.Length == 0) return;

            lock (Sync) {
                if (_audioStream == null) return;

                _audioSampleRate = 16000;
                _audioChannels = 1;

                _audioStream.Write(normalized, 0, normalized.Length);
                _audioBytesWritten += normalized.Length;
            }
        } catch (Exception e) {
            Debug.LogWarning($"Failed to append audio payload: {e.Message}");
        }
    }

    public static void AppendCameraFrameBase64(string base64Image) {
        if (string.IsNullOrEmpty(base64Image)) return;

        try {
            byte[] jpg = Convert.FromBase64String(base64Image);
            lock (Sync) {
                if (string.IsNullOrEmpty(_videoFramesFolder) || !Directory.Exists(_videoFramesFolder)) return;

                _videoFrameIndex++;
                long elapsedMs = (long)(DateTime.UtcNow - _sessionStartedAtUtc).TotalMilliseconds;
                string framePath = Path.Combine(_videoFramesFolder, $"frame_t{elapsedMs:D10}ms_{_videoFrameIndex:D6}.jpg");
                File.WriteAllBytes(framePath, jpg);
            }
        } catch (Exception e) {
            Debug.LogWarning($"Failed to append camera frame: {e.Message}");
        }
    }

    private static void WriteWavHeaderPlaceholder() {
        // 44-byte WAV header, sizes patched at session end.
        byte[] header = new byte[44];
        Array.Copy(Encoding.ASCII.GetBytes("RIFF"), 0, header, 0, 4);
        Array.Copy(BitConverter.GetBytes(36), 0, header, 4, 4);
        Array.Copy(Encoding.ASCII.GetBytes("WAVE"), 0, header, 8, 4);
        Array.Copy(Encoding.ASCII.GetBytes("fmt "), 0, header, 12, 4);
        Array.Copy(BitConverter.GetBytes(16), 0, header, 16, 4);
        Array.Copy(BitConverter.GetBytes((short)1), 0, header, 20, 2);
        Array.Copy(BitConverter.GetBytes(_audioChannels), 0, header, 22, 2);
        Array.Copy(BitConverter.GetBytes(_audioSampleRate), 0, header, 24, 4);
        int byteRate = _audioSampleRate * _audioChannels * 2;
        Array.Copy(BitConverter.GetBytes(byteRate), 0, header, 28, 4);
        short blockAlign = (short)(_audioChannels * 2);
        Array.Copy(BitConverter.GetBytes(blockAlign), 0, header, 32, 2);
        Array.Copy(BitConverter.GetBytes((short)16), 0, header, 34, 2);
        Array.Copy(Encoding.ASCII.GetBytes("data"), 0, header, 36, 4);
        Array.Copy(BitConverter.GetBytes(0), 0, header, 40, 4);
        _audioStream.Write(header, 0, header.Length);
    }

    private static void FinalizeWavHeader() {
        if (_audioStream == null) return;

        _audioStream.Seek(0, SeekOrigin.Begin);
        byte[] header = new byte[44];
        Array.Copy(Encoding.ASCII.GetBytes("RIFF"), 0, header, 0, 4);
        Array.Copy(BitConverter.GetBytes(36 + _audioBytesWritten), 0, header, 4, 4);
        Array.Copy(Encoding.ASCII.GetBytes("WAVE"), 0, header, 8, 4);
        Array.Copy(Encoding.ASCII.GetBytes("fmt "), 0, header, 12, 4);
        Array.Copy(BitConverter.GetBytes(16), 0, header, 16, 4);
        Array.Copy(BitConverter.GetBytes((short)1), 0, header, 20, 2);
        Array.Copy(BitConverter.GetBytes(_audioChannels), 0, header, 22, 2);
        Array.Copy(BitConverter.GetBytes(_audioSampleRate), 0, header, 24, 4);
        int byteRate = _audioSampleRate * _audioChannels * 2;
        Array.Copy(BitConverter.GetBytes(byteRate), 0, header, 28, 4);
        short blockAlign = (short)(_audioChannels * 2);
        Array.Copy(BitConverter.GetBytes(blockAlign), 0, header, 32, 2);
        Array.Copy(BitConverter.GetBytes((short)16), 0, header, 34, 2);
        Array.Copy(Encoding.ASCII.GetBytes("data"), 0, header, 36, 4);
        Array.Copy(BitConverter.GetBytes(_audioBytesWritten), 0, header, 40, 4);
        _audioStream.Write(header, 0, header.Length);
    }

    private static bool TryExtractPcm16(byte[] bytes, out byte[] pcmBytes, out int sampleRate, out short channels) {
        pcmBytes = Array.Empty<byte>();
        sampleRate = 16000;
        channels = 1;

        if (bytes == null || bytes.Length < 2) return false;

        if (bytes.Length > 44 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F') {
            int cursor = 12;
            int dataStart = -1;
            int dataLength = 0;

            while (cursor + 8 <= bytes.Length) {
                string chunk = Encoding.ASCII.GetString(bytes, cursor, 4);
                int chunkSize = BitConverter.ToInt32(bytes, cursor + 4);
                cursor += 8;

                if (chunk == "fmt " && chunkSize >= 16 && cursor + chunkSize <= bytes.Length) {
                    channels = (short)Mathf.Clamp(BitConverter.ToInt16(bytes, cursor + 2), 1, 2);
                    sampleRate = BitConverter.ToInt32(bytes, cursor + 4);
                }

                if (chunk == "data" && cursor + chunkSize <= bytes.Length) {
                    dataStart = cursor;
                    dataLength = chunkSize;
                    break;
                }

                cursor += chunkSize;
            }

            if (dataStart >= 0 && dataLength >= 2) {
                pcmBytes = new byte[dataLength];
                Buffer.BlockCopy(bytes, dataStart, pcmBytes, 0, dataLength);
                return true;
            }
        }

        // Fallback: treat payload as raw PCM16 mono.
        int length = bytes.Length - (bytes.Length % 2);
        if (length <= 0) return false;
        pcmBytes = new byte[length];
        Buffer.BlockCopy(bytes, 0, pcmBytes, 0, length);
        sampleRate = 16000;
        channels = 1;
        return true;
    }

    private static byte[] NormalizeToMono16kPcm(byte[] pcmBytes, int sourceSampleRate, short sourceChannels) {
        if (pcmBytes == null || pcmBytes.Length < 2) return Array.Empty<byte>();

        int channels = Mathf.Clamp(sourceChannels, 1, 2);
        int totalSamples = pcmBytes.Length / 2;
        if (totalSamples <= 0) return Array.Empty<byte>();

        int frameCount = totalSamples / channels;
        if (frameCount <= 0) return Array.Empty<byte>();

        // Decode to mono float first.
        float[] mono = new float[frameCount];
        int byteIndex = 0;
        for (int i = 0; i < frameCount; i++) {
            if (channels == 1) {
                short s = BitConverter.ToInt16(pcmBytes, byteIndex);
                mono[i] = s / 32768f;
                byteIndex += 2;
            } else {
                short l = BitConverter.ToInt16(pcmBytes, byteIndex);
                short r = BitConverter.ToInt16(pcmBytes, byteIndex + 2);
                mono[i] = ((l / 32768f) + (r / 32768f)) * 0.5f;
                byteIndex += 4;
            }
        }

        int srcRate = Mathf.Max(8000, sourceSampleRate);
        const int dstRate = 16000;
        if (srcRate == dstRate) {
            return FloatMonoToPcm16(mono);
        }

        // Linear interpolation resample to keep timeline correct.
        int outCount = Mathf.Max(1, Mathf.RoundToInt(mono.Length * (dstRate / (float)srcRate)));
        float[] resampled = new float[outCount];
        float step = srcRate / (float)dstRate;
        for (int i = 0; i < outCount; i++) {
            float srcPos = i * step;
            int i0 = Mathf.Clamp((int)srcPos, 0, mono.Length - 1);
            int i1 = Mathf.Min(i0 + 1, mono.Length - 1);
            float t = srcPos - i0;
            resampled[i] = mono[i0] * (1f - t) + mono[i1] * t;
        }

        return FloatMonoToPcm16(resampled);
    }

    private static byte[] FloatMonoToPcm16(float[] samples) {
        if (samples == null || samples.Length == 0) return Array.Empty<byte>();

        byte[] output = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++) {
            float clamped = Mathf.Clamp(samples[i], -1f, 1f);
            short pcm = (short)Mathf.RoundToInt(clamped * 32767f);
            byte[] pair = BitConverter.GetBytes(pcm);
            output[i * 2] = pair[0];
            output[i * 2 + 1] = pair[1];
        }

        return output;
    }
}