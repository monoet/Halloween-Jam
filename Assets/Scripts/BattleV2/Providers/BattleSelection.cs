using BattleV2.Actions;
using BattleV2.Charge;

namespace BattleV2.Providers
{
    public readonly struct BattleSelection
    {
        public BattleSelection(BattleActionData action, int cpCharge = 0, ChargeProfile profile = null)
        {
            Action = action;
            CpCharge = cpCharge;
            Profile = profile;
        }

        public BattleActionData Action { get; }
        public int CpCharge { get; }
        public ChargeProfile Profile { get; }
    }
}

