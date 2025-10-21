using System;
using System.Threading.Tasks;
using BattleV2.Orchestration;

namespace BattleV2.Execution
{
    /// <summary>
    /// Placeholder middleware for triggering attack animations before the execution pipeline runs.
    /// </summary>
    public sealed class AnimationStartMiddleware : IActionMiddleware
    {
        private readonly BattleManagerV2 manager;

        public AnimationStartMiddleware(BattleManagerV2 manager)
        {
            this.manager = manager;
        }

        public Task InvokeAsync(ActionContext context, Func<Task> next)
        {
            // TODO: integrate BattleAnimationController once available.
            return next != null ? next() : Task.CompletedTask;
        }
    }
}
