using UnityEngine;

namespace BattleV2.UI.Diagnostics
{
    /// <summary>
    /// Lightweight debug helper for Mag menu tracing. Also provides Log/LogWarning/LogError shims
    /// to avoid breaking existing references to BattleV2.UI.Debug.*.
    /// </summary>
    public static class MagMenuDebug
    {
        public static bool Enabled = true;

        public static void Log(string code, string msg, Object ctx = null)
        {
            if (!Enabled) return;
            if (ctx != null) UnityEngine.Debug.Log($"UI_magmenudebug{code} {msg}", ctx);
            else UnityEngine.Debug.Log($"UI_magmenudebug{code} {msg}");
        }

        // Shims for existing calls expecting BattleV2.UI.Debug.Log/LogWarning/LogError
        public static void Log(string message, Object ctx = null)
        {
            if (ctx != null) UnityEngine.Debug.Log(message, ctx);
            else UnityEngine.Debug.Log(message);
        }

        public static void LogWarning(string message, Object ctx = null)
        {
            if (ctx != null) UnityEngine.Debug.LogWarning(message, ctx);
            else UnityEngine.Debug.LogWarning(message);
        }

        public static void LogError(string message, Object ctx = null)
        {
            if (ctx != null) UnityEngine.Debug.LogError(message, ctx);
            else UnityEngine.Debug.LogError(message);
        }
    }
}
