using System;
using System.Threading.Tasks;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Plays a sprite flipbook based on a binding entry.
    /// </summary>
    public sealed class FlipbookExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "flipbook";
        private const string LogScope = "AnimStep/Flipbook";

        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step)
        {
            return step.HasBinding;
        }

        public async Task ExecuteAsync(StepExecutionContext context)
        {
            if (!context.Step.HasBinding)
            {
                BattleLogger.Warn(LogScope, $"Step '{context.Step.Id ?? "(no id)"}' missing flipbook binding id.");
                return;
            }

            if (!TryResolveFlipbook(context, context.Step.BindingId, out var binding))
            {
                BattleLogger.Warn(LogScope, $"Flipbook '{context.Step.BindingId}' not found for actor '{context.Actor?.name ?? "(null)"}'.");
                return;
            }

            if (binding.Frames == null || binding.Frames.Length == 0)
            {
                BattleLogger.Warn(LogScope, $"Flipbook '{context.Step.BindingId}' has no frames configured.");
                return;
            }

            var parameters = context.Step.Parameters;
            float frameRate = binding.FrameRate <= 0f ? 12f : binding.FrameRate;
            bool loop = binding.Loop;

            if (parameters.TryGetFloat("frameRate", out var overrideFrameRate))
            {
                frameRate = Mathf.Max(1f, overrideFrameRate);
            }

            if (parameters.TryGetBool("loop", out var loopOverride))
            {
                loop = loopOverride;
            }

            var playback = AnimationPlaybackRequest.ForSpriteFlipbook(binding.Frames, frameRate, loop);

            try
            {
                await context.Wrapper.PlayAsync(playback, context.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the scheduler cancels the token.
            }
        }

        private static bool TryResolveFlipbook(StepExecutionContext context, string flipbookId, out FlipbookBinding binding)
        {
            binding = default;
            if (string.IsNullOrWhiteSpace(flipbookId))
            {
                return false;
            }

            if (context.Bindings != null && context.Bindings.TryGetFlipbook(flipbookId, out binding))
            {
                return true;
            }

            return context.Wrapper != null && context.Wrapper.TryGetFlipbook(flipbookId, out binding);
        }
    }
}
