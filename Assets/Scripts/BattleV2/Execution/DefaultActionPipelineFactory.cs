using BattleV2.Actions;
using System.Collections.Generic;

namespace BattleV2.Execution
{
    public sealed class DefaultActionPipelineFactory : IActionPipelineFactory
    {
        public ActionPipeline CreatePipeline(BattleActionData actionData, IAction actionImplementation)
        {
            var middlewares = new IActionMiddleware[]
            {
                new ExecuteActionMiddleware()
            };

            return new ActionPipeline(middlewares);
        }
    }
}
