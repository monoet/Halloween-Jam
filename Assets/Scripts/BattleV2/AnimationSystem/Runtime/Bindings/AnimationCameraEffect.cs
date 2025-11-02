using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime.Bindings
{
    /// <summary>
    /// Base class for camera effects triggered by animation payloads.
    /// Extend this ScriptableObject to implement custom zooms, shakes, etc.
    /// </summary>
    public abstract class AnimationCameraEffect : ScriptableObject
    {
        /// <summary>
        /// Applies the camera effect. Return true when the effect was handled.
        /// </summary>
        public abstract bool TryApply(
            CombatantState actor,
            AnimationImpactEvent? impactEvent,
            AnimationPhaseEvent? phaseEvent,
            in AnimationEventPayload payload);

        /// <summary>
        /// Clears any persistent state for the supplied actor. Optional.
        /// </summary>
        public virtual void Reset(CombatantState actor) { }
    }
}
