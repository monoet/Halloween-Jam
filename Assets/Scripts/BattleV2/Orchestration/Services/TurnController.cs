using System.Collections.Generic;
using BattleV2.Core;

namespace BattleV2.Orchestration.Services
{
    public interface ITurnController
    {
        CombatantState Current { get; }
        CombatantState Next();
        void Rebuild(IReadOnlyList<CombatantState> allies, IReadOnlyList<CombatantState> enemies);
    }

    /// <summary>
    /// Placeholder turn order manager. Ordena por velocidad cada vez que se reconstruye.
    /// </summary>
    public sealed class TurnController : ITurnController
    {
        private readonly IBattleEventBus eventBus;
        private readonly List<CombatantState> turnOrder = new();
        private int currentIndex;

        public TurnController(IBattleEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public CombatantState Current => turnOrder.Count == 0 ? null : turnOrder[currentIndex];

        public CombatantState Next()
        {
            if (turnOrder.Count == 0)
            {
                return null;
            }

            currentIndex = (currentIndex + 1) % turnOrder.Count;
            return turnOrder[currentIndex];
        }

        public void Rebuild(IReadOnlyList<CombatantState> allies, IReadOnlyList<CombatantState> enemies)
        {
            turnOrder.Clear();
            currentIndex = 0;

            if (allies != null)
            {
                for (int i = 0; i < allies.Count; i++)
                {
                    if (allies[i] != null && allies[i].IsAlive)
                    {
                        turnOrder.Add(allies[i]);
                    }
                }
            }

            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i] != null && enemies[i].IsAlive)
                    {
                        turnOrder.Add(enemies[i]);
                    }
                }
            }

            turnOrder.Sort((a, b) =>
            {
                if (a == null || b == null)
                {
                    return 0;
                }

                int compare = b.FinalStats.Speed.CompareTo(a.FinalStats.Speed);
                if (compare != 0)
                {
                    return compare;
                }

                return a.GetInstanceID().CompareTo(b.GetInstanceID());
            });
        }
    }
}
