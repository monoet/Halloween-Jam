using BattleV2.Orchestration;
using BattleV2.Orchestration.Services;
using BattleV2.Actions;

namespace BattleV2.Targeting.Policies
{
    public interface ITargetResolutionPolicy
    {
        TargetResolutionStatus Interpret(TargetResolutionResult result, BattleActionData action);
    }

    /// <summary>
    /// Default behavior: Any empty target set is treated as Cancelled (Legacy behavior).
    /// </summary>
    public class DefaultResolutionPolicy : ITargetResolutionPolicy
    {
        public TargetResolutionStatus Interpret(TargetResolutionResult result, BattleActionData action)
        {
            // Legacy behavior: If targets are empty, it's a Cancel.
            // If targets are present, it's Confirmed.
            // Ignores the explicit Status field for backward compatibility if needed, 
            // or we can map it directly if we trust the source.
            
            if (result.Status == TargetResolutionStatus.Back)
            {
                // In legacy mode, Back is treated as Cancel (EndTurn)
                return TargetResolutionStatus.Cancelled;
            }

            if (result.TargetSet.IsEmpty)
            {
                return TargetResolutionStatus.Cancelled;
            }

            return TargetResolutionStatus.Confirmed;
        }
    }
}
