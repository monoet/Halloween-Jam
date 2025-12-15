using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Execution.TimedHits;
using BattleV2.Providers;

namespace BattleV2.Charge
{
    public readonly struct ChargeRequest
    {
        public ChargeRequest(
            BattleActionContext context,
            BattleActionData action,
            ChargeProfile chargeProfile,
            int availableCp,
            int baseCpCost,
            Ks1TimedHitProfile timedHitProfile = null,
            BasicTimedHitProfile basicTimedHitProfile = null,
            TimedHitRunnerKind runnerKind = TimedHitRunnerKind.Default)
        {
            Context = context;
            Action = action;
            ChargeProfile = chargeProfile;
            AvailableCp = availableCp;
            BaseCpCost = baseCpCost;
            TimedHitProfile = timedHitProfile;
            BasicTimedHitProfile = basicTimedHitProfile;
            RunnerKind = runnerKind;
        }

        public BattleActionContext Context { get; }
        public BattleActionData Action { get; }
        public ChargeProfile ChargeProfile { get; }
        public Ks1TimedHitProfile TimedHitProfile { get; }
        public BasicTimedHitProfile BasicTimedHitProfile { get; }
        public TimedHitRunnerKind RunnerKind { get; }
        public int AvailableCp { get; }
        public int BaseCpCost { get; }

        public int MaxCharge => AvailableCp > BaseCpCost ? AvailableCp - BaseCpCost : 0;
    }
}
