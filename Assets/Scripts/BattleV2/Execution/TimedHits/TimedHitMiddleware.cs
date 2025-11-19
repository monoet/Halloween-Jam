using System;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Orchestration;

namespace BattleV2.Execution.TimedHits
{
    public sealed class TimedHitMiddleware : IActionMiddleware
    {
        private readonly ITimedHitService timedHitService;
        private readonly ITimedHitAction timedHitAction;

        public TimedHitMiddleware(ITimedHitService timedHitService, ITimedHitAction timedHitAction)
        {
            this.timedHitService = timedHitService;
            this.timedHitAction = timedHitAction;
        }

        public async Task InvokeAsync(ActionContext context, Func<Task> next)
        {
            var selection = context.Selection;
            if (selection.TimedHitResult.HasValue)
            {
                var resolved = selection.TimedHitResult.Value;
                context.TimedResult = resolved;

                if (resolved.Cancelled)
                {
                    context.Cancelled = true;
                    return;
                }

                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
                return;
            }

            var handle = selection.TimedHitHandle;
            if (handle != null)
            {
                try
                {
                    var awaited = await handle.WaitAsync(context.CancellationToken).ConfigureAwait(false);
                    if (awaited.HasValue)
                    {
                        context.TimedResult = awaited.Value;
                        if (awaited.Value.Cancelled)
                        {
                            context.Cancelled = true;
                            return;
                        }
                    }
                    else
                    {
                        context.TimedResult = awaited;
                    }
                }
                catch (OperationCanceledException)
                {
                    context.Cancelled = true;
                    return;
                }

                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
                return;
            }

            var profile = selection.TimedHitProfile ?? timedHitAction?.TimedHitProfile;
            var basicProfile = selection.BasicTimedHitProfile;
            if (basicProfile == null && timedHitAction is IBasicTimedHitAction basicTimedAction)
            {
                basicProfile = basicTimedAction.BasicTimedHitProfile;
            }

            var runnerKind = selection.RunnerKind;
            if (runnerKind == TimedHitRunnerKind.Basic && basicProfile == null)
            {
                BattleLogger.Warn("TimedHitMiddleware", $"Selection for action '{selection.Action?.id}' requested Basic runner without a profile.");
                runnerKind = TimedHitRunnerKind.Default;
            }

            if (profile == null && basicProfile == null)
            {
                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
                return;
            }

            var request = new TimedHitRequest(
                context.Attacker,
                context.Target,
                context.ActionData,
                selection.ChargeProfile,
                profile,
                selection.CpCharge,
                TimedHitRunMode.Execute,
                context.CancellationToken,
                basicProfile,
                runnerKind);

            TimedHitResult result;
            try
            {
                if (timedHitService != null)
                {
                    result = await timedHitService.RunAsync(request, context.PhaseResultListener).ConfigureAwait(false);
                }
                else
                {
                    result = await RunWithFallbackAsync(request, context.PhaseResultListener).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                context.Cancelled = true;
                return;
            }

            if (result.Cancelled)
            {
                context.Cancelled = true;
                return;
            }

            context.TimedResult = result;

            if (next != null)
            {
                await next().ConfigureAwait(false);
            }
        }

        private static Task<TimedHitResult> RunWithFallbackAsync(
            TimedHitRequest request,
            Action<TimedHitPhaseResult> phaseListener)
        {
            var runner = InstantTimedHitRunner.Shared;
            if (phaseListener == null)
            {
                return runner.RunAsync(request);
            }

            void Handler(TimedHitPhaseResult phase) => phaseListener(phase);
            runner.OnPhaseResolved += Handler;
            var task = runner.RunAsync(request);

            if (task.IsCompleted)
            {
                runner.OnPhaseResolved -= Handler;
                return task;
            }

            return AwaitRunnerAsync(runner, task, Handler);
        }

        private static async Task<TimedHitResult> AwaitRunnerAsync(
            ITimedHitRunner runner,
            Task<TimedHitResult> task,
            Action<TimedHitPhaseResult> handler)
        {
            try
            {
                return await task.ConfigureAwait(false);
            }
            finally
            {
                runner.OnPhaseResolved -= handler;
            }
        }
    }
}
