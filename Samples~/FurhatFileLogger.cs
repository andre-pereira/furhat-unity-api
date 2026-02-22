using System;
using System.IO;
using UnityEngine;

public static class FurhatFileLogger {
    private static string _logPath;

    static FurhatFileLogger() {
        // Saves to your project folder or persistent data path
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logPath = Path.Combine(Application.persistentDataPath, $"FurhatLog_{timestamp}.txt");
        Debug.Log($"Logging session to: {_logPath}");
    }

    public static void Append(string direction, string type, string json) {
        try {
            string logLine = $"[{DateTime.Now:HH:mm:ss}] {direction} | {type} | {json}{Environment.NewLine}";
            File.AppendAllText(_logPath, logLine);
        } catch (Exception e) {
            Debug.LogError($"Failed to write to log file: {e.Message}");
        }
    }
}