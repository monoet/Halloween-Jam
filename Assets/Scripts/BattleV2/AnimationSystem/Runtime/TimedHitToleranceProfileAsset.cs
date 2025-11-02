using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Scriptable definition of timed-hit tolerance overrides.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Animation/Timed Hit Tolerance Profile", fileName = "TimedHitToleranceProfile")]
    public sealed class TimedHitToleranceProfileAsset : ScriptableObject
    {
        [SerializeField] private ToleranceSettings defaultTolerance = ToleranceSettings.Default;
        [SerializeField] private Entry[] overrides = Array.Empty<Entry>();

        public TimedHitTolerance GetDefaultTolerance() => defaultTolerance.ToRuntime();

        public Dictionary<string, TimedHitTolerance> BuildOverridesDictionary(StringComparer comparer = null)
        {
            comparer ??= StringComparer.OrdinalIgnoreCase;
            var dict = new Dictionary<string, TimedHitTolerance>(comparer);

            if (overrides == null)
            {
                return dict;
            }

            for (int i = 0; i < overrides.Length; i++)
            {
                var entry = overrides[i];
                if (string.IsNullOrWhiteSpace(entry.Id))
                {
                    continue;
                }

                dict[entry.Id] = entry.Settings.ToRuntime();
            }

            return dict;
        }

        [Serializable]
        public struct Entry
        {
            [SerializeField] private string id;
            [SerializeField] private ToleranceSettings tolerance;

            public string Id => id;
            public ToleranceSettings Settings => tolerance;
        }

        [Serializable]
        public struct ToleranceSettings
        {
            [Min(0f)] public float perfectMilliseconds;
            [Min(0f)] public float goodMilliseconds;
            [Min(0f)] public float earlyMilliseconds;
            [Min(0f)] public float lateMilliseconds;

            public TimedHitTolerance ToRuntime() =>
                new(perfectMilliseconds, goodMilliseconds, earlyMilliseconds, lateMilliseconds);

            public static ToleranceSettings Default => new()
            {
                perfectMilliseconds = 45f,
                goodMilliseconds = 120f,
                earlyMilliseconds = 90f,
                lateMilliseconds = 90f
            };
        }
    }
}
