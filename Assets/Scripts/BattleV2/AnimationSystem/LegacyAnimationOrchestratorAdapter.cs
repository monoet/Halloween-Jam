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

        public LegacyAnimationOrchestratorAdapter(IBattleAnimOrchestrator legacyOrchestrator)
        {
            legacy = legacyOrchestrator;
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
                request.AverageSpeed);

            // Legacy orchestrator does not support cancellation; best-effort no-op.
            cancellationToken.ThrowIfCancellationRequested();
            return legacy.PlayAsync(playback);
        }
    }
}
