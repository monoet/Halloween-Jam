using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime.Tweens;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Applies a transform tween defined in bindings to the combatant wrapper.
    /// </summary>
    public sealed class TweenExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "tween";
        private const string LogScope = "AnimStep/Tween";

        private readonly ITweenBindingResolver bindingResolver;
        private readonly ITweenBridge tweenBridge;

        public string Id => ExecutorId;

        public TweenExecutor(ITweenBindingResolver bindingResolver, ITweenBridge tweenBridge)
        {
            this.bindingResolver = bindingResolver ?? throw new ArgumentNullException(nameof(bindingResolver));
            this.tweenBridge = tweenBridge ?? throw new ArgumentNullException(nameof(tweenBridge));
        }

        public bool CanExecute(ActionStep step)
        {
            return step.HasBinding;
        }

        public async Task ExecuteAsync(StepExecutionContext context)
        {
            if (!context.Step.HasBinding)
            {
                BattleLogger.Warn(LogScope, $"Step '{context.Step.Id ?? "(no id)"}' missing tween binding id.");
                return;
            }

            if (!bindingResolver.TryResolve(context.Step.BindingId, context, out var resolved))
            {
                BattleLogger.Warn(LogScope, $"Tween '{context.Step.BindingId}' not found for actor '{context.Actor?.name ?? "(null)"}'.");
                return;
            }

            var tween = ApplyOverrides(resolved.Tween, context.Step.Parameters);
            if (!tween.IsValid)
            {
                BattleLogger.Warn(LogScope, $"Tween '{context.Step.BindingId}' is not valid (missing targets or duration).");
                return;
            }

            var request = new TweenExecutionRequest(resolved.TargetTransform, tween);
            BattleLogger.Log(LogScope,
                $"REQUEST '{context.Step.BindingId}' actor={context.Actor?.name ?? "(null)"} target={resolved.TargetTransform?.name ?? "(null)"} dur={tween.Duration} pos={tween.TargetLocalPosition}");

            await tweenBridge.PlayAsync(request, context.CancellationToken);

            BattleLogger.Log(LogScope,
                $"EXECUTED '{context.Step.BindingId}' actor={context.Actor?.name ?? "(null)"} target={resolved.TargetTransform?.name ?? "(null)"} dur={tween.Duration} pos={tween.TargetLocalPosition}");
        }

        private static TransformTween ApplyOverrides(TransformTween tween, ActionStepParameters parameters)
        {
            if (parameters.TryGetFloat("duration", out var duration) && duration > 0f)
            {
                tween.Duration = duration;
            }

            if (parameters.TryGetFloat("posX", out var posX) ||
                parameters.TryGetFloat("posY", out var posY) ||
                parameters.TryGetFloat("posZ", out var posZ))
            {
                var current = tween.TargetLocalPosition ?? Vector3.zero;
                float x = parameters.TryGetFloat("posX", out posX) ? posX : current.x;
                float y = parameters.TryGetFloat("posY", out posY) ? posY : current.y;
                float z = parameters.TryGetFloat("posZ", out posZ) ? posZ : current.z;
                tween.TargetLocalPosition = new Vector3(x, y, z);
            }

            if (parameters.TryGetFloat("scaleX", out var scaleX) ||
                parameters.TryGetFloat("scaleY", out var scaleY) ||
                parameters.TryGetFloat("scaleZ", out var scaleZ))
            {
                var current = tween.TargetLocalScale ?? Vector3.one;
                float x = parameters.TryGetFloat("scaleX", out scaleX) ? scaleX : current.x;
                float y = parameters.TryGetFloat("scaleY", out scaleY) ? scaleY : current.y;
                float z = parameters.TryGetFloat("scaleZ", out scaleZ) ? scaleZ : current.z;
                tween.TargetLocalScale = new Vector3(x, y, z);
            }

            if (parameters.TryGetFloat("rotX", out var rotX) ||
                parameters.TryGetFloat("rotY", out var rotY) ||
                parameters.TryGetFloat("rotZ", out var rotZ))
            {
                var current = tween.TargetLocalRotation ?? Quaternion.identity;
                float x = parameters.TryGetFloat("rotX", out rotX) ? rotX : current.eulerAngles.x;
                float y = parameters.TryGetFloat("rotY", out rotY) ? rotY : current.eulerAngles.y;
                float z = parameters.TryGetFloat("rotZ", out rotZ) ? rotZ : current.eulerAngles.z;
                tween.TargetLocalRotation = Quaternion.Euler(x, y, z);
            }

            return tween;
        }

    }
}
