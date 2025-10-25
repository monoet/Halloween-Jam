using UnityEngine;

namespace BattleV2.Core
{
    /// <summary>
    /// Centralised logger for the new battle pipeline.
    /// Keeps log format consistent and easy to grep.
    /// </summary>
    public static class BattleLogger
    {
        public static event System.Action<string, string> OnLogged;

        public static void Log(string tag, string message)
        {
            var formatted = $"[Battle:{tag}] {message}";
            Debug.Log(formatted);
            OnLogged?.Invoke(tag, message);
        }

        public static void Warn(string tag, string message)
        {
            var formatted = $"[Battle:{tag}] {message}";
            Debug.LogWarning(formatted);
            OnLogged?.Invoke(tag, $"WARN: {message}");
        }

        public static void Error(string tag, string message)
        {
            var formatted = $"[Battle:{tag}] {message}";
            Debug.LogError(formatted);
            OnLogged?.Invoke(tag, $"ERROR: {message}");
        }
    }
}
