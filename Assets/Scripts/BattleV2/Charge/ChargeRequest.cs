using BattleV2.Actions;
using BattleV2.Core;

namespace BattleV2.Charge
{
    public readonly struct ChargeRequest
    {
        public ChargeRequest(BattleActionContext context, BattleActionData action, ChargeProfile profile, int availableCp, int baseCpCost)
        {
            Context = context;
            Action = action;
            Profile = profile;
            AvailableCp = availableCp;
            BaseCpCost = baseCpCost;
        }

        public BattleActionContext Context { get; }
        public BattleActionData Action { get; }
        public ChargeProfile Profile { get; }
        public int AvailableCp { get; }
        public int BaseCpCost { get; }

        public int MaxCharge => AvailableCp > BaseCpCost ? AvailableCp - BaseCpCost : 0;
    }
}
