using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Providers
{
    /// <summary>
    /// Computes a tween that rushes the actor toward the spotlight/target while stopping at a configurable distance.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Animation/Tween Providers/RunnerUp To Spotlight")]
    public sealed class RunnerUpToSpotlightProvider : TransformTweenProvider
    {
        private const string LogScope = "Tween/RunnerUpSpotlight";

        [SerializeField, Tooltip("Distance to keep from the target when finishing the run-up.")]
        private float stopDistance = 1.2f;

        [SerializeField, Min(0f), Tooltip("Duration of the tween in seconds.")]
        private float duration = 0.6f;

        [SerializeField, Tooltip("Easing applied to the tween playback.")]
        private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public override TransformTween BuildTween(
            Transform actorTransform,
            BattleSelection selection,
            IReadOnlyList<CombatantState> targets,
            ActionStepParameters contextParameters)
        {
            var tween = TransformTween.None;
            if (actorTransform == null)
            {
                BattleLogger.Warn(LogScope, "RunnerUpToSpotlight executed without actor transform.");
                return tween;
            }

            var targetTransform = ResolveTargetTransform(selection, targets);
            if (targetTransform == null)
            {
                BattleLogger.Warn(LogScope, "RunnerUpToSpotlight missing target transform. Skipping tween.");
                return tween;
            }

            var actorPosition = actorTransform.position;
            var targetPosition = targetTransform.position;
            var direction = targetPosition - actorPosition;
            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                direction.Normalize();
            }
            else
            {
                direction = actorTransform.forward;
            }

            var worldDestination = targetPosition - direction * Mathf.Max(0f, stopDistance);
            var parent = actorTransform.parent;
            var localDestination = parent != null
                ? parent.InverseTransformPoint(worldDestination)
                : worldDestination;

            tween.TargetLocalPosition = localDestination;
            tween.Duration = Mathf.Max(0.01f, duration);
            tween.Easing = easing;
            return tween;
        }

        private static Transform ResolveTargetTransform(BattleSelection selection, IReadOnlyList<CombatantState> targets)
        {
            if (selection.TargetTransform != null)
            {
                return selection.TargetTransform;
            }

            if (targets != null && targets.Count > 0)
            {
                var combatant = targets[0];
                if (combatant != null)
                {
                    return combatant.transform;
                }
            }

            return null;
        }
    }
}
