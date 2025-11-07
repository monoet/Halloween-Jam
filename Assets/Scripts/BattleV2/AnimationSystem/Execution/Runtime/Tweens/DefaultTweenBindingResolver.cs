using System;
using BattleV2.AnimationSystem.Execution.Runtime.Tweens;
using BattleV2.AnimationSystem.Runtime.Bindings;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Tweens
{
    public sealed class DefaultTweenBindingResolver : ITweenBindingResolver
    {
        public bool TryResolve(string tweenId, StepExecutionContext context, out TweenResolveResult result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(tweenId))
            {
                return false;
            }

            var target = ResolveTarget(context);
            if (target == null)
            {
                BattleLogger.Warn("TweenResolver", $"No target transform. actor={(context.Actor?.name ?? "(null)")} wrapper={(context.Wrapper?.GetType().Name ?? "(null)")} tweenId={tweenId}");
                return false;
            }

            if (TryResolveLiteral(tweenId, context, out var tween))
            {
                result = new TweenResolveResult(target, tween);
                BattleLogger.Log("TweenResolver", $"Resolved literal {tweenId}: target={target?.name ?? "(null)"} dur={tween.Duration} pos={tween.TargetLocalPosition}");
                return result.IsValid;
            }

            if (TryResolveProviderTween(tweenId, context, out tween))
            {
                result = new TweenResolveResult(target, tween);
                BattleLogger.Log("TweenResolver", $"Resolved provider {tweenId}: target={target?.name ?? "(null)"} dur={tween.Duration} pos={tween.TargetLocalPosition}");
                return result.IsValid;
            }

            return false;
        }

        private static bool TryResolveLiteral(string tweenId, StepExecutionContext context, out TransformTween tween)
        {
            tween = TransformTween.None;

            if (context.Bindings != null && context.Bindings.TryGetTween(tweenId, out tween) && tween.IsValid)
            {
                return true;
            }

            if (context.Wrapper != null && context.Wrapper.TryGetTween(tweenId, out tween) && tween.IsValid)
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveProviderTween(string tweenId, StepExecutionContext context, out TransformTween tween)
        {
            tween = TransformTween.None;
            var actor = context.Request.Actor;
            if (actor == null)
            {
                return false;
            }

            var provider = actor.GetComponentInChildren<CharacterAnimationSetProvider>(true);
            var set = provider != null ? provider.AnimationSet : null;
            if (set == null)
            {
                return false;
            }

            if (!set.TryGetTweenProvider(tweenId, out var tweenProvider) || tweenProvider == null)
            {
                return false;
            }

            var built = tweenProvider.BuildTween(
                actor.transform,
                context.Request.Selection,
                context.Request.Targets,
                context.Step.Parameters);

            if (!built.IsValid)
            {
                return false;
            }

            tween = built;
            return true;
        }

        private static Transform ResolveTarget(StepExecutionContext context)
        {
            if (context.Actor != null)
            {
                return context.Actor.transform;
            }

            if (context.Wrapper is Component component)
            {
                return component.transform;
            }

            return null;
        }
    }
}
