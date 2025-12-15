using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Core;

namespace BattleV2.Execution
{
    /// <summary>
    /// Final middleware that invokes the IAction implementation and awaits its completion.
    /// </summary>
    public sealed class ExecuteActionMiddleware : IActionMiddleware
    {
        public Task InvokeAsync(ActionContext context, Func<Task> next)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return InvokeInternalAsync(context, next);
        }

        private static async Task InvokeInternalAsync(ActionContext context, Func<Task> next)
        {
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][MW.ExecuteActionMiddleware.Enter] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                    context.Attacker);

                context.MarkEffectsApplied("ExecuteActionMiddleware.BeforeExecute");
                context.ActionImplementation.Execute(
                    context.Attacker,
                    context.CombatContext,
                    context.CpCharge,
                    context.TimedResult,
                    () => tcs.TrySetResult(true));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }

            await tcs.Task;

            BattleDiagnostics.Log(
                "Thread.debug00",
                $"[Thread.debug00][MW.ExecuteActionMiddleware.AfterExecute] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                context.Attacker);

            if (next != null)
            {
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][MW.ExecuteActionMiddleware.AwaitNext.Before] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                    context.Attacker);
                await next();
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][MW.ExecuteActionMiddleware.AwaitNext.After] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                    context.Attacker);
            }
        }
    }
}

