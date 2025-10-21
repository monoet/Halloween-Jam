using System;
using System.Threading.Tasks;
using BattleV2.Anim;

namespace BattleV2.Execution
{
    /// <summary>
    /// Placeholder middleware for triggering attack animations before the execution pipeline runs.
    /// </summary>
    public sealed class AnimationStartMiddleware : IActionMiddleware
    {
        public Task InvokeAsync(ActionContext context, Func<Task> next)
        {
            BattleEvents.EmitPlayerTurnCommitted();
            return next != null ? next() : Task.CompletedTask;
        }
    }
}
