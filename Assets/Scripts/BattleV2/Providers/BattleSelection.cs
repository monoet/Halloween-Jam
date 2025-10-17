using BattleV2.Actions;

namespace BattleV2.Providers
{
    public readonly struct BattleSelection
    {
        public BattleActionData Action { get; }
        public int CpCharge { get; }

        public BattleSelection(BattleActionData action, int cpCharge = 0)
        {
            Action = action;
            CpCharge = cpCharge;
        }
    }
}

