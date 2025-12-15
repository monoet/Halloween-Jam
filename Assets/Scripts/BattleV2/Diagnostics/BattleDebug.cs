using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using BattleV2.Core;

namespace BattleV2.Diagnostics
{
    /// <summary>
    /// Lightweight debug logger with channel toggles, designed for repro harnesses and A/B tests.
    /// Logs are prefixed like "[DEBUG-MS012] message".
    /// </summary>
    public static class BattleDebug
    {
        private const string PrefsPrefix = "BattleDebug.";
        private static readonly object sync = new();
        private static readonly Dictionary<string, bool> cache = new(StringComparer.OrdinalIgnoreCase);
        private static int mainThreadId = -1;
        private static IMainThreadInvoker invoker;

        public static int MainThreadId => mainThreadId;

        public static void ConfigureInvoker(IMainThreadInvoker mainThreadInvoker)
        {
            invoker = mainThreadInvoker;
        }

        public static void CaptureMainThread()
        {
            if (mainThreadId >= 0)
            {
                return;
            }

            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsMainThread
        {
            get
            {
                if (mainThreadId < 0)
                {
                    return true;
                }

                return Thread.CurrentThread.ManagedThreadId == mainThreadId;
            }
        }

        public static bool IsEnabled(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                return false;
            }

            lock (sync)
            {
                if (cache.TryGetValue(channel, out var value))
                {
                    return value;
                }
            }

            // Never touch PlayerPrefs off the main thread (Unity restriction).
            if (!IsMainThread)
            {
                return false;
            }

            bool enabled = PlayerPrefs.GetInt(PrefsPrefix + channel, 0) == 1;
            lock (sync)
            {
                cache[channel] = enabled;
            }
            return enabled;
        }

        public static void SetEnabled(string channel, bool enabled, bool persist = true)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                return;
            }

            lock (sync)
            {
                cache[channel] = enabled;
            }

            if (!persist)
            {
                return;
            }

            // Persist only on the main thread.
            if (IsMainThread)
            {
                PlayerPrefs.SetInt(PrefsPrefix + channel, enabled ? 1 : 0);
                return;
            }

            invoker?.Run(() => PlayerPrefs.SetInt(PrefsPrefix + channel, enabled ? 1 : 0));
        }

        public static void Log(string channel, int code, string message, UnityEngine.Object context = null)
        {
            if (!IsEnabled(channel))
            {
                return;
            }

            Write(LogType.Log, channel, code, message, context);
        }

        public static void Warn(string channel, int code, string message, UnityEngine.Object context = null)
        {
            if (!IsEnabled(channel))
            {
                return;
            }

            Write(LogType.Warning, channel, code, message, context);
        }

        public static void Error(string channel, int code, string message, UnityEngine.Object context = null)
        {
            if (!IsEnabled(channel))
            {
                return;
            }

            Write(LogType.Error, channel, code, message, context);
        }

        private static void Write(LogType type, string channel, int code, string message, UnityEngine.Object context)
        {
            var prefix = $"[DEBUG-{channel}{code:000}]";
            var full = string.IsNullOrWhiteSpace(message) ? prefix : $"{prefix} {message}";

            switch (type)
            {
                case LogType.Warning:
                    Debug.LogWarning(full, context);
                    break;
                case LogType.Error:
                    Debug.LogError(full, context);
                    break;
                default:
                    Debug.Log(full, context);
                    break;
            }
        }
    }
}
