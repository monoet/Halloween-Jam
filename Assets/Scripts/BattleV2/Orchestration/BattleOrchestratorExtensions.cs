using System;
using BattleV2.Core;
using HalloweenJam.Combat;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Temporary extension bridge until V2 orchestrator is fully integrated.
    /// </summary>
    public static class BattleOrchestratorExtensions
    {
        public static void ResolveEnemyTurn(this BattleOrchestrator orchestrator, CombatantState player, CombatantState enemy, Action onComplete)
        {
            if (orchestrator == null)
            {
                BattleLogger.Warn("Orchestrator", "ResolveEnemyTurn called with null orchestrator. Completing immediately.");
                onComplete?.Invoke();
                return;
            }

            // TODO: Integrate with the actual enemy turn routine once V2 orchestrator is ready.
            BattleLogger.Warn("Orchestrator", "ResolveEnemyTurn stub invoked – integrate with enemy routines.");
            onComplete?.Invoke();
        }
    }
}
