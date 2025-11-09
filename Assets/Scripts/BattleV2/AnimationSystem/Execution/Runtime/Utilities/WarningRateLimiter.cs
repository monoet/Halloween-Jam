using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Utilities
{
    /// <summary>
    /// Lightweight warning gate to avoid spamming the Unity console.
    /// </summary>
    public sealed class WarningRateLimiter
    {
        private readonly float intervalSeconds;
        private readonly string name;
        private readonly Dictionary<string, float> lastEmitTimes = new Dictionary<string, float>();

        public WarningRateLimiter(string name, float intervalSeconds = 0.5f)
        {
            this.name = string.IsNullOrWhiteSpace(name) ? "RateLimiter" : name;
            this.intervalSeconds = Mathf.Max(0.01f, intervalSeconds);
        }

        public bool TryWarn(string key, string message, Object context = null)
        {
            float now = Time.unscaledTime;
            bool allow = false;
            lock (lastEmitTimes)
            {
                if (!lastEmitTimes.TryGetValue(key, out var lastTime) || now - lastTime >= intervalSeconds)
                {
                    lastEmitTimes[key] = now;
                    allow = true;
                }
            }

            if (!allow)
            {
                return false;
            }

            if (context != null)
            {
                Debug.LogWarning($"[{name}] {message}", context);
            }
            else
            {
                Debug.LogWarning($"[{name}] {message}");
            }

            return true;
        }
    }
}
