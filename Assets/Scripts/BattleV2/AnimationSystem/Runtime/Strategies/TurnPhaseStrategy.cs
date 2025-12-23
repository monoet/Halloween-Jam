using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime.Recipes;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;

namespace BattleV2.AnimationSystem.Strategies
{
    internal sealed class TurnPhaseStrategy : IPhaseStrategy
    {
        public void OnEnter(StrategyContext context)
        {
            var invoker = MainThreadInvoker.Instance;
            if (invoker != null)
            {
                _ = invoker.RunAsync(() => RunSequenceAsync(context));
            }
            else
            {
                _ = RunSequenceAsync(context);
            }
        }

        private async Task RunSequenceAsync(StrategyContext context)
        {
            if (context == null)
            {
                return;
            }

            var orchestrator = context.Orchestrator;
            if (orchestrator == null)
            {
                context.LogWarn("TurnPhaseStrategy missing orchestrator reference.");
                return;
            }

            var animationContext = context.AnimationContext;
            var primaryActor = animationContext.PrimaryActor;

            try
            {
                await orchestrator.PlayRecipeAsync("router:ui:spotlight_in", animationContext).ConfigureAwait(true);
                await orchestrator.PlayRecipeAsync(PilotActionRecipes.TurnIntroId, animationContext).ConfigureAwait(true);
                await orchestrator.PlayRecipeAsync(PilotActionRecipes.RunUpId, animationContext).ConfigureAwait(true);
                await orchestrator.PlayRecipeAsync(PilotActionRecipes.IdleId, animationContext).ConfigureAwait(true);
                BattleV2.AnimationSystem.Runtime.IdleEnsureUtility.EnsureIdleNextTick(primaryActor, "TurnPhaseStrategy.PostRunUp");
            }
            catch (System.Exception ex)
            {
                context.LogError($"TurnPhase sequence failed: {ex.Message}");
            }
        }

        public void OnExit(StrategyContext context)
        {
            context?.LogInfo("Exit TurnPhase");
        }
    }
}
