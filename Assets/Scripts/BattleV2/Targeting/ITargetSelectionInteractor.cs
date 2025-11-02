using System.Threading.Tasks;

namespace BattleV2.Targeting
{
    /// <summary>
    /// Allows custom UX (debug harness, UI) to drive manual target selection.
    /// </summary>
    public interface ITargetSelectionInteractor
    {
        Task<TargetSet> SelectAsync(TargetContext context, TargetSet proposedSet);
    }
}
