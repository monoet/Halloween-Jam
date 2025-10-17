using UnityEngine;

namespace BattleV2.Core
{
    /// <summary>
    /// Centralised logger for the new battle pipeline.
    /// Keeps log format consistent and easy to grep.
    /// </summary>
    public static class BattleLogger
    {
        public static void Log(string tag, string message)
        {
            Debug.Log($"[Battle:{tag}] {message}");
        }

        public static void Warn(string tag, string message)
        {
            Debug.LogWarning($"[Battle:{tag}] {message}");
        }

        public static void Error(string tag, string message)
        {
            Debug.LogError($"[Battle:{tag}] {message}");
        }
    }
}
