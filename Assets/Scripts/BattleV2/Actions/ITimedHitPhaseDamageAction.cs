using BattleV2.Core;
using BattleV2.Execution.TimedHits;

namespace BattleV2.Actions
{
    /// <summary>
    /// Exposes the data required by <see cref="PhaseDamageMiddleware"/> to apply damage per phase.
    /// </summary>
    public interface ITimedHitPhaseDamageAction : ITimedHitAction
    {
        bool TryBuildPhaseDamagePlan(CombatantState actor, CombatContext context, int cpCharge, out TimedHitPhaseDamagePlan plan);
    }
}
