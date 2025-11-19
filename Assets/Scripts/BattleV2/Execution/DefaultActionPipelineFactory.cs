using BattleV2.Actions;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.Execution.TimedHits;
using BattleV2.Orchestration;
using System.Collections.Generic;

namespace BattleV2.Execution
{
    public sealed class DefaultActionPipelineFactory : IActionPipelineFactory
    {
        private readonly BattleManagerV2 manager;
        private readonly ITimedHitService timedHitService;

        public DefaultActionPipelineFactory(BattleManagerV2 manager)
        {
            this.manager = manager;
            timedHitService = manager?.TimedHitService;
        }

        public ActionPipeline CreatePipeline(BattleActionData actionData, IAction actionImplementation)
        {
            var middlewares = new List<IActionMiddleware>();

            if (actionImplementation is ITimedHitAction timedHitAction)
            {
                middlewares.Add(new AnimationStartMiddleware());
                if (actionImplementation is ITimedHitPhaseDamageAction)
                {
                    middlewares.Add(new PhaseDamageMiddleware());
                }
                middlewares.Add(new TimedHitMiddleware(timedHitService, timedHitAction));
            }

            middlewares.Add(new ExecuteActionMiddleware());

            return new ActionPipeline(middlewares);
        }
    }
}

