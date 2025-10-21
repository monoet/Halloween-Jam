using BattleV2.Actions;

namespace BattleV2.Execution
{
    public interface IActionPipelineFactory
    {
        ActionPipeline CreatePipeline(BattleActionData actionData, IAction actionImplementation);
    }
}

