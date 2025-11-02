using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime.Bindings
{
    /// <summary>
    /// Base class for UI actions triggered by timeline payloads.
    /// </summary>
    public abstract class AnimationUiAction : ScriptableObject
    {
        /// <summary>
        /// Handles the UI payload. Return true when the action was processed.
        /// </summary>
        public abstract bool TryHandle(
            CombatantState actor,
            AnimationPhaseEvent? phaseEvent,
            AnimationWindowEvent? windowEvent,
            AnimationImpactEvent? impactEvent,
            in AnimationEventPayload payload);

        /// <summary>
        /// Clears UI state associated with the actor. Optional.
        /// </summary>
        public virtual void Clear(CombatantState actor) { }
    }
}
