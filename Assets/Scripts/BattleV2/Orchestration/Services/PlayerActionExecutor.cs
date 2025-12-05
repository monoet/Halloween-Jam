using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Execution.TimedHits;
using BattleV2.Orchestration;
using BattleV2.Orchestration.Events;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public sealed class PlayerActionExecutor
    {
        private readonly IActionPipeline actionPipeline;
        private readonly ITimedHitResultResolver timedResultResolver;
        private readonly ITriggeredEffectsService triggeredEffects;
        private readonly IBattleEventBus eventBus;

        public PlayerActionExecutor(
            IActionPipeline actionPipeline,
            ITimedHitResultResolver timedResultResolver,
            ITriggeredEffectsService triggeredEffects,
            IBattleEventBus eventBus)
        {
            this.actionPipeline = actionPipeline;
            this.timedResultResolver = timedResultResolver;
            this.triggeredEffects = triggeredEffects;
            this.eventBus = eventBus;
        }

        public async Task ExecuteAsync(PlayerActionExecutionContext context)
        {
            if (actionPipeline == null || context.Player == null || context.Selection.Action == null || context.Implementation == null)
            {
                context.OnFallback?.Invoke();
                return;
            }

            try
            {
                var targets = context.Snapshot.Targets ?? Array.Empty<CombatantState>();
                var defeatCandidates = CollectDeathCandidates(targets);

                var request = new ActionRequest(
                    context.Manager,
                    context.Player,
                    targets,
                    context.Selection,
                    context.Implementation,
                    context.CombatContext);

                var result = await actionPipeline.Run(request);
                if (!result.Success)
                {
                    context.OnFallback?.Invoke();
                    return;
                }

                var resolvedTimedResult = timedResultResolver != null
                    ? timedResultResolver.Resolve(context.Selection, context.Implementation, result.TimedResult)
                    : result.TimedResult;

                int totalComboPointsAwarded = Mathf.Max(0, result.ComboPointsAwarded);
                if (context.Implementation is LunarChainAction && context.Player != null && context.CombatContext != null && context.CombatContext.Player == context.Player)
                {
                    ApplyLunarChainRefunds(context, ref totalComboPointsAwarded);
                }

                BattleV2.Charge.TimedHitResult? finalTimedResult = AdjustTimedResult(resolvedTimedResult, totalComboPointsAwarded);

                ScheduleTriggeredEffects(context, finalTimedResult);
                context.RefreshCombatContext?.Invoke();

                if (context.PlaybackTask != null)
                {
                    await AwaitPlayback(context.PlaybackTask);
                }

                PublishDefeatEvents(defeatCandidates, context.Player);

                int cpAfter = context.Player != null ? context.Player.CurrentCP : 0;
                var resolvedSelection = context.Selection.WithTimedResult(finalTimedResult);

                context.OnActionResolved?.Invoke(resolvedSelection, context.ComboPointsBefore, cpAfter);

                bool battleEnded = context.TryResolveBattleEnd != null && context.TryResolveBattleEnd();
                var completedTargets = context.Snapshot.Targets ?? Array.Empty<CombatantState>();
                eventBus?.Publish(new ActionCompletedEvent(context.Player, resolvedSelection, completedTargets));

                if (battleEnded)
                {
                    return;
                }

                context.SetState?.Invoke(BattleState.AwaitingAction);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleManagerV2] Player action threw exception: {ex}");
                context.OnFallback?.Invoke();
            }
        }

        private static void ApplyLunarChainRefunds(PlayerActionExecutionContext context, ref int totalComboPointsAwarded)
        {
            var selection = context.Selection;
            var player = context.Player;

            var tier = selection.TimedHitProfile != null
                ? selection.TimedHitProfile.GetTierForCharge(selection.CpCharge)
                : default;

            int refundCap = tier.RefundMax > 0 ? tier.RefundMax : int.MaxValue;

            if (totalComboPointsAwarded > refundCap)
            {
                int overflow = totalComboPointsAwarded - refundCap;
                if (overflow > 0 && player != null)
                {
                    player.SpendCP(overflow);
                    totalComboPointsAwarded -= overflow;
                }
            }

            int desiredTotal = totalComboPointsAwarded + 1;
            int finalRefund = Mathf.Min(desiredTotal, refundCap);
            int additional = finalRefund - totalComboPointsAwarded;
            if (additional > 0 && player != null)
            {
                player.AddCP(additional);
                totalComboPointsAwarded = finalRefund;
            }
        }

        private static BattleV2.Charge.TimedHitResult? AdjustTimedResult(BattleV2.Charge.TimedHitResult? resolvedTimedResult, int totalComboPointsAwarded)
        {
            if (resolvedTimedResult.HasValue)
            {
                var raw = resolvedTimedResult.Value;
                if (raw.CpRefund != totalComboPointsAwarded)
                {
                    return new BattleV2.Charge.TimedHitResult(
                        raw.HitsSucceeded,
                        raw.TotalHits,
                        totalComboPointsAwarded,
                        raw.DamageMultiplier,
                        raw.Cancelled,
                        raw.SuccessStreak,
                        raw.PhaseDamageApplied,
                        raw.TotalDamageApplied);
                }

                return resolvedTimedResult;
            }

            if (totalComboPointsAwarded > 0)
            {
                return new BattleV2.Charge.TimedHitResult(
                    hitsSucceeded: 0,
                    totalHits: 0,
                    cpRefund: totalComboPointsAwarded,
                    damageMultiplier: 1f,
                    cancelled: false,
                    successStreak: 0,
                    phaseDamageApplied: false,
                    totalDamageApplied: 0);
            }

            return resolvedTimedResult;
        }

        private static List<CombatantState> CollectDeathCandidates(IReadOnlyList<CombatantState> targets)
        {
            var result = new List<CombatantState>();
            if (targets == null)
            {
                return result;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                if (!result.Contains(target))
                {
                    result.Add(target);
                }
            }

            return result;
        }

        private void PublishDefeatEvents(List<CombatantState> candidates, CombatantState killer)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                var combatant = candidates[i];
                if (combatant != null && combatant.IsDead())
                {
                    eventBus?.Publish(new CombatantDefeatedEvent(combatant, killer));
                }
            }
        }

        private void ScheduleTriggeredEffects(PlayerActionExecutionContext context, BattleV2.Charge.TimedHitResult? timedResult)
        {
            if (triggeredEffects == null || context.Player == null)
            {
                return;
            }

            var targets = context.Snapshot.Targets ?? Array.Empty<CombatantState>();
            if (targets.Count == 0)
            {
                return;
            }

            try
            {
                triggeredEffects.Schedule(context.Player, context.Selection, timedResult, context.Snapshot, context.CombatContext);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleManagerV2] Failed to schedule triggered effects: {ex}");
            }
        }

        private static async Task AwaitPlayback(Task playbackTask)
        {
            try
            {
                await playbackTask;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleManagerV2] Animation playback failed: {ex}");
            }
        }
    }

    public readonly struct PlayerActionExecutionContext
    {
        public BattleManagerV2 Manager { get; init; }
        public CombatantState Player { get; init; }
        public BattleSelection Selection { get; init; }
        public IAction Implementation { get; init; }
        public CombatContext CombatContext { get; init; }
        public ExecutionSnapshot Snapshot { get; init; }
        public Task PlaybackTask { get; init; }
        public int ComboPointsBefore { get; init; }
        public Func<bool> TryResolveBattleEnd { get; init; }
        public Action RefreshCombatContext { get; init; }
        public Action<BattleSelection, int, int> OnActionResolved { get; init; }
        public Action OnFallback { get; init; }
        public Action<BattleState> SetState { get; init; }
    }
}
