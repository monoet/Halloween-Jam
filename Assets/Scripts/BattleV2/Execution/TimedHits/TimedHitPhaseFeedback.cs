using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Describes the per-phase damage feedback generated during a timed-hit sequence.
    /// Used by presentation layers (HUD, VFX, audio) to react without depending on battle logic.
    /// </summary>
    public readonly struct TimedHitPhaseFeedback
    {
        public TimedHitPhaseFeedback(
            CombatantState target,
            int phaseIndex,
            int totalPhases,
            bool isSuccess,
            int damage,
            float damageMultiplier,
            float accuracyNormalized,
            Vector3? worldPosition = null)
        {
            Target = target;
            PhaseIndex = phaseIndex;
            TotalPhases = totalPhases;
            IsSuccess = isSuccess;
            Damage = damage;
            DamageMultiplier = damageMultiplier;
            AccuracyNormalized = accuracyNormalized;
            WorldPosition = worldPosition;
        }

        public CombatantState Target { get; }
        public int PhaseIndex { get; }
        public int TotalPhases { get; }
        public bool IsSuccess { get; }
        public int Damage { get; }
        public float DamageMultiplier { get; }
        public float AccuracyNormalized { get; }
        public Vector3? WorldPosition { get; }
    }
}
