using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleV2.Core;
using BattleV2.Targeting;
using BattleV2.Providers;
using BattleV2.Core.Services;

namespace BattleV2.Orchestration.Services
{
    public sealed class TargetResolutionService
    {
        private readonly ITargetingCoordinator targetingCoordinator;

        public TargetResolutionService(ITargetingCoordinator targetingCoordinator)
        {
            this.targetingCoordinator = targetingCoordinator;
        }

        public async Task<PlayerTargetResolution> ResolveForPlayerAsync(
            CombatantState actor,
            BattleSelection selection,
            CombatContext context,
            IReadOnlyList<CombatantState> allies,
            IReadOnlyList<CombatantState> enemies,
            Func<CombatantState, CharacterRuntime> runtimeResolver)
        {
            if (targetingCoordinator == null || actor == null || selection.Action == null)
            {
                return PlayerTargetResolution.Empty;
            }

            var intent = TargetingIntent.FromAction(selection.Action);
            var resolution = await targetingCoordinator.ResolveAsync(
                actor,
                selection.Action,
                intent,
                TargetSourceType.Manual,
                context?.Enemy,
                allies,
                enemies);

            CombatantState primaryEnemy = null;
            CharacterRuntime primaryRuntime = context?.EnemyRuntime;

            var targets = resolution.Targets;
            if (targets.Count > 0)
            {
                var primaryTarget = targets[0];
                if (primaryTarget != null && Contains(enemies, primaryTarget))
                {
                    primaryEnemy = primaryTarget;
                    primaryRuntime = runtimeResolver != null
                        ? runtimeResolver(primaryTarget)
                        : primaryTarget?.CharacterRuntime;
                }
            }

            return new PlayerTargetResolution(resolution, primaryEnemy, primaryRuntime);
        }

        private static bool Contains(IReadOnlyList<CombatantState> list, CombatantState combatant)
        {
            if (list == null || combatant == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == combatant)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public readonly struct PlayerTargetResolution
    {
        public PlayerTargetResolution(
            TargetResolutionResult result,
            CombatantState primaryEnemy,
            CharacterRuntime primaryEnemyRuntime)
        {
            Result = result;
            PrimaryEnemy = primaryEnemy;
            PrimaryEnemyRuntime = primaryEnemyRuntime;
        }

        public TargetResolutionResult Result { get; }
        public CombatantState PrimaryEnemy { get; }
        public CharacterRuntime PrimaryEnemyRuntime { get; }

        public TargetResolutionStatus Status => Result.Status;
        public bool HasTargets => Result.Targets != null && Result.Targets.Count > 0;

        public static PlayerTargetResolution Empty => new PlayerTargetResolution(
            default,
            null,
            null);
    }
}
