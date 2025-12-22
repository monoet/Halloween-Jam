using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BattleV2.Core
{
    /// <summary>
    /// "Flight Recorder" for the Battle System.
    /// Logs key events with high-precision timestamps to help debug race conditions and flow issues.
    /// </summary>
    public static class BattleDiagnostics
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Dev-only toggle for CP trace logs. Default false to avoid noise in prod.
        /// </summary>
        public static bool DevCpTrace = false;
#else
        public const bool DevCpTrace = false;
#endif

        private struct LogEntry
        {
            public double Timestamp;
            public string Category;
            public string Message;
            public Object Context;
        }

        private static readonly List<LogEntry> logs = new List<LogEntry>(1000);
        private static double startTime;

        static BattleDiagnostics()
        {
            startTime = Time.realtimeSinceStartupAsDouble;
        }

        public static void Log(string category, string message, Object context = null)
        {
            lock (logs)
            {
                logs.Add(new LogEntry
                {
                    Timestamp = Time.realtimeSinceStartupAsDouble - startTime,
                    Category = category,
                    Message = message,
                    Context = context
                });
            }
            
            // Optional: Mirror to Unity Console
            Debug.Log($"[{category}] {message}", context);
        }

        public static void Dump()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== BATTLE DIAGNOSTICS DUMP ===");
            lock (logs)
            {
                foreach (var log in logs)
                {
                    sb.AppendLine($"[{log.Timestamp:F3}s] [{log.Category}] {log.Message}");
                }
            }
            sb.AppendLine("===============================");
            Debug.Log(sb.ToString());
        }

        public static void Clear()
        {
            lock (logs)
            {
                logs.Clear();
                startTime = Time.realtimeSinceStartupAsDouble;
            }
        }
    }
}
