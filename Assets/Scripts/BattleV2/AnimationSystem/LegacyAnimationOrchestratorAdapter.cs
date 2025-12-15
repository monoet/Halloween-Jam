using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Orchestration.Services;

namespace BattleV2.AnimationSystem
{
    /// <summary>
    /// Temporary adapter so the new animation contract can delegate
    /// to the legacy orchestrator without touching battle flow.
    /// </summary>
    public sealed class LegacyAnimationOrchestratorAdapter : IAnimationOrchestrator
    {
        private readonly IBattleAnimOrchestrator legacy;
        private BattlePhase currentPhase;

        public LegacyAnimationOrchestratorAdapter(IBattleAnimOrchestrator legacyOrchestrator)
        {
            legacy = legacyOrchestrator;
        }

        public BattlePhase CurrentPhase => currentPhase;

        public BattlePhase GetCurrentPhase(AnimationContext context)
        {
            return currentPhase;
        }

        public void EnterPhase(BattlePhase phase, AnimationContext context)
        {
            currentPhase = phase;
        }

        public AmbientHandle StartAmbient(AmbientSpec spec, AnimationContext context)
        {
            return AmbientHandle.Invalid;
        }

        public void StopAmbient(AmbientHandle handle, AnimationContext context)
        {
        }

        public Task PlayRecipeAsync(string recipeId, AnimationContext context)
        {
            return Task.CompletedTask;
        }

        public Task PlayAsync(AnimationRequest request, CancellationToken cancellationToken = default)
        {
            if (legacy == null)
            {
                return Task.CompletedTask;
            }

            var playback = new ActionPlaybackRequest(
                request.Actor,
                request.Selection,
                request.Targets,
                request.AverageSpeed,
                request.RecipeOverride);

            // Legacy orchestrator does not support cancellation; best-effort no-op.
            cancellationToken.ThrowIfCancellationRequested();
            return legacy.PlayAsync(playback);
        }

        [Obsolete("Use EnterPhase/StartAmbient instead of PlayIntroAsync.")]
        public Task PlayIntroAsync()
        {
            EnterPhase(BattlePhase.Intro, AnimationContext.Default);
            return Task.CompletedTask;
        }

        [Obsolete("Use EnterPhase/StartAmbient instead of PlayLoopAmbientAsync.")]
        public Task PlayLoopAmbientAsync()
        {
            EnterPhase(BattlePhase.Loop, AnimationContext.Default);
            return Task.CompletedTask;
        }

        [Obsolete("Use EnterPhase/PlayRecipeAsync instead of PlayCinematicAsync.")]
        public Task PlayCinematicAsync(string recipeId)
        {
            EnterPhase(BattlePhase.Cinematic, AnimationContext.Default);
            return PlayRecipeAsync(recipeId, AnimationContext.Default);
        }
    }
}
