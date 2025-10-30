using BattleV2.AnimationSystem;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Routers
{
    public interface IAnimationVfxService
    {
        /// <summary>
        /// Attempts to play the visual effect identified by <paramref name="vfxId"/>.
        /// Returns false when the effect could not be resolved (missing asset, dependencies, etc.).
        /// </summary>
        bool TryPlay(string vfxId, in AnimationImpactEvent evt, in AnimationEventPayload payload);

        /// <summary>
        /// Stops any persistent effects bound to the supplied actor. Called when locks are released or animations cancel.
        /// </summary>
        void StopAllFor(CombatantState actor);
    }

    public interface IAnimationSfxService
    {
        bool TryPlay(
            string sfxId,
            CombatantState actor,
            AnimationImpactEvent? impactEvent,
            AnimationPhaseEvent? phaseEvent,
            in AnimationEventPayload payload);

        void StopAllFor(CombatantState actor);
    }

    public interface IAnimationCameraService
    {
        /// <summary>
        /// Applies a camera effect for the supplied payload (shake, zoom, etc.).
        /// Returns false when the effect key is not handled.
        /// </summary>
        bool TryApply(
            string effectId,
            CombatantState actor,
            AnimationImpactEvent? impactEvent,
            AnimationPhaseEvent? phaseEvent,
            in AnimationEventPayload payload);

        void Reset(CombatantState actor);
    }

    public interface IAnimationUiService
    {
        /// <summary>
        /// Processes a UI payload coming from a phase/impact/window event.
        /// </summary>
        bool TryHandle(
            string uiId,
            CombatantState actor,
            AnimationPhaseEvent? phaseEvent,
            AnimationWindowEvent? windowEvent,
            AnimationImpactEvent? impactEvent,
            in AnimationEventPayload payload);

        /// <summary>
        /// Clears transient UI widgets linked to the supplied actor.
        /// </summary>
        void Clear(CombatantState actor);
    }
}
