using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Runtime.Core;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Runtime.SystemSteps
{
    internal sealed class SystemStepRunner
    {
        private const string SystemStepWindowOpen = "window.open";
        private const string SystemStepWindowClose = "window.close";
        private const string SystemStepGate = "gate.on";
        private const string SystemStepDamage = "damage.apply";
        private const string SystemStepFallback = "fallback";
        private const string SystemStepPhaseLock = "phase.lock";
        private const string SystemStepPhaseUnlock = "phase.unlock";

        private static readonly TimedHitJudgment[] DefaultSuccessJudgments =
        {
            TimedHitJudgment.Perfect,
            TimedHitJudgment.Good
        };

        private readonly string logTag;

        internal SystemStepRunner(string logTag)
        {
            this.logTag = string.IsNullOrWhiteSpace(logTag) ? "StepScheduler" : logTag;
        }

        internal bool TryHandle(ActionStep step, StepSchedulerContext context, ExecutionState state, out StepResult result)
        {
            result = StepResult.Skipped;

            if (string.IsNullOrWhiteSpace(step.ExecutorId))
            {
                return false;
            }
            string executorId = step.ExecutorId ?? string.Empty;
            switch (executorId.ToLowerInvariant())
            {
                case SystemStepWindowOpen:
                    HandleWindowOpen(step, context, state);
                    result = StepResult.Completed;
                    return true;

                case SystemStepWindowClose:
                    HandleWindowClose(step, context, state);
                    result = StepResult.Completed;
                    return true;

                case SystemStepGate:
                    result = HandleGate(step, context, state);
                    return true;

                case SystemStepDamage:
                    HandleDamage(step, context);
                    result = StepResult.Completed;
                    return true;

                case SystemStepPhaseLock:
                    HandlePhaseLock(step, context, state, true);
                    result = StepResult.Completed;
                    return true;

                case SystemStepPhaseUnlock:
                    HandlePhaseLock(step, context, state, false);
                    result = StepResult.Completed;
                    return true;

                case SystemStepFallback:
                    result = HandleFallback(step, context);
                    return true;

                default:
                    return false;
            }
        }

        private void HandleWindowOpen(ActionStep step, StepSchedulerContext context, ExecutionState state)
        {
            var parameters = step.Parameters;
            if (!TryGetRequired(parameters, "id", out var id))
            {
                BattleLogger.Warn(logTag, "window.open missing 'id'.");
                return;
            }

            string tag = parameters.TryGetString("tag", out var tagValue) ? tagValue : id;
            float start = parameters.TryGetFloat("start", out var s) ? s : parameters.TryGetFloat("startNormalized", out s) ? s : 0f;
            float end = parameters.TryGetFloat("end", out var e) ? e : parameters.TryGetFloat("endNormalized", out e) ? e : 1f;
            int index = parameters.TryGetInt("index", out var idx) ? idx : parameters.TryGetInt("windowIndex", out idx) ? idx : 0;
            int count = parameters.TryGetInt("count", out var cnt) ? cnt : parameters.TryGetInt("windowCount", out cnt) ? cnt : 1;
            string payload = BuildPayload(parameters, new[] { "id", "tag", "start", "startNormalized", "end", "endNormalized", "index", "windowIndex", "count", "windowCount" });

            state.RegisterWindow(id, new ExecutionState.WindowState(id, tag));
            context.EventBus?.Publish(new AnimationWindowEvent(context.Actor, tag, payload, start, end, true, index, count));
        }

        private void HandleWindowClose(ActionStep step, StepSchedulerContext context, ExecutionState state)
        {
            var parameters = step.Parameters;
            if (!TryGetRequired(parameters, "id", out var id))
            {
                BattleLogger.Warn(logTag, "window.close missing 'id'.");
                return;
            }

            ExecutionState.WindowState window = null;
            if (!state.TryRemoveWindow(id, out window))
            {
                BattleLogger.Warn(logTag, $"window.close('{id}') called but window not open.");
            }

            string tag = parameters.TryGetString("tag", out var tagValue) ? tagValue : window?.Tag ?? id;
            float start = parameters.TryGetFloat("start", out var s) ? s : parameters.TryGetFloat("startNormalized", out s) ? s : 0f;
            float end = parameters.TryGetFloat("end", out var e) ? e : parameters.TryGetFloat("endNormalized", out e) ? e : 1f;
            int index = parameters.TryGetInt("index", out var idx) ? idx : parameters.TryGetInt("windowIndex", out idx) ? idx : 0;
            int count = parameters.TryGetInt("count", out var cnt) ? cnt : parameters.TryGetInt("windowCount", out cnt) ? cnt : 1;
            string payload = BuildPayload(parameters, new[] { "id", "tag", "start", "startNormalized", "end", "endNormalized", "index", "windowIndex", "count", "windowCount" });

            context.EventBus?.Publish(new AnimationWindowEvent(context.Actor, tag, payload, start, end, false, index, count));
        }

        private StepResult HandleGate(ActionStep step, StepSchedulerContext context, ExecutionState state)
        {
            var parameters = step.Parameters;
            if (!TryGetRequired(parameters, "id", out var id))
            {
                BattleLogger.Warn(logTag, "gate.on missing 'id'.");
                return StepResult.Failed;
            }

            string successLabel = parameters.TryGetString("success", out var success) ? success : null;
            string failLabel = parameters.TryGetString("fail", out var fail) ? fail : null;
            string timeoutLabel = parameters.TryGetString("timeout", out var timeout) ? timeout : failLabel;

            var judgments = ParseJudgmentList(parameters.TryGetString("successOn", out var list) ? list : null);
            if (!state.TryGetWindowResult(id, out var hitResult))
            {
                if (string.IsNullOrWhiteSpace(timeoutLabel))
                {
                    return StepResult.Completed;
                }

                bool abortOnTimeout = parameters.TryGetBool("abortOnTimeout", out var abortTimeout) && abortTimeout;
                return abortOnTimeout ? StepResult.Abort("GateTimeout") : StepResult.Branch(timeoutLabel);
            }

            bool isSuccess = judgments.Contains(hitResult.Judgment);
            string branchTarget = isSuccess ? successLabel : failLabel;

            if (string.IsNullOrWhiteSpace(branchTarget))
            {
                return StepResult.Completed;
            }

            bool abort =
                (isSuccess && parameters.TryGetBool("abortOnSuccess", out var abortOnSuccess) && abortOnSuccess) ||
                (!isSuccess && parameters.TryGetBool("abortOnFail", out var abortOnFail) && abortOnFail);

            if (abort)
            {
                return StepResult.Abort(isSuccess ? "GateAbortSuccess" : "GateAbortFail");
            }

            return StepResult.Branch(branchTarget);
        }

        private void HandleDamage(ActionStep step, StepSchedulerContext context)
        {
            if (!TryGetRequired(step.Parameters, "formula", out var formula))
            {
                BattleLogger.Warn(logTag, "damage.apply missing 'formula'.");
                return;
            }

            var evt = new AnimationDamageRequestEvent(
                context.Actor,
                context.Request.Selection.Action,
                context.Request.Targets,
                formula,
                step.Parameters.Data);

            context.EventBus?.Publish(evt);
        }

        private StepResult HandleFallback(ActionStep step, StepSchedulerContext context)
        {
            string timelineId = step.Parameters.TryGetString("timelineId", out var timeline) ? timeline : null;
            string recipeId = step.Parameters.TryGetString("recipeId", out var recipe) ? recipe : null;
            string reason = step.Parameters.TryGetString("reason", out var r) ? r : "FallbackTriggered";

            if (string.IsNullOrWhiteSpace(timelineId) && string.IsNullOrWhiteSpace(recipeId))
            {
                BattleLogger.Warn(logTag, "fallback step requires 'timelineId' or 'recipeId'.");
                return StepResult.Failed;
            }

            context.EventBus?.Publish(new AnimationFallbackRequestedEvent(context.Actor, timelineId, recipeId, reason));
            return StepResult.Abort(reason);
        }

        private void HandlePhaseLock(ActionStep step, StepSchedulerContext context, ExecutionState state, bool locked)
        {
            string reason = step.Parameters.TryGetString("reason", out var value) ? value : step.Id ?? "timeline";
            if (locked)
            {
                if (state.RegisterLock(reason))
                {
                    context.EventBus?.Publish(new AnimationLockEvent(context.Actor, true, reason));
                }
            }
            else
            {
                if (state.ReleaseLock(reason))
                {
                    context.EventBus?.Publish(new AnimationLockEvent(context.Actor, false, reason));
                }
            }
        }

        private static string BuildPayload(ActionStepParameters parameters, IEnumerable<string> excludedKeys)
        {
            var excluded = new HashSet<string>(excludedKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();

            if (!parameters.IsEmpty)
            {
                foreach (var kv in parameters.Data)
                {
                    if (excluded.Contains(kv.Key))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                    {
                        continue;
                    }

                    list.Add($"{kv.Key}={kv.Value}");
                }
            }

            return list.Count == 0 ? string.Empty : string.Join(";", list);
        }

        private static bool TryGetRequired(ActionStepParameters parameters, string key, out string value)
        {
            if (parameters.TryGetString(key, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private static HashSet<TimedHitJudgment> ParseJudgmentList(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return new HashSet<TimedHitJudgment>(DefaultSuccessJudgments);
            }

            var set = new HashSet<TimedHitJudgment>();
            var tokens = csv.Split(',');
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i].Trim();
                if (Enum.TryParse<TimedHitJudgment>(token, true, out var judgment))
                {
                    set.Add(judgment);
                }
            }

            if (set.Count == 0)
            {
                return new HashSet<TimedHitJudgment>(DefaultSuccessJudgments);
            }

            return set;
        }
    }
}


