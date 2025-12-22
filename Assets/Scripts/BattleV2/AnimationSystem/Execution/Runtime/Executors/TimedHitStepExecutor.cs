using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution.TimedHits;
using BattleV2.Providers;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Bridges the step scheduler with the timed-hit runner so recipes can drive the interaction.
    /// </summary>
    public sealed class TimedHitStepExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "timed_hit";
        private const string LogScope = "AnimStep/TimedHit";

        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step)
        {
            return true;
        }

        public async Task ExecuteAsync(StepExecutionContext context)
        {
            BattleDiagnostics.Log(
                "Thread.debug00",
                $"[Thread.debug00][TimedHitStepExecutor.Enter] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} actor={context.Actor?.name ?? "(null)"} actionId={context.Request.Selection.Action?.id ?? "(null)"}",
                context.Actor);
            UnityThread.AssertMainThread("TimedHitStepExecutor.Execute");

            var selection = context.Request.Selection;
            var handle = selection.TimedHitHandle;
            var profile = selection.TimedHitProfile;
            var basicProfile = selection.BasicTimedHitProfile;

            if (handle == null)
            {
                BattleLogger.Warn(LogScope, "Timed hit step executed without handle. Completing immediately.");
                return;
            }

            if (profile == null && basicProfile == null)
            {
                handle.TrySetResult(default);
                return;
            }

            var target = ResolvePrimaryTarget(context);
            var mode = ResolveMode(context.Step);

            var request = new TimedHitRequest(
                context.Actor,
                target,
                selection.Action,
                selection.ChargeProfile,
                profile,
                selection.CpCharge,
                mode,
                context.CancellationToken,
                basicProfile,
                selection.RunnerKind,
                handle.ExecutionId);

            TimedHitResult result;
            try
            {
                if (context.TimedHitService != null)
                {
                    result = await context.TimedHitService.RunAsync(request);
                }
                else
                {
                    result = await InstantTimedHitRunner.Shared.RunAsync(request);
                }
            }
            catch (OperationCanceledException)
            {
                handle.TrySetCancelled(context.CancellationToken);
                throw;
            }
            catch (Exception ex)
            {
                handle.TrySetException(ex);
                throw;
            }

            handle.TrySetResult(result);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevCpTrace)
            {
                BattleDiagnostics.Log(
                    "CPTRACE",
                    $"TH_HANDLE_SET exec={handle.ExecutionId} action={selection.Action?.id ?? "(null)"} cp={selection.CpCharge} cancelled={result.Cancelled} judgment={result.Judgment}",
                    context.Actor);
            }
#endif

            if (result.Cancelled)
            {
                context.RouterBundle?.UiService?.Clear(context.Actor);
            }
        }

        private static CombatantState ResolvePrimaryTarget(StepExecutionContext context)
        {
            var targets = context.Request.Targets;
            if (targets != null && targets.Count > 0)
            {
                return targets[0];
            }

            return null;
        }

        private static TimedHitRunMode ResolveMode(ActionStep step)
        {
            if (step.Parameters.TryGetString("mode", out var modeValue) &&
                Enum.TryParse<TimedHitRunMode>(modeValue, true, out var mode))
            {
                return mode;
            }

            return TimedHitRunMode.Execute;
        }

    }
}
