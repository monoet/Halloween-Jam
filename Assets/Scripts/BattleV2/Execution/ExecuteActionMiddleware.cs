using System;
using System.Threading.Tasks;

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

            if (next != null)
            {
                await next();
            }
        }
    }
}

