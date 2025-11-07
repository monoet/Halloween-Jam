using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    /// <summary>
    /// Base class for runtime-generated transform tweens plugged into CharacterAnimationSet.
    /// </summary>
    public abstract class TransformTweenProvider : ScriptableObject
    {
        /// <summary>
        /// Implementations must build a fresh tween instance every call.
        /// </summary>
        public abstract TransformTween BuildTween(
            Transform actorTransform,
            BattleSelection selection,
            System.Collections.Generic.IReadOnlyList<CombatantState> targets,
            ActionStepParameters contextParameters);
    }
}
