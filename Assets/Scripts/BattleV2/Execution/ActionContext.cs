using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Orchestration;
using BattleV2.Providers;
using System.Threading;

namespace BattleV2.Execution
{
    /// <summary>
    /// Carries execution data through the action middleware pipeline.
    /// </summary>
    public sealed class ActionContext
    {
        public ActionContext(
            BattleManagerV2 manager,
            CombatantState attacker,
            CombatantState target,
            BattleActionData actionData,
            IAction actionImplementation,
            CombatContext combatContext,
            BattleSelection selection,
            CancellationToken cancellationToken = default)
        {
            Manager = manager;
            Attacker = attacker;
            Target = target;
            ActionData = actionData;
            ActionImplementation = actionImplementation;
            CombatContext = combatContext;
            Selection = selection;
            CancellationToken = cancellationToken;
        }

        public BattleManagerV2 Manager { get; }
        public CombatantState Attacker { get; }
        public CombatantState Target { get; }
        public BattleActionData ActionData { get; }
        public IAction ActionImplementation { get; }
        public CombatContext CombatContext { get; }
        public BattleSelection Selection { get; }
        public int CpCharge => Selection.CpCharge;
        public CancellationToken CancellationToken { get; }

        public bool Cancelled { get; set; }
        public TimedHitResult? TimedResult { get; set; }
        public bool PhaseDamageApplied { get; set; }
        public int TotalDamageApplied { get; set; }
        public int ComboPointsAwarded { get; set; }
    }
}






