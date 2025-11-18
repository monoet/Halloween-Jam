using BattleV2.AnimationSystem;
using BattleV2.Charge;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Represents the outcome of a single KS1 phase so animation/audio bridges can react per-window.
    /// </summary>
    public readonly struct Ks1PhaseOutcome
    {
        public Ks1PhaseOutcome(
            int phaseIndex,
            int totalPhases,
            TimedHitJudgment judgment,
            bool chainCancelled,
            bool chainCompleted,
            TimedHitResult result)
        {
            PhaseIndex = phaseIndex;
            TotalPhases = totalPhases;
            Judgment = judgment;
            ChainCancelled = chainCancelled;
            ChainCompleted = chainCompleted;
            Result = result;
        }

        /// <summary>Zero-based index of the phase that just resolved.</summary>
        public int PhaseIndex { get; }

        /// <summary>Total number of phases configured for the KS1 tier.</summary>
        public int TotalPhases { get; }

        /// <summary>Judgment emitted by the timed-hit service for this window.</summary>
        public TimedHitJudgment Judgment { get; }

        /// <summary>True when the chain is cancelled because of this outcome.</summary>
        public bool ChainCancelled { get; }

        /// <summary>True when this phase completed the entire chain (success case).</summary>
        public bool ChainCompleted { get; }

        /// <summary>Canonical result snapshot for this phase.</summary>
        public TimedHitResult Result { get; }
    }
}
