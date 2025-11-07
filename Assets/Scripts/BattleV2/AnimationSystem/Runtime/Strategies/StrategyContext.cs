using BattleV2.AnimationSystem;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Strategies
{
    /// <summary>
    /// Shared payload supplied to strategies so they can log consistently and understand orchestration context.
    /// </summary>
    public sealed class StrategyContext
    {
        public StrategyContext(
            string logScope,
            AnimationContext animationContext,
            BattlePhase phase = BattlePhase.None,
            string recipeId = null)
        {
            LogScope = string.IsNullOrWhiteSpace(logScope) ? "AnimStrategy" : logScope;
            AnimationContext = animationContext;
            Phase = phase;
            RecipeId = recipeId;
        }

        public string LogScope { get; }
        public AnimationContext AnimationContext { get; }
        public BattlePhase Phase { get; }
        public string RecipeId { get; }

        public StrategyContext WithPhase(BattlePhase phase)
        {
            return new StrategyContext(LogScope, AnimationContext, phase, RecipeId);
        }

        public StrategyContext WithRecipe(string recipeId)
        {
            return new StrategyContext(LogScope, AnimationContext, Phase, recipeId);
        }

        public void LogInfo(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                StrategyLoggerBridge.Info(LogScope, message);
            }
        }

        public void LogWarn(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                StrategyLoggerBridge.Warn(LogScope, message);
            }
        }

        public void LogError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                StrategyLoggerBridge.Error(LogScope, message);
            }
        }
    }
}
