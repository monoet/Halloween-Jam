using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Execution.TimedHits;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public readonly struct ActionRequest
    {
        public ActionRequest(
            int executionId,
            BattleManagerV2 manager,
            CombatantState actor,
            IReadOnlyList<CombatantState> targets,
            BattleSelection selection,
            IAction implementation,
            CombatContext combatContext,
            Execution.ActionJudgment judgment = default)
        {
            ExecutionId = executionId;
            Manager = manager;
            Actor = actor;
            Targets = targets;
            Selection = selection;
            Implementation = implementation;
            CombatContext = combatContext;
            Judgment = judgment;
        }

        public int ExecutionId { get; }
        public BattleManagerV2 Manager { get; }
        public CombatantState Actor { get; }
        public IReadOnlyList<CombatantState> Targets { get; }
        public BattleSelection Selection { get; }
        public IAction Implementation { get; }
        public CombatContext CombatContext { get; }
        public Execution.ActionJudgment Judgment { get; }

        public CombatantState PrimaryTarget =>
            Targets != null && Targets.Count > 0 ? Targets[0] : CombatContext?.Enemy;
    }

    public readonly struct ActionResult
    {
        private ActionResult(bool success, TimedHitResult? timedResult, int comboPointsAwarded, bool effectsApplied)
        {
            Success = success;
            TimedResult = timedResult;
            ComboPointsAwarded = comboPointsAwarded;
            EffectsApplied = effectsApplied;
        }

        public bool Success { get; }
        public TimedHitResult? TimedResult { get; }
        public int ComboPointsAwarded { get; }
        public bool EffectsApplied { get; }

        public static ActionResult From(TimedHitResult? timedResult, int comboPointsAwarded, bool effectsApplied = false) =>
            new(true, timedResult, comboPointsAwarded, effectsApplied);

        public static ActionResult Failure(bool effectsApplied = false) => new(false, null, 0, effectsApplied);
    }

    public interface IActionPipeline
    {
        Task<ActionResult> Run(ActionRequest request);
    }

    /// <summary>
    /// Columna vertebral para acciones de combate. Actualmente delega en el pipeline legacy mientras migramos.
    /// </summary>
    public sealed class OrchestrationActionPipeline : IActionPipeline
    {
        private readonly IBattleEventBus eventBus;

        public OrchestrationActionPipeline(IBattleEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public async Task<ActionResult> Run(ActionRequest request)
        {
            if (request.Manager == null || request.Actor == null || request.Selection.Action == null || request.Implementation == null)
            {
                return ActionResult.Failure();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevFlowTrace)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"PIPELINE_ENTER exec={request.ExecutionId} actor={request.Actor?.DisplayName ?? "(null)"}#{request.Actor?.GetInstanceID() ?? 0} action={request.Selection.Action?.id ?? "(null)"} shape={request.Selection.Action?.targetShape} targets={FormatTargets(request.Targets)}",
                    request.Actor);
            }
#endif

            if (request.Implementation is BattleV2.Actions.IActionMultiTarget multiTarget)
            {
                return await RunMultiTargetAsync(request, multiTarget);
            }

            return await RunLegacyPipelineAsync(request);
        }

        private static async Task<ActionResult> RunMultiTargetAsync(ActionRequest request, BattleV2.Actions.IActionMultiTarget multiTarget)
        {
            await UnityThread.SwitchToMainThread();
            UnityThread.AssertMainThread("RunMultiTargetAsync.Enter");
            BattleDiagnostics.Log(
                "Thread.debug00",
                $"[Thread.debug00][PipeLegacy.Multi.Enter] tid={System.Threading.Thread.CurrentThread.ManagedThreadId} mainTid={UnityMainThreadGuard.MainThreadId} isMain={UnityMainThreadGuard.IsMainThread()} actor={request.Actor?.DisplayName ?? "(null)"}#{request.Actor?.GetInstanceID() ?? 0} actionId={request.Selection.Action?.id ?? "(null)"}",
                request.Actor);

            var targets = request.Targets ?? Array.Empty<CombatantState>();
            if (targets.Count == 0 && request.PrimaryTarget != null)
            {
                targets = new[] { request.PrimaryTarget };
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevFlowTrace)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"PIPE_MULTI_TARGET exec={request.ExecutionId} actor={request.Actor?.DisplayName ?? "(null)"}#{request.Actor?.GetInstanceID() ?? 0} action={request.Selection.Action?.id ?? "(null)"} shape={request.Selection.Action?.targetShape} recipients={FormatTargets(targets)}",
                    request.Actor);
            }

            if (request.Selection.Action != null &&
                request.Selection.Action.targetShape == BattleV2.Targeting.TargetShape.Single &&
                targets.Count > 1)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"WARN_TARGET_MISMATCH exec={request.ExecutionId} actor={request.Actor?.DisplayName ?? "(null)"}#{request.Actor?.GetInstanceID() ?? 0} action={request.Selection.Action?.id ?? "(null)"} expected=Single actualCount={targets.Count} recipients={FormatTargets(targets)}",
                    request.Actor);

                targets = new[] { targets[0] };

                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"TARGET_MISMATCH_CLAMP exec={request.ExecutionId} actor={request.Actor?.DisplayName ?? "(null)"}#{request.Actor?.GetInstanceID() ?? 0} action={request.Selection.Action?.id ?? "(null)"} clampedRecipients={FormatTargets(targets)}",
                    request.Actor);
            }
#endif

            try
            {
                multiTarget.ExecuteMulti(request.Actor, request.CombatContext, targets, request.Selection, () => { });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ActionPipeline] Multi-target action threw exception: {ex}");
                return ActionResult.Failure(true);
            }

            return ActionResult.From(null, 0, true);
        }

        private async Task<ActionResult> RunLegacyPipelineAsync(ActionRequest request)
        {
            await UnityThread.SwitchToMainThread();
            UnityThread.AssertMainThread("RunLegacyPipelineAsync.Enter");

            BattleDiagnostics.Log(
                "Thread.debug00",
                $"[Thread.debug00][PipeLegacy.Enter] tid={System.Threading.Thread.CurrentThread.ManagedThreadId} mainTid={UnityMainThreadGuard.MainThreadId} isMain={UnityMainThreadGuard.IsMainThread()} actor={request.Actor?.DisplayName ?? "(null)"}#{request.Actor?.GetInstanceID() ?? 0} actionId={request.Selection.Action?.id ?? "(null)"}",
                request.Actor);

            var pipelineFactory = new DefaultActionPipelineFactory(request.Manager);
            var pipeline = pipelineFactory.CreatePipeline(request.Selection.Action, request.Implementation);

            CombatantState effectiveTarget = request.PrimaryTarget ?? request.CombatContext?.Enemy;
            var combatContext = FreezeContext(request.CombatContext, effectiveTarget);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevFlowTrace)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"PIPE_LEGACY exec={request.ExecutionId} actor={request.Actor?.DisplayName ?? "(null)"}#{request.Actor?.GetInstanceID() ?? 0} action={request.Selection.Action?.id ?? "(null)"} target={(effectiveTarget != null ? effectiveTarget.DisplayName + "#" + effectiveTarget.GetInstanceID() : "(null)")}",
                    request.Actor);
            }
#endif

            var actionContext = new Execution.ActionContext(
                request.Manager,
                request.Actor,
                effectiveTarget,
                request.Selection.Action,
                request.Implementation,
                combatContext,
                request.Selection,
                request.Judgment);

            await UnityThread.SwitchToMainThread();
            UnityThread.AssertMainThread("RunLegacyPipelineAsync.BeforeExecute");

            BattleDiagnostics.Log(
                "Thread.debug00",
                $"[Thread.debug00][PipeLegacy.AwaitExecute.Before] tid={System.Threading.Thread.CurrentThread.ManagedThreadId} mainTid={UnityMainThreadGuard.MainThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                request.Actor);

            try
            {
                await pipeline.ExecuteAsync(actionContext);
            }
            catch (Exception ex)
            {
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][PipeLegacy.Exception] tid={System.Threading.Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} mainTid={UnityMainThreadGuard.MainThreadId} ex={ex.GetType().Name} msg={ex.Message}",
                    request.Actor);
                await UnityThread.SwitchToMainThread();
                UnityThread.AssertMainThread("RunLegacyPipelineAsync.ExceptionExit");
                return ActionResult.Failure(actionContext.EffectsApplied);
            }

            await UnityThread.SwitchToMainThread();
            UnityThread.AssertMainThread("RunLegacyPipelineAsync.Exit");

            BattleDiagnostics.Log(
                "Thread.debug00",
                $"[Thread.debug00][PipeLegacy.AwaitExecute.After] tid={System.Threading.Thread.CurrentThread.ManagedThreadId} mainTid={UnityMainThreadGuard.MainThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                request.Actor);

            BattleDiagnostics.Log(
                "Thread.debug00",
                $"[Thread.debug00][PipeLegacy.Exit] tid={System.Threading.Thread.CurrentThread.ManagedThreadId} mainTid={UnityMainThreadGuard.MainThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                request.Actor);

            return ActionResult.From(actionContext.TimedResult, actionContext.ComboPointsAwarded, actionContext.EffectsApplied);
        }

        private static string FormatTargets(IReadOnlyList<CombatantState> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return "[]";
            }

            const int max = 6;
            var count = targets.Count;
            var result = new System.Text.StringBuilder(capacity: 64);
            result.Append('[');
            int limit = count < max ? count : max;
            for (int i = 0; i < limit; i++)
            {
                var t = targets[i];
                if (i > 0) result.Append(',');
                if (t == null)
                {
                    result.Append("(null)");
                    continue;
                }
                result.Append(t.DisplayName);
                result.Append('#');
                result.Append(t.GetInstanceID());
            }
            if (count > max)
            {
                result.Append(",..+");
                result.Append(count - max);
            }
            result.Append(']');
            return result.ToString();
        }

        private static CombatContext FreezeContext(CombatContext context, CombatantState target)
        {
            if (context == null)
            {
                return null;
            }

            var enemy = target ?? context.Enemy;
            var enemyRuntime = ResolveRuntime(enemy) ?? context.EnemyRuntime;

            return new CombatContext(
                context.Player,
                enemy,
                context.PlayerRuntime,
                enemyRuntime,
                context.Services,
                context.Catalog);
        }

        private static CharacterRuntime ResolveRuntime(CombatantState combatant)
        {
            if (combatant == null)
            {
                return null;
            }

            if (combatant.CharacterRuntime != null)
            {
                return combatant.CharacterRuntime;
            }

            return combatant.GetComponent<CharacterRuntime>();
        }
    }
}
