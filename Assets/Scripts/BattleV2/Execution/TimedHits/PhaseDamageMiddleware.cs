using System;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Anim;
using BattleV2.Charge;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Applies damage, VFX/SFX feedback and logging for each resolved phase of a timed-hit sequence.
    /// Relies on the action implementation to provide a phase damage plan exposing base damage and clamping info.
    /// </summary>
    public sealed class PhaseDamageMiddleware : IActionMiddleware
    {
        public async Task InvokeAsync(ActionContext context, Func<Task> next)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.ActionImplementation is not ITimedHitPhaseDamageAction phaseAction)
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

                TryAwardComboPoint(phase);

                context.Target.TakeDamage(damageValue);
                totalDamage += damageValue;
                appliedAny = true;

                EmitFeedback(phase, damageValue, combinedMultiplier);
            }

            var previousListener = context.PhaseResultListener;
            context.PhaseResultListener = phase =>
            {
                HandlePhaseResolved(phase);
            };
            try
            {
                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
            }
            finally
            {
                context.PhaseResultListener = previousListener;
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

            if (context.TimedResult.HasValue && context.TimedResult.Value.CpRefund != context.ComboPointsAwarded)
            {
                var raw = context.TimedResult.Value;
                context.TimedResult = new TimedHitResult(
                    raw.HitsSucceeded,
                    raw.TotalHits,
                    context.ComboPointsAwarded,
                    raw.DamageMultiplier,
                    raw.Cancelled,
                    raw.SuccessStreak,
                    raw.PhaseDamageApplied,
                    raw.TotalDamageApplied);
            }

            void TryAwardComboPoint(TimedHitPhaseResult phase)
            {
                if (!phase.IsSuccess)
                {
                    return;
                }

                var attacker = context.Attacker;
                if (attacker == null || !attacker.IsPlayer)
                {
                    BattleDiagnostics.Log(
                        "AddCp.debugging",
                        $"skip_timed_cp actor={(attacker != null ? attacker.DisplayName : "(null)")}#{(attacker != null ? attacker.GetInstanceID() : 0)} reason={(attacker == null ? "attacker_null" : "not_player")}",
                        attacker);
                    return;
                }

                var profile = context.Selection.TimedHitProfile;
                int refundCap = profile != null
                    ? profile.GetTierForCharge(context.CpCharge).RefundMax
                    : int.MaxValue;

                if (refundCap <= 0)
                {
                    refundCap = int.MaxValue;
                }

                if (refundCap > 0 && context.ComboPointsAwarded >= refundCap)
                {
                    BattleDiagnostics.Log(
                        "AddCp.debugging",
                        $"skip_timed_cp actor={attacker.DisplayName}#{attacker.GetInstanceID()} reason=cap_reached cap={refundCap} awarded={context.ComboPointsAwarded}",
                        attacker);
                    return;
                }

                attacker.AddCP(1);
                context.ComboPointsAwarded += 1;
            }
        }
    }
}
