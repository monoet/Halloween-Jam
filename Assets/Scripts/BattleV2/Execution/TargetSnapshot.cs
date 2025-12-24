using System;
using System.Collections.Generic;
using BattleV2.Core;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
#endif

namespace BattleV2.Execution
{
    /// <summary>
    /// Snapshot helpers to avoid aliasing/mutation of target lists in P2-lite.
    /// </summary>
    public static class TargetSnapshot
    {
        public static CombatantState[] Snapshot(IReadOnlyList<CombatantState> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<CombatantState>();
            }

            var copy = new CombatantState[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

#if UNITY_EDITOR
            AssertUniqueAndNonNull(copy);
#endif

            return copy;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Stable, greppable ids for logging; returns null when DevFlowTrace is off to avoid allocations.
        /// </summary>
        public static string StableIds(IReadOnlyList<CombatantState> list)
        {
            if (!BattleDiagnostics.DevFlowTrace)
            {
                return null;
            }

            if (list == null || list.Count == 0)
            {
                return "[]";
            }

            int take = Math.Min(list.Count, 8);
            var parts = new string[take];
            for (int i = 0; i < take; i++)
            {
                var c = list[i];
                if (c == null)
                {
                    parts[i] = "(null)";
                    continue;
                }

                string prefix = c.IsEnemy ? "E" : "P";
                int id = c.GetInstanceID();
                parts[i] = $"{prefix}{id}";
            }

            string suffix = list.Count > take ? $",..+{list.Count - take}" : string.Empty;
            return $"[{string.Join(",", parts)}{suffix}]";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void AssertUniqueAndNonNull(IReadOnlyList<CombatantState> list)
        {
            if (list == null)
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                {
                    Debug.Assert(false, "[P2L] TargetSnapshot found null entry.");
                    return;
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null)
                {
                    continue;
                }
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (list[j] == a)
                    {
                        Debug.Assert(false, "[P2L] TargetSnapshot found duplicate entry.");
                        return;
                    }
                }
            }
        }
#endif
    }
}
