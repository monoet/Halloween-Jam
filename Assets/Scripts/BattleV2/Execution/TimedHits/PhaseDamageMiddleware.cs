using System;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Anim;
using UnityEngine;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Applies damage, VFX/SFX feedback and logging for each resolved phase of a timed-hit sequence.
    /// Relies on the action implementation to provide a phase damage plan exposing base damage and clamping info.
    /// </summary>
    public sealed class PhaseDamageMiddleware : IActionMiddleware
    {
        private readonly ITimedHitRunner runner;

        public PhaseDamageMiddleware(ITimedHitRunner runner)
        {
            this.runner = runner;
        }

        public async Task InvokeAsync(ActionContext context, Func<Task> next)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (runner == null || context.ActionImplementation is not ITimedHitPhaseDamageAction phaseAction)
            {
                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
                return;
            }

            if (!phaseAction.TryBuildPhaseDamagePlan(context.Attacker, context.CombatContext, context.CpCharge, out var plan))
            {
                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
                return;
            }

            int totalDamage = 0;
            bool appliedAny = false;

            void EmitFeedback(TimedHitPhaseResult phase, int phaseDamage, float combinedMultiplier)
            {
                var target = context.Target;
                Vector3? worldPos = target != null ? target.transform.position : (Vector3?)null;
                int totalPhases = plan.TotalPhases > 0 ? plan.TotalPhases : Mathf.Max(phase.Index, 0);
                var feedback = new TimedHitPhaseFeedback(
                    target,
                    phase.Index,
                    totalPhases,
                    phase.IsSuccess,
                    phaseDamage,
                    combinedMultiplier,
                    phase.AccuracyNormalized,
                    worldPos);

                BattleEvents.EmitTimedHitPhaseFeedback(feedback);
            }

            void HandlePhaseResolved(TimedHitPhaseResult phase)
            {
                if (context.Cancelled || context.Target == null)
                {
                    return;
                }

                float contribution = Mathf.Max(0f, phase.DamageMultiplier);
                float tierMultiplier = plan.TierDamageMultiplier > 0f ? plan.TierDamageMultiplier : 1f;
                float combinedMultiplier = contribution * tierMultiplier;

                if ((!phase.IsSuccess && !plan.AllowPartialOnMiss) || combinedMultiplier <= 0f)
                {
                    EmitFeedback(phase, 0, combinedMultiplier);
                    return;
                }

                int damageValue = Mathf.RoundToInt(plan.BaseDamagePerHit * combinedMultiplier);
                damageValue = Mathf.Max(plan.MinimumDamage, damageValue);

                if (damageValue <= 0)
                {
                    EmitFeedback(phase, 0, combinedMultiplier);
                    return;
                }

                context.Target.TakeDamage(damageValue);
                totalDamage += damageValue;
                appliedAny = true;

                EmitFeedback(phase, damageValue, combinedMultiplier);
            }

            runner.OnPhaseResolved += HandlePhaseResolved;
            try
            {
                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
            }
            finally
            {
                runner.OnPhaseResolved -= HandlePhaseResolved;
            }

            if (appliedAny)
            {
                context.PhaseDamageApplied = true;
                context.TotalDamageApplied += totalDamage;

                if (context.TimedResult.HasValue)
                {
                    context.TimedResult = context.TimedResult.Value.WithPhaseDamage(true, context.TotalDamageApplied);
                }
            }
        }
    }
}
