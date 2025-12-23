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
        int GetTurnCounter(CombatantState actor);
    }

    public sealed class BattleTurnService : IBattleTurnService
    {
        private readonly ITurnController turnController;
        private readonly IBattleEventBus eventBus;
        private IDisposable actionCompletedSubscription;
        private IReadOnlyList<CombatantState> allies = Array.Empty<CombatantState>();
        private IReadOnlyList<CombatantState> enemies = Array.Empty<CombatantState>();
        private bool active;
        private readonly Dictionary<int, int> turnCounters = new();

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

            // TODO(v2 Validation): Rebuild() during an active battle preserves turn order today
            // because TurnController.Rebuild keeps the current actor stable. If future changes
            // alter that behavior (e.g., index reset/queue rebuild), you'll see turn-order jumps
            // after deaths/spawns. If that ever happens: capture current actor StableId, call
            // Rebuild, then restore current via SetCurrent(actorId) or skip Rebuild while active.
            turnController.Rebuild(allies, enemies);

            if (!active)
            {
                turnController.Reset();
                turnCounters.Clear();
                return;
            }

            // Prune counters for combatants no longer in roster (no reset during active battle).
            var aliveIds = new HashSet<int>();
            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i] != null) aliveIds.Add(allies[i].StableId);
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null) aliveIds.Add(enemies[i].StableId);
            }

            var keys = new List<int>(turnCounters.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                if (!aliveIds.Contains(keys[i]))
                {
                    turnCounters.Remove(keys[i]);
                }
            }
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

        public int GetTurnCounter(CombatantState actor)
        {
            if (actor == null)
            {
                return 0;
            }

            int id = actor.StableId;
            return turnCounters.TryGetValue(id, out var count) ? count : 0;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevCpTrace)
            {
                BattleDiagnostics.Log(
                    "CPTRACE",
                    $"TURN_CLOSE_RECEIVE exec={evt.ExecutionId} actor={evt.Actor?.DisplayName ?? "(null)"}#{(evt.Actor != null ? evt.Actor.GetInstanceID() : 0)} isTriggered={evt.IsTriggered} action={evt.Selection.Action?.id ?? "(null)"} cp={evt.Selection.CpCharge}",
                    evt.Actor);
            }
            if (BattleDiagnostics.DevFlowTrace)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"TURN_CLOSE_RECEIVE exec={evt.ExecutionId} actor={evt.Actor?.DisplayName ?? "(null)"}#{(evt.Actor != null ? evt.Actor.GetInstanceID() : 0)} isTriggered={evt.IsTriggered} action={evt.Selection.Action?.id ?? "(null)"} cp={evt.Selection.CpCharge}",
                    evt.Actor);
            }
#endif
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

            int id = next.StableId;
            turnCounters[id] = turnCounters.TryGetValue(id, out var count) ? count + 1 : 1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevCpTrace)
            {
                BattleDiagnostics.Log(
                    "CPTRACE",
                    $"TURN_ADVANCE next={next.DisplayName}#{next.GetInstanceID()} stableId={next.StableId} turnCount={turnCounters[id]}",
                    next);
            }
            if (BattleDiagnostics.DevFlowTrace)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"TURN_ADVANCE next={next.DisplayName}#{next.GetInstanceID()} stableId={next.StableId} turnCount={turnCounters[id]}",
                    next);
            }
#endif
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
