using System.Threading;
using System.Threading.Tasks;

namespace BattleV2.AnimationSystem.Strategies
{
    internal sealed class NoOpRecipeExecutor : IRecipeExecutor
    {
        public static readonly NoOpRecipeExecutor Instance = new NoOpRecipeExecutor();

        private NoOpRecipeExecutor()
        {
        }

        public bool CanExecute(string recipeId, StrategyContext context)
        {
            return false;
        }

        public Task ExecuteAsync(string recipeId, StrategyContext context, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }
    }
}
