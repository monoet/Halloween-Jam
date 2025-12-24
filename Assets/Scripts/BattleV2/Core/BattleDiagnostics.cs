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

        /// <summary>
        /// Dev-only toggle for animation lifecycle trace logs (idle/reset/plan-final hooks). Default false to avoid noise in prod.
        /// </summary>
        public static bool DevAnimTrace = false;

        /// <summary>
        /// Dev-only toggle for locomotion/tween trace logs (LOCOMOTIONTRACE). Default false to avoid noise in prod.
        /// </summary>
        public static bool DevLocomotionTrace = false;

        /// <summary>
        /// Dev-only toggle for unified battle flow logs (BATTLEFLOW). Use this when you need a single searchable tag
        /// that correlates multiple phases in the turn.
        /// </summary>
        public static bool DevFlowTrace = false;

        /// <summary>
        /// Dev-only toggle for P2-lite target snapshot logging.
        /// </summary>
        public static bool EnableP2LiteSnapshotLog = false;

        /// <summary>
        /// Dev-only toggle for P2-lite list shadow logging (LISTS/DIFF).
        /// </summary>
        public static bool EnableP2LiteListsShadow = false;

        /// <summary>
        /// Dev-only toggle for P2-lite resolve shadow logging (RESOLVE/DIFF/SKIP).
        /// </summary>
        public static bool EnableP2LiteResolveShadow = false;

        /// <summary>
        /// Dev-only toggle for P2-lite request logging (REQ).
        /// </summary>
        public static bool EnableP2LiteReqLog = false;
#else
        public const bool DevCpTrace = false;
        public const bool DevAnimTrace = false;
        public const bool DevLocomotionTrace = false;
        public const bool DevFlowTrace = false;
        public const bool EnableP2LiteSnapshotLog = false;
        public const bool EnableP2LiteListsShadow = false;
        public const bool EnableP2LiteResolveShadow = false;
        public const bool EnableP2LiteReqLog = false;
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
