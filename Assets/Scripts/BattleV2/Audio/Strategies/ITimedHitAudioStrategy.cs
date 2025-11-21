using BattleV2.Charge;
using BattleV2.Core;

namespace BattleV2.Audio.Strategies
{
    public interface ITimedHitAudioStrategy
    {
        void Emit(TimedHitResult result, CombatantState attacker, CombatantState target);
    }
}
