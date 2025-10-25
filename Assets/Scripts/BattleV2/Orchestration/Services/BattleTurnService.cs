using System;
using System.Collections.Generic;
using BattleV2.Core;
using BattleV2.Orchestration.Events;

namespace BattleV2.Orchestration.Services
{
    public interface IBattleTurnService : IDisposable
    {
        event Action<CombatantState> OnTurnReady;
        void UpdateRoster(RosterSnapshot snapshot);
        void Begin();
        void Advance(CombatantState actor);
        void Stop();
    }

    public sealed class BattleTurnService : IBattleTurnService
    {
        private readonly ITurnController turnController;
        private readonly IBattleEventBus eventBus;
        private IDisposable actionCompletedSubscription;
        private IReadOnlyList<CombatantState> allies = Array.Empty<CombatantState>();
        private IReadOnlyList<CombatantState> enemies = Array.Empty<CombatantState>();
        private bool active;

        public BattleTurnService(IBattleEventBus eventBus)
            : this(new TurnController(eventBus), eventBus)
        {
        }

        public BattleTurnService(ITurnController turnController, IBattleEventBus eventBus)
        {
            this.turnController = turnController;
            this.eventBus = eventBus;
            actionCompletedSubscription = eventBus.Subscribe<ActionCompletedEvent>(HandleActionCompleted);
        }

        public event Action<CombatantState> OnTurnReady;

        public void UpdateRoster(RosterSnapshot snapshot)
        {
            allies = snapshot.Allies ?? Array.Empty<CombatantState>();
            enemies = snapshot.Enemies ?? Array.Empty<CombatantState>();

            turnController.Rebuild(allies, enemies);
            turnController.Reset();
        }

        public void Begin()
        {
            if (active)
            {
                return;
            }

            active = true;
            turnController.Rebuild(allies, enemies);
            turnController.Reset();
            RequestNextTurn();
        }

        public void Stop()
        {
            active = false;
        }

        public void Advance(CombatantState actor)
        {
            if (!active)
            {
                return;
            }

            turnController.Rebuild(allies, enemies);
            turnController.SetCurrent(actor);
            RequestNextTurn();
        }

        private void HandleActionCompleted(ActionCompletedEvent evt)
        {
            if (!active || evt.IsTriggered)
            {
                return;
            }

            Advance(evt.Actor);
        }

        private void RequestNextTurn()
        {
            if (!active)
            {
                return;
            }

            var next = turnController.Next();
            if (next == null)
            {
                OnTurnReady?.Invoke(null);
                return;
            }

            OnTurnReady?.Invoke(next);
        }

        public void Dispose()
        {
            Stop();
            actionCompletedSubscription?.Dispose();
            actionCompletedSubscription = null;
        }
    }
}
