using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.Orchestration.Runtime;

namespace BattleV2.AnimationSystem.Execution.Runtime.Tweens
{
    /// <summary>
    /// Resolves tween data for a binding id given the current execution context (actor, selection, parameters).
    /// </summary>
    public interface ITweenBindingResolver
    {
        bool TryResolve(string tweenId, StepExecutionContext context, out TweenResolveResult result);
    }

    /// <summary>
    /// Result returned by <see cref="ITweenBindingResolver"/>. Contains the target transform and the tween payload.
    /// </summary>
    public readonly struct TweenResolveResult
    {
        public TweenResolveResult(Transform targetTransform, TransformTween tween)
        {
            TargetTransform = targetTransform;
            Tween = tween;
        }

        public Transform TargetTransform { get; }
        public TransformTween Tween { get; }

        public bool IsValid => TargetTransform != null && Tween.IsValid;
    }
}
