using System;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime.Recipes;

namespace BattleV2.AnimationSystem.Strategies
{
    internal sealed class TurnPhaseStrategy : IPhaseStrategy
    {
        public void OnEnter(StrategyContext context)
        {
            _ = RunSequenceAsync(context);
        }

        private async System.Threading.Tasks.Task RunSequenceAsync(StrategyContext context)
        {
            if (context == null)
            {
                return;
            }

            context.LogInfo("▶ Enter TurnPhase");

            var orchestrator = context.Orchestrator;
            if (orchestrator == null)
            {
                context.LogWarn("TurnPhaseStrategy missing orchestrator reference.");
                return;
            }

            var animationContext = context.AnimationContext;

            try
            {
                await orchestrator.PlayRecipeAsync("router:ui:spotlight_in", animationContext);
                await orchestrator.PlayRecipeAsync(PilotActionRecipes.TurnIntroId, animationContext);
                await orchestrator.PlayRecipeAsync(PilotActionRecipes.RunUpId, animationContext);
                await orchestrator.PlayRecipeAsync(PilotActionRecipes.IdleId, animationContext);
            }
            catch (Exception ex)
            {
                context.LogError($"TurnPhase sequence failed: {ex.Message}");
            }
        }

        public void OnExit(StrategyContext context)
        {
            context?.LogInfo("⏹ Exit TurnPhase");
        }
    }
}
