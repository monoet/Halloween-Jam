using BattleV2.Actions;
using BattleV2.Orchestration;
using BattleV2.Execution.TimedHits;
using System.Collections.Generic;

namespace BattleV2.Execution
{
    public sealed class DefaultActionPipelineFactory : IActionPipelineFactory
    {
        private readonly BattleManagerV2 manager;

        public DefaultActionPipelineFactory(BattleManagerV2 manager)
        {
            this.manager = manager;
        }

        public ActionPipeline CreatePipeline(BattleActionData actionData, IAction actionImplementation)
        {
            var middlewares = new List<IActionMiddleware>();

            if (actionImplementation is ITimedHitAction timedHitAction)
            {
                var runner = manager.TimedHitRunner;
                middlewares.Add(new AnimationStartMiddleware());
                if (actionImplementation is ITimedHitPhaseDamageAction)
                {
                    middlewares.Add(new PhaseDamageMiddleware(runner));
                }
                middlewares.Add(new TimedHitMiddleware(manager, timedHitAction));
            }

            middlewares.Add(new ExecuteActionMiddleware());

            return new ActionPipeline(middlewares);
        }
    }
}

