using System.Threading;
using System.Threading.Tasks;

namespace BattleV2.AnimationSystem.Strategies
{
    public interface IRecipeExecutor
    {
        bool CanExecute(string recipeId, StrategyContext context);
        Task ExecuteAsync(string recipeId, StrategyContext context, CancellationToken token = default);
    }
}
