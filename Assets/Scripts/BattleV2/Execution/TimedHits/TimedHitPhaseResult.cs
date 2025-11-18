using BattleV2.Core;

namespace BattleV2.Execution.TimedHits
{
    public readonly struct TimedHitPhaseResult
    {
        public TimedHitPhaseResult(
            int index,
            bool isSuccess,
            float damageMultiplier,
            float accuracyNormalized,
            CombatantState actor = null)
        {
            Index = index;
            IsSuccess = isSuccess;
            DamageMultiplier = damageMultiplier;
            AccuracyNormalized = accuracyNormalized;
            Actor = actor;
        }

        public int Index { get; }
        public bool IsSuccess { get; }
        public float DamageMultiplier { get; }
        public float AccuracyNormalized { get; }
        public CombatantState Actor { get; }
    }
}
