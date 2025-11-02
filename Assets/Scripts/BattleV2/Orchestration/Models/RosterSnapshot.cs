using System;
using System.Collections.Generic;
using UnityEngine;
using HalloweenJam.Combat;
using BattleV2.AnimationSystem.Runtime;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Runtime snapshot of the roster. Acts as the single source of truth for active/ reserve combatants.
    /// TODO: PersistStats y TryRevive hooks.
    /// </summary>
    public readonly struct RosterSnapshot
    {
        public RosterSnapshot(
            CombatantState player,
            CharacterRuntime playerRuntime,
            CombatantState enemy,
            CharacterRuntime enemyRuntime,
            IReadOnlyList<CombatantState> activeAllies,
            IReadOnlyList<CombatantState> enemies,
            IReadOnlyList<GameObject> spawnedPlayerInstances,
            IReadOnlyList<GameObject> spawnedEnemyInstances,
            ScriptableObject enemyDropTable,
            float averageSpeed,
            IReadOnlyList<CombatantState> reserveAllies = null)
        {
            Player = player;
            PlayerRuntime = playerRuntime;
            Enemy = enemy;
            EnemyRuntime = enemyRuntime;
            ActiveAllies = activeAllies ?? Array.Empty<CombatantState>();
            Allies = ActiveAllies;
            ReserveAllies = reserveAllies ?? Array.Empty<CombatantState>();
            Enemies = enemies ?? Array.Empty<CombatantState>();
            SpawnedPlayerInstances = spawnedPlayerInstances ?? Array.Empty<GameObject>();
            SpawnedEnemyInstances = spawnedEnemyInstances ?? Array.Empty<GameObject>();
            EnemyDropTable = enemyDropTable;
            AverageSpeed = averageSpeed;
        }

        public CombatantState Player { get; }
        public CharacterRuntime PlayerRuntime { get; }
        public CombatantState Enemy { get; }
        public CharacterRuntime EnemyRuntime { get; }
        public IReadOnlyList<CombatantState> ActiveAllies { get; }
        public IReadOnlyList<CombatantState> Allies { get; }
        public IReadOnlyList<CombatantState> ReserveAllies { get; }
        public IReadOnlyList<CombatantState> Enemies { get; }
        public IReadOnlyList<GameObject> SpawnedPlayerInstances { get; }
        public IReadOnlyList<GameObject> SpawnedEnemyInstances { get; }
        public ScriptableObject EnemyDropTable { get; }
        public float AverageSpeed { get; }

        public static RosterSnapshot Empty => new RosterSnapshot(
            null,
            null,
            null,
            null,
            Array.Empty<CombatantState>(),
            Array.Empty<CombatantState>(),
            Array.Empty<GameObject>(),
            Array.Empty<GameObject>(),
            null,
            0f,
            Array.Empty<CombatantState>());
    }
}
