using System;
using System.Threading;
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
            int execId = context?.Selection.TimedHitHandle != null ? context.Selection.TimedHitHandle.ExecutionId : 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevCpTrace)
            {
                BattleDiagnostics.Log(
                    "CPTRACE",
                    $"TH_BEGIN exec={execId} action={context?.Selection.Action?.id ?? "(null)"} cp={context?.Selection.CpCharge ?? 0} hasHandle={(context?.Selection.TimedHitHandle != null)} hasResult={(context?.Selection.TimedHitResult.HasValue ?? false)}",
                    context?.Attacker);
            }
#endif
            BattleDiagnostics.Log(
                "Thread.debug00",
                $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.Enter] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                context?.Attacker);

            var selection = context.Selection;
            if (selection.TimedHitResult.HasValue)
            {
                var resolved = selection.TimedHitResult.Value;
                context.TimedResult = resolved;

                if (resolved.Cancelled)
                {
                    context.Cancelled = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (BattleDiagnostics.DevCpTrace)
                    {
                        BattleDiagnostics.Log(
                            "CPTRACE",
                            $"TH_END exec={execId} outcome=Cancelled source=SelectionResult",
                            context?.Attacker);
                    }
#endif
                    return;
                }

                if (next != null)
                {
                    BattleDiagnostics.Log(
                        "Thread.debug00",
                        $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.AwaitNext.Before] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                        context.Attacker);
                    await next();
                    BattleDiagnostics.Log(
                        "Thread.debug00",
                        $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.AwaitNext.After] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                        context.Attacker);
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevCpTrace)
                {
                    BattleDiagnostics.Log(
                        "CPTRACE",
                        $"TH_END exec={execId} outcome=Resolved source=SelectionResult",
                        context?.Attacker);
                }
#endif
                return;
            }

            var handle = selection.TimedHitHandle;
            if (handle != null)
            {
                try
                {
                    BattleDiagnostics.Log(
                        "Thread.debug00",
                        $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.Handle.Wait.Before] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                        context.Attacker);
                    var awaited = await handle.WaitAsync(context.CancellationToken);
                    BattleDiagnostics.Log(
                        "Thread.debug00",
                        $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.Handle.Wait.After] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                        context.Attacker);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (BattleDiagnostics.DevCpTrace)
                    {
                        BattleDiagnostics.Log(
                            "CPTRACE",
                            $"TH_HANDLE_WAIT_DONE exec={execId} hasValue={awaited.HasValue} cancelled={(awaited.HasValue && awaited.Value.Cancelled)}",
                            context?.Attacker);
                    }
#endif
                    if (awaited.HasValue)
                    {
                        context.TimedResult = awaited.Value;
                        if (awaited.Value.Cancelled)
                        {
                            context.Cancelled = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (BattleDiagnostics.DevCpTrace)
                            {
                                BattleDiagnostics.Log(
                                    "CPTRACE",
                                    $"TH_END exec={execId} outcome=Cancelled source=HandleWait",
                                    context?.Attacker);
                            }
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (BattleDiagnostics.DevCpTrace)
                    {
                        BattleDiagnostics.Log(
                            "CPTRACE",
                            $"TH_END exec={execId} outcome=Cancelled source=HandleWaitException",
                            context?.Attacker);
                    }
#endif
                    return;
                }

                if (next != null)
                {
                    BattleDiagnostics.Log(
                        "Thread.debug00",
                        $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.AwaitNext.Before] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                        context.Attacker);
                    await next();
                    BattleDiagnostics.Log(
                        "Thread.debug00",
                        $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.AwaitNext.After] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                        context.Attacker);
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevCpTrace)
                {
                    var resolved = context.TimedResult;
                    BattleDiagnostics.Log(
                        "CPTRACE",
                        $"TH_END exec={execId} outcome=Resolved source=HandleWait hasResult={resolved.HasValue} cancelled={(resolved.HasValue && resolved.Value.Cancelled)}",
                        context?.Attacker);
                }
#endif
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
                    await next();
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
                runnerKind,
                execId);

            TimedHitResult result;
            try
            {
                if (timedHitService != null)
                {
                    BattleDiagnostics.Log(
                        "Thread.debug00",
                        $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.RunAsync.Before] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                        context.Attacker);
                    result = await timedHitService.RunAsync(request, context.PhaseResultListener);
                    BattleDiagnostics.Log(
                        "Thread.debug00",
                        $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.RunAsync.After] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                        context.Attacker);
                }
                else
                {
                    BattleDiagnostics.Log(
                        "Thread.debug00",
                        $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.RunFallback.Before] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                        context.Attacker);
                    result = await RunWithFallbackAsync(request, context.PhaseResultListener);
                    BattleDiagnostics.Log(
                        "Thread.debug00",
                        $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.RunFallback.After] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                        context.Attacker);
                }
            }
            catch (OperationCanceledException)
            {
                context.Cancelled = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevCpTrace)
                {
                    BattleDiagnostics.Log(
                        "CPTRACE",
                        $"TH_END exec={execId} outcome=Cancelled source=RunAsyncException",
                        context?.Attacker);
                }
#endif
                return;
            }

            if (result.Cancelled)
            {
                context.Cancelled = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevCpTrace)
                {
                    BattleDiagnostics.Log(
                        "CPTRACE",
                        $"TH_END exec={execId} outcome=Cancelled source=RunAsyncResult",
                        context?.Attacker);
                }
#endif
                return;
            }

            context.TimedResult = result;

            if (next != null)
            {
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.AwaitNext.Before] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                    context.Attacker);
                await next();
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][MW.{nameof(TimedHitMiddleware)}.AwaitNext.After] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                    context.Attacker);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevCpTrace)
            {
                BattleDiagnostics.Log(
                    "CPTRACE",
                    $"TH_END exec={execId} outcome=Resolved source=RunAsync judgment={result.Judgment}",
                    context?.Attacker);
            }
#endif
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
                return await task;
            }
            finally
            {
                runner.OnPhaseResolved -= handler;
            }
        }
    }
}
