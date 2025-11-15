using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime.Telemetry;
using BattleV2.Core;
using DG.Tweening;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    internal static class StepSchedulerIdleUtility
    {
        public static bool IsActorIdle(CombatantState actor)
        {
            if (actor == null)
            {
                return true;
            }

            bool tweensAlive = HasTweensOn(actor.transform);
            bool schedulerBusy = StepSchedulerMetricsObserver.HasActiveSteps(actor);
            bool delaying = BattlePacingUtility.IsActorDelaying(actor);

            return !tweensAlive && !schedulerBusy && !delaying;
        }

        private static bool HasTweensOn(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            var tweens = DOTween.PlayingTweens();
            if (tweens == null)
            {
                return false;
            }

            for (int i = 0; i < tweens.Count; i++)
            {
                var tween = tweens[i];
                if (tween == null || !tween.IsActive())
                {
                    continue;
                }

                if (tween.target is Transform tr)
                {
                    if (tr == root || tr.IsChildOf(root))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static async Task WaitUntilActorIdleAsync(CombatantState actor, CancellationToken token)
        {
            while (!IsActorIdle(actor))
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                await Task.Yield();
            }
        }
    }
}
