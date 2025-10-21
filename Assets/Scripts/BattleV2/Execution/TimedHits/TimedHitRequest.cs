using System.Threading;
using BattleV2.Charge;
using BattleV2.Core;

namespace BattleV2.Execution.TimedHits
{
    public readonly struct TimedHitRequest
    {
        public TimedHitRequest(
            CombatantState attacker,
            CombatantState target,
            Ks1TimedHitProfile profile,
            int cpCharge,
            TimedHitRunMode mode,
            CancellationToken cancellationToken)
        {
            Attacker = attacker;
            Target = target;
            Profile = profile;
            CpCharge = cpCharge;
            Mode = mode;
            CancellationToken = cancellationToken;
        }

        public CombatantState Attacker { get; }
        public CombatantState Target { get; }
        public Ks1TimedHitProfile Profile { get; }
        public int CpCharge { get; }
        public TimedHitRunMode Mode { get; }
        public CancellationToken CancellationToken { get; }
    }
}


