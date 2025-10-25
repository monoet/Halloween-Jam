using System;
using BattleV2.Core;
using BattleV2.Orchestration.Events;

namespace BattleV2.Orchestration.Services
{
    public interface IBattleAnimOrchestrator
    {
        void StartListening();
        void StopListening();
    }

    /// <summary>
    /// Placeholder that will route ActionStarted/Completed events into animation gates.
    /// </summary>
    public sealed class BattleAnimOrchestrator : IBattleAnimOrchestrator
    {
        private readonly IBattleEventBus eventBus;
        private IDisposable startedSub;
        private IDisposable completedSub;

        public BattleAnimOrchestrator(IBattleEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public void StartListening()
        {
            startedSub = eventBus.Subscribe<ActionStartedEvent>(HandleStarted);
            completedSub = eventBus.Subscribe<ActionCompletedEvent>(HandleCompleted);
        }

        public void StopListening()
        {
            startedSub?.Dispose();
            completedSub?.Dispose();
            startedSub = null;
            completedSub = null;
        }

        private void HandleStarted(ActionStartedEvent evt)
        {
            // TODO: integrate with animation timeline and issue locks.
        }

        private void HandleCompleted(ActionCompletedEvent evt)
        {
            // TODO: release locks and emit stage completion when timeline ends.
        }
    }
}
