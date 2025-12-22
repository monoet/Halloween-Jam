using System;
using System.Collections.Generic;
using System.Threading;
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
        private readonly BattleV2.Marks.MarkInteractionProcessor markProcessor;

        public PlayerActionExecutor(
            IActionPipeline actionPipeline,
            ITimedHitResultResolver timedResultResolver,
            ITriggeredEffectsService triggeredEffects,
            IBattleEventBus eventBus,
            BattleV2.Marks.MarkInteractionProcessor markProcessor)
        {
            this.actionPipeline = actionPipeline;
            this.timedResultResolver = timedResultResolver;
            this.triggeredEffects = triggeredEffects;
            this.eventBus = eventBus;
            this.markProcessor = markProcessor;
        }

        public async Task ExecuteAsync(PlayerActionExecutionContext context)
        {
            if (actionPipeline == null || context.Player == null || context.Selection.Action == null || context.Implementation == null)
            {
                context.OnFallback?.Invoke();
                return;
            }

            int spSpent = 0;
            int cpSpent = 0;
            bool pipelineEffectsApplied = false;

            try
            {
                int spCost = Mathf.Max(0, context.BaseSpCost);
                int cpBase = Mathf.Max(0, context.BaseCpCost);
                int cpCharge = Mathf.Max(0, context.Selection.CpCharge);
                int cpCost = cpBase + cpCharge;
                int tid = Thread.CurrentThread.ManagedThreadId;

                BattleDiagnostics.Log(
                    "PAE.BUITI",
                    $"b=1 phase=PAE.Enter actor={context.Player?.DisplayName ?? "(null)"}#{context.Player?.GetInstanceID() ?? 0} actionId={context.Selection.Action?.id ?? "(null)"} " +
                    $"spBase={spCost} cpBase={cpBase} cpCharge={cpCharge} cpTotal={cpCost} cpBefore={context.Player.CurrentCP} spBefore={context.Player.CurrentSP} targets={(context.Snapshot.Targets != null ? context.Snapshot.Targets.Count : 0)}",
                    context.Player);
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][PAE.Enter] tid={tid} mainTid={UnityMainThreadGuard.MainThreadId} isMain={UnityMainThreadGuard.IsMainThread()} actor={context.Player?.DisplayName ?? "(null)"}#{context.Player?.GetInstanceID() ?? 0} actionId={context.Selection.Action?.id ?? "(null)"}",
                    context.Player);

                var targets = context.Snapshot.Targets ?? Array.Empty<CombatantState>();
                var defeatCandidates = CollectDeathCandidates(targets);

                var chargeResult = ChargeSelectionCosts(context, spCost, cpBase, cpCharge, cpCost);
                spSpent = chargeResult.SpSpent;
                cpSpent = chargeResult.CpSpent;
                if (!chargeResult.Success)
                {
                    return;
                }

                var preCost = chargeResult.Pre;
                var postCost = chargeResult.PostCost;

                var judgment = context.Judgment.HasValue
                    ? context.Judgment
                    : BattleV2.Execution.ActionJudgment.FromSelection(
                        context.Selection,
                        context.Player,
                        cpCost,
                        System.HashCode.Combine(context.Player != null ? context.Player.GetInstanceID() : 0, context.Selection.Action != null ? context.Selection.Action.id.GetHashCode() : 0, cpCharge),
                        preCost,
                        postCost);
                judgment = judgment.WithPostCost(postCost);

                var request = new ActionRequest(
                    context.Manager,
                    context.Player,
                    targets,
                    context.Selection,
                    context.Implementation,
                    context.CombatContext,
                    judgment);

                BattleDiagnostics.Log(
                    "PAE.BUITI",
                    $"b=1 phase=PAE.Pipeline.Start actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} targets={(targets != null ? targets.Count : 0)}",
                    context.Player);

                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][PAE.AwaitPipeline.Before] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                    context.Player);

                ActionResult result;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using (BattleV2.Charge.ComboPointScaling.BeginTrace(context.ExecutionId, context.Selection.Action?.id ?? "(null)"))
#endif
                {
                    try
                    {
                        result = await actionPipeline.Run(request);
                        pipelineEffectsApplied = result.EffectsApplied;
                    }
                    catch (Exception)
                    {
                        // Assume effects may have applied before the throw to avoid phantom refunds.
                        pipelineEffectsApplied = true;
                        throw;
                    }
                }

                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][PAE.AwaitPipeline.After] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} effectsApplied={result.EffectsApplied}",
                    context.Player);
                BattleDiagnostics.Log(
                    "AddCp.Debugging01",
                    $"phase=PipelineResult actor={context.Player?.DisplayName ?? "(null)"}#{context.Player?.GetInstanceID() ?? 0} actionId={context.Selection.Action?.id ?? "(null)"} success={result.Success} timedResult={(result.TimedResult.HasValue ? "yes" : "no")}",
                    context.Player);
                BattleDiagnostics.Log(
                    "PAE.BUITI",
                    $"b=1 phase=PAE.Pipeline.End actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} success={result.Success}",
                    context.Player);
                if (!result.Success)
                {
                    if (!result.EffectsApplied && cpSpent > 0)
                    {
                        BattleDiagnostics.Log(
                            "PAE.BUITI",
                            $"b=1 phase=PAE.Fail.RefundCP actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} cpRefund={cpSpent} cpBefore={context.Player.CurrentCP}",
                            context.Player);
                        context.Player.AddCP(cpSpent);
                        cpSpent = 0;
                    }

                    if (!result.EffectsApplied && spSpent > 0)
                    {
                        BattleDiagnostics.Log(
                            "PAE.BUITI",
                            $"b=1 phase=PAE.Fail.RefundSP actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} spRefund={spSpent} spBefore={context.Player.CurrentSP}",
                            context.Player);
                        context.Player.RestoreSP(spSpent);
                        spSpent = 0;
                    }

                    if (result.EffectsApplied)
                    {
                        BattleDiagnostics.Log(
                            "Thread.debug00",
                            $"[Thread.debug00][PAE.Fail.NoRefund] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} reason=EffectsApplied",
                            context.Player);
                    }
                    context.OnFallback?.Invoke();
                    return;
                }

                var resolvedTimedResult = timedResultResolver != null
                    ? timedResultResolver.Resolve(context.Selection, context.Implementation, result.TimedResult)
                    : result.TimedResult;

                var afterAction = ResourceSnapshot.FromCombatant(context.Player);
                BattleDiagnostics.Log(
                    "ActionCharge",
                    $"actor={(context.Player != null ? context.Player.DisplayName : "(null)")}#{(context.Player != null ? context.Player.GetInstanceID() : 0)} actionId={context.Selection.Action?.id ?? "(null)"} cpPre={preCost.CpCurrent} cpPost={afterAction.CpCurrent} spPre={preCost.SpCurrent} spPost={afterAction.SpCurrent} cpCharge={context.Selection.CpCharge}",
                    context.Player);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevCpTrace)
                {
                    BattleDiagnostics.Log(
                        "CPTRACE",
                        $"exec={context.ExecutionId} phase=ActionCharge action={context.Selection.Action?.id ?? "(null)"} cpBase={cpBase} cpCharge={cpCharge} cpTotal={cpCost} cpPre={preCost.CpCurrent} cpPost={afterAction.CpCurrent} spPre={preCost.SpCurrent} spPost={afterAction.SpCurrent}",
                        context.Player);
                }
#endif
                if (cpCost > 0 && cpSpent <= 0)
                {
                    Debug.LogWarning($"[CP/SP] Expected CP charge but none occurred: action={context.Selection.Action?.id ?? "null"} actor={context.Player?.name ?? "(null)"}");
                }
                var judgmentWithCosts = judgment.WithPostCost(postCost);

                var timedGrade = ActionJudgment.ResolveTimedGrade(resolvedTimedResult);
                var finalJudgment = context.Judgment.HasValue
                    ? context.Judgment.WithTimedGrade(timedGrade)
                    : judgmentWithCosts.WithTimedGrade(timedGrade);

                int totalComboPointsAwarded = Mathf.Max(0, result.ComboPointsAwarded);
                if (context.Implementation is LunarChainAction && context.Player != null && context.Player.IsPlayer)
                {
                    ApplyLunarChainRefunds(context, ref totalComboPointsAwarded);
                }

                BattleV2.Charge.TimedHitResult? finalTimedResult = AdjustTimedResult(resolvedTimedResult, totalComboPointsAwarded);
                var resolvedSelection = context.Selection.WithTimedResult(finalTimedResult);

                ScheduleTriggeredEffects(context, finalTimedResult);
                if (context.PlaybackTask != null)
                {
                    await AwaitPlayback(context.PlaybackTask);
                }
                markProcessor?.Process(context.Player, resolvedSelection, finalJudgment, targets, context.ExecutionId, context.PlayerTurnCounter);
                context.RefreshCombatContext?.Invoke();

                PublishDefeatEvents(defeatCandidates, context.Player);

                int cpAfter = context.Player != null ? context.Player.CurrentCP : 0;
                context.OnActionResolved?.Invoke(resolvedSelection, context.ComboPointsBefore, cpAfter);

                bool battleEnded = context.TryResolveBattleEnd != null && context.TryResolveBattleEnd();
                var completedTargets = context.Snapshot.Targets ?? Array.Empty<CombatantState>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevCpTrace)
                {
                    BattleDiagnostics.Log(
                        "CPTRACE",
                        $"TURN_CLOSE_PUBLISH exec={context.ExecutionId} actor={context.Player?.DisplayName ?? "(null)"}#{(context.Player != null ? context.Player.GetInstanceID() : 0)} action={resolvedSelection.Action?.id ?? "(null)"} cp={resolvedSelection.CpCharge} isTriggered=false",
                        context.Player);
                }
#endif
                eventBus?.Publish(new ActionCompletedEvent(context.ExecutionId, context.Player, resolvedSelection, completedTargets, isTriggered: false, judgment: finalJudgment));

                if (battleEnded)
                {
                    return;
                }

                context.SetState?.Invoke(BattleState.AwaitingAction);
            }
            catch (Exception ex)
            {
                var actorName = context.Player?.DisplayName ?? "(null)";
                int actorId = context.Player != null ? context.Player.GetInstanceID() : 0;
                var actionId = context.Selection.Action?.id ?? "(null)";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevCpTrace)
                {
                    BattleDiagnostics.Log(
                        "CPTRACE",
                        $"EXCEPTION exec={context.ExecutionId} where=PlayerActionExecutor actionId={actionId} exType={(ex != null ? ex.GetType().Name : "(null)")} exMsg={(ex != null ? ex.Message : "(null)")}",
                        context.Player);
                }
#endif
                BattleDiagnostics.Log(
                    "PAE.BUITI",
                    $"b=1 phase=PAE.Exception actor={actorName}#{actorId} actionId={actionId} cpSpent={cpSpent} spSpent={spSpent}",
                    context.Player);
                BattleDiagnostics.Log(
                    "PAE.BUITI",
                    $"b=1 phase=PAE.Exception.Detail actor={actorName}#{actorId} actionId={actionId} exType={(ex != null ? ex.GetType().Name : "(null)")} exMsg={(ex != null ? ex.Message : "(null)")}",
                    context.Player);
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][PAE.Exception] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} effectsApplied={pipelineEffectsApplied} exType={(ex != null ? ex.GetType().Name : "(null)")}",
                    context.Player);
                // If execution failed after spending CP, refund to avoid silent loss.
                if (context.Player != null && !pipelineEffectsApplied)
                {
                    int refundCp = Mathf.Max(0, cpSpent);
                    if (refundCp > 0)
                    {
                        BattleDiagnostics.Log(
                            "PAE.BUITI",
                            $"b=1 phase=PAE.Exception.RefundCP actor={actorName}#{actorId} actionId={actionId} cpRefund={refundCp} cpBefore={context.Player.CurrentCP}",
                            context.Player);
                        context.Player.AddCP(refundCp);
                    }

                    int refundSp = Mathf.Max(0, spSpent);
                    if (refundSp > 0)
                    {
                        BattleDiagnostics.Log(
                            "PAE.BUITI",
                            $"b=1 phase=PAE.Exception.RefundSP actor={actorName}#{actorId} actionId={actionId} spRefund={refundSp} spBefore={context.Player.CurrentSP}",
                            context.Player);
                        context.Player.RestoreSP(refundSp);
                    }
                }
                Debug.LogError($"[BattleManagerV2] Player action threw exception: {ex}");
                context.OnFallback?.Invoke();
            }
        }

        private ChargeResult ChargeSelectionCosts(PlayerActionExecutionContext context, int spCost, int cpBase, int cpCharge, int cpCost)
        {
            var preCost = context.Judgment.HasValue ? context.Judgment.ResourcesPreCost : ResourceSnapshot.FromCombatant(context.Player);
            var postCost = context.Judgment.HasValue ? context.Judgment.ResourcesPostCost : preCost;
            int spSpentLocal = 0;
            int cpSpentLocal = 0;

            if (spCost > 0)
            {
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][SpendSP.Before] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} spCost={spCost} spBefore={context.Player.CurrentSP}",
                    context.Player);

                BattleDiagnostics.Log(
                    "PAE.BUITI",
                    $"b=1 phase=PAE.SP.Attempt actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} spCost={spCost} spBefore={context.Player.CurrentSP}",
                    context.Player);

                if (!context.Player.SpendSP(spCost))
                {
                    BattleDiagnostics.Log(
                        "PAE.BUITI",
                        $"b=1 phase=PAE.SP.Fail actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} spCost={spCost} spBefore={context.Player.CurrentSP}",
                        context.Player);
                    context.OnFallback?.Invoke();
                    return ChargeResult.Failure(preCost, postCost, spSpentLocal, cpSpentLocal);
                }

                spSpentLocal = spCost;

                BattleDiagnostics.Log(
                    "PAE.BUITI",
                    $"b=1 phase=PAE.SP.Charged actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} spCost={spCost} spAfter={context.Player.CurrentSP}",
                    context.Player);
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][SpendSP.After] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} spAfter={context.Player.CurrentSP}",
                    context.Player);

                postCost = ResourceSnapshot.FromCombatant(context.Player);
            }

            if (cpCost > 0)
            {
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][SpendCP.Before] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} cpBase={cpBase} cpCharge={cpCharge} cpTotal={cpCost} cpBefore={context.Player.CurrentCP}",
                    context.Player);

                BattleDiagnostics.Log(
                    "PAE.BUITI",
                    $"b=1 phase=PAE.CP.Attempt actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} cpBase={cpBase} cpCharge={cpCharge} cpTotal={cpCost} cpBefore={context.Player.CurrentCP}",
                    context.Player);

                if (!context.Player.SpendCP(cpCost))
                {
                    BattleDiagnostics.Log(
                        "PAE.BUITI",
                        $"b=1 phase=PAE.CP.Fail actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} cpTotal={cpCost} cpBefore={context.Player.CurrentCP}",
                        context.Player);

                    if (spSpentLocal > 0)
                    {
                        BattleDiagnostics.Log(
                            "PAE.BUITI",
                            $"b=1 phase=PAE.CP.Fail.RollbackSP actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} spRefund={spSpentLocal} spBefore={context.Player.CurrentSP}",
                            context.Player);
                        context.Player.RestoreSP(spSpentLocal);
                        spSpentLocal = 0;
                    }
                    context.OnFallback?.Invoke();
                    return ChargeResult.Failure(preCost, postCost, spSpentLocal, cpSpentLocal);
                }

                cpSpentLocal = cpCost;
                BattleDiagnostics.Log(
                    "PAE.BUITI",
                    $"b=1 phase=PAE.CP.Charged actor={context.Player.DisplayName}#{context.Player.GetInstanceID()} actionId={context.Selection.Action?.id ?? "(null)"} cpTotal={cpCost} cpAfter={context.Player.CurrentCP}",
                    context.Player);
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][SpendCP.After] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} cpAfter={context.Player.CurrentCP}",
                    context.Player);

                postCost = ResourceSnapshot.FromCombatant(context.Player);
            }

            return ChargeResult.Successful(preCost, postCost, spSpentLocal, cpSpentLocal);
        }

        private readonly struct ChargeResult
        {
            public bool Success { get; }
            public ResourceSnapshot Pre { get; }
            public ResourceSnapshot PostCost { get; }
            public int SpSpent { get; }
            public int CpSpent { get; }

            private ChargeResult(bool success, ResourceSnapshot pre, ResourceSnapshot postCost, int spSpent, int cpSpent)
            {
                Success = success;
                Pre = pre;
                PostCost = postCost;
                SpSpent = spSpent;
                CpSpent = cpSpent;
            }

            public static ChargeResult Successful(ResourceSnapshot pre, ResourceSnapshot postCost, int spSpent, int cpSpent) =>
                new ChargeResult(true, pre, postCost, spSpent, cpSpent);

            public static ChargeResult Failure(ResourceSnapshot pre, ResourceSnapshot postCost, int spSpent, int cpSpent) =>
                new ChargeResult(false, pre, postCost, spSpent, cpSpent);
        }

        private static void ApplyLunarChainRefunds(PlayerActionExecutionContext context, ref int totalComboPointsAwarded)
        {
            var selection = context.Selection;
            var player = context.Player;

            if (player != null && !player.IsPlayer)
            {
                BattleDiagnostics.Log(
                    "AddCp.Debugging01",
                    $"phase=ApplyLunarChainRefunds actor={player.DisplayName}#{player.GetInstanceID()} reason=not_player",
                    player);
                return;
            }

            BattleDiagnostics.Log(
                "AddCp.Debugging01",
                $"phase=ApplyLunarChainRefunds actor={player?.DisplayName ?? "(null)"}#{player?.GetInstanceID() ?? 0} cpCharge={selection.CpCharge} awarded={totalComboPointsAwarded}",
                player);

            var tier = selection.TimedHitProfile != null
                ? selection.TimedHitProfile.GetTierForCharge(selection.CpCharge)
                : default;

            int refundCap = tier.RefundMax > 0 ? tier.RefundMax : int.MaxValue;

            if (totalComboPointsAwarded > refundCap)
            {
                int overflow = totalComboPointsAwarded - refundCap;
                if (overflow > 0 && player != null)
                {
                    if (player.SpendCP(overflow))
                    {
                        totalComboPointsAwarded -= overflow;
                    }
                    else
                    {
                        BattleDiagnostics.Log(
                            "AddCp.Debugging01",
                            $"phase=ApplyLunarChainRefunds overflow_spend_failed actor={player.DisplayName}#{player.GetInstanceID()} overflow={overflow}",
                            player);
                        totalComboPointsAwarded = refundCap;
                    }
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
                triggeredEffects.Schedule(context.ExecutionId, context.Player, context.Selection, timedResult, context.Snapshot, context.CombatContext);
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
        public int ExecutionId { get; init; }
        public BattleManagerV2 Manager { get; init; }
        public CombatantState Player { get; init; }
        public BattleSelection Selection { get; init; }
        public IAction Implementation { get; init; }
        public CombatContext CombatContext { get; init; }
        public ExecutionSnapshot Snapshot { get; init; }
        public Task PlaybackTask { get; init; }
        public int ComboPointsBefore { get; init; }
        public BattleV2.Execution.ActionJudgment Judgment { get; init; }
        public Func<bool> TryResolveBattleEnd { get; init; }
        public Action RefreshCombatContext { get; init; }
        public Action<BattleSelection, int, int> OnActionResolved { get; init; }
        public Action OnFallback { get; init; }
        public Action<BattleState> SetState { get; init; }
        public int BaseSpCost { get; init; }
        public int BaseCpCost { get; init; }
        public int PlayerTurnCounter { get; init; }
    }
}
