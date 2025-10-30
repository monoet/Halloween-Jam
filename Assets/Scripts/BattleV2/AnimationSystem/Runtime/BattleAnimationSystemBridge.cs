using System;
using System.Threading.Tasks;
using BattleV2.Orchestration.Services;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Bridges the legacy battle orchestration interface to the new animation system.
    /// Allows <see cref="BattleManagerV2"/> to call into <see cref="IAnimationOrchestrator"/> without code changes to gameplay flow.
    /// </summary>
    public sealed class BattleAnimationSystemBridge : IBattleAnimOrchestrator
    {
        private readonly IAnimationOrchestrator orchestrator;

        public BattleAnimationSystemBridge(IAnimationOrchestrator orchestrator)
        {
            this.orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        }

        public Task PlayAsync(ActionPlaybackRequest request)
        {
            if (orchestrator == null || request.Actor == null)
            {
                return Task.CompletedTask;
            }

            var animationRequest = new AnimationRequest(
                request.Actor,
                request.Selection,
                request.Targets,
                request.AverageSpeed);

            return orchestrator.PlayAsync(animationRequest);
        }
    }
}

