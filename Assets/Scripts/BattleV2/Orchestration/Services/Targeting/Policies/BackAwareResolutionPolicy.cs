using BattleV2.Orchestration;
using BattleV2.Orchestration.Services;
using BattleV2.Actions;

namespace BattleV2.Targeting.Policies
{
    /// <summary>
    /// Experimental policy that distinguishes between Back (return to menu) and Cancel (end turn).
    /// </summary>
    public class BackAwareResolutionPolicy : ITargetResolutionPolicy
    {
        public TargetResolutionStatus Interpret(TargetResolutionResult result, BattleActionData action)
        {
            // If the result explicitly says Back, respect it.
            if (result.Status == TargetResolutionStatus.Back)
            {
                return TargetResolutionStatus.Back;
            }

            // If the result explicitly says Cancelled, respect it.
            if (result.Status == TargetResolutionStatus.Cancelled)
            {
                return TargetResolutionStatus.Cancelled;
            }

            // If targets are empty but not explicitly Back/Cancelled, 
            // check if the action requires targets.
            if (result.TargetSet.IsEmpty)
            {
                // If the action DOES NOT require targets, an empty set is valid (Confirmed).
                if (action != null && !action.requiresTarget)
                {
                    return TargetResolutionStatus.Confirmed;
                }

                // If it DOES require targets, then empty means Back (soft cancel).
                return TargetResolutionStatus.Back;
            }

            return TargetResolutionStatus.Confirmed;
        }
    }
}
