using BattleV2.Actions;
using BattleV2.Charge;

namespace BattleV2.Providers
{
    public readonly struct BattleSelection
    {
        public BattleSelection(
            BattleActionData action,
            int cpCharge = 0,
            ChargeProfile chargeProfile = null,
            Ks1TimedHitProfile timedHitProfile = null)
        {
            Action = action;
            CpCharge = cpCharge;
            ChargeProfile = chargeProfile;
            TimedHitProfile = timedHitProfile;
        }

        public BattleActionData Action { get; }
        public int CpCharge { get; }
        public ChargeProfile ChargeProfile { get; }
        public Ks1TimedHitProfile TimedHitProfile { get; }
    }
}
