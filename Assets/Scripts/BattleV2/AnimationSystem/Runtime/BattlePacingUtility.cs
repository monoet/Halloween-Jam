using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    internal static class BattlePacingUtility
    {
        public static BattlePacingSettings Settings => AnimationSystemInstaller.ActivePacing;
        public static bool IsActorDelaying(CombatantState actor)
        {
            if (actor == null)
            {
                return false;
            }

            lock (delayGate)
            {
                return delayingActors.Contains(actor);
            }
        }

        private static readonly HashSet<CombatantState> delayingActors = new HashSet<CombatantState>();
        private static readonly object delayGate = new object();

        private static string Timestamp
        {
            get
            {
                float t = Time.realtimeSinceStartup;
                int minutes = (int)(t / 60f);
                float seconds = t % 60f;
                return $"{minutes:00}:{seconds:00.000}";
            }
        }

        public static Task DelayAsync(float seconds, string phase, CombatantState actor, CancellationToken token)
        {
            return DelayTrackedAsync(seconds, phase, actor, token);
        }

        public static async Task DelayTrackedAsync(float seconds, string phase, CombatantState actor, CancellationToken token)
        {
            var clamped = Mathf.Max(0f, seconds);
            if (clamped <= 0f)
            {
                return;
            }

            if (actor != null)
            {
                lock (delayGate)
                {
                    delayingActors.Add(actor);
                }
            }

            Debug.Log($"TTDebug03 [DELAY_TRACK] phase={phase} actor={actor?.name ?? "(null)"} seconds={clamped:F3} time={Timestamp}");

            var delayMs = (int)(clamped * 1000f);
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected when token is cancelled; swallow
            }
            finally
            {
                if (actor != null)
                {
                    lock (delayGate)
                    {
                        delayingActors.Remove(actor);
                    }
                }
            }
        }

        public static Task DelayGlobalAsync(string sourceLabel, CancellationToken token = default)
        {
            var settings = Settings;
            var seconds = settings != null ? settings.globalTurnGap : 0f;
            return DelayAsync(seconds, $"{sourceLabel}_GlobalGap", null, token);
        }
    }
}
