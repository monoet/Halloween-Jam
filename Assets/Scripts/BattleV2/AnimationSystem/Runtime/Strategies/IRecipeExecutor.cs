using System.Threading;
using System.Threading.Tasks;

namespace BattleV2.AnimationSystem.Strategies
{
    public interface IRecipeExecutor
    {
        bool CanExecute(string recipeId, AnimationContext context);
        Task ExecuteAsync(string recipeId, AnimationContext context, CancellationToken token = default);
    }
}
