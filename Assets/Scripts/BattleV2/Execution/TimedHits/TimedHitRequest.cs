using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Actions;
using System.Threading;

namespace BattleV2.Execution.TimedHits
{
    public readonly struct TimedHitRequest
    {
        public TimedHitRequest(
            CombatantState attacker,
            CombatantState target,
            BattleActionData actionData,
            ChargeProfile chargeProfile,
            Ks1TimedHitProfile profile,
            int cpCharge,
            TimedHitRunMode mode,
            CancellationToken cancellationToken)
        {
            Attacker = attacker;
            Target = target;
            ActionData = actionData;
            ChargeProfile = chargeProfile;
            Profile = profile;
            CpCharge = cpCharge;
            Mode = mode;
            CancellationToken = cancellationToken;
        }

        public CombatantState Attacker { get; }
        public CombatantState Target { get; }
        public BattleActionData ActionData { get; }
        public ChargeProfile ChargeProfile { get; }
        public Ks1TimedHitProfile Profile { get; }
        public int CpCharge { get; }
        public TimedHitRunMode Mode { get; }
        public CancellationToken CancellationToken { get; }
    }
}


