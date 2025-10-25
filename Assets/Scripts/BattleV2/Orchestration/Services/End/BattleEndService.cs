using System;
using BattleV2.Core;
using BattleV2.Orchestration.Events;

namespace BattleV2.Orchestration.Services
{
    public interface IBattleEndService
    {
        event Action<BattleResult> OnBattleEnded;
        bool TryResolve(RosterSnapshot roster, CombatantState player, BattleStateController stateController);
    }

    public enum BattleResult
    {
        Victory,
        Defeat
        // TODO: Empate/Retreat si se requiere más adelante.
    }

    public sealed class BattleEndService : IBattleEndService
    {
        private readonly IBattleEventBus eventBus;

        public BattleEndService(IBattleEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public event Action<BattleResult> OnBattleEnded;

        public bool TryResolve(RosterSnapshot roster, CombatantState player, BattleStateController stateController)
        {
            if (player == null || player.IsDead())
            {
                stateController?.Set(BattleState.Defeat);
                Publish(BattleResult.Defeat);
                return true;
            }

            var enemies = roster.Enemies;
            if (enemies == null || enemies.Count == 0)
            {
                stateController?.Set(BattleState.Victory);
                Publish(BattleResult.Victory);
                return true;
            }

            bool enemyAlive = false;
            for (int i = 0; i < enemies.Count; i++)
            {
                var combatant = enemies[i];
                if (combatant != null && combatant.IsAlive)
                {
                    enemyAlive = true;
                    break;
                }
            }

            if (!enemyAlive)
            {
                stateController?.Set(BattleState.Victory);
                Publish(BattleResult.Victory);
                return true;
            }

            return false;
        }

        private void Publish(BattleResult result)
        {
            eventBus?.Publish(result);
            OnBattleEnded?.Invoke(result);
        }
    }
}
