using System.Collections.Generic;
using BattleV2.Orchestration;
using HalloweenJam.Combat;
using UnityEngine;
using BattleV2.UI;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;

namespace BattleV2.Orchestration.Services
{
    public readonly struct BattleRosterConfig
    {
        public bool AutoSpawnPlayer { get; init; }
        public PlayerLoadout PlayerLoadout { get; init; }
        public Transform PlayerSpawnPoint { get; init; }
        public PlayerPartyLoadout PlayerPartyLoadout { get; init; }
        public Transform[] PlayerSpawnPoints { get; init; }
        public bool AutoSpawnEnemy { get; init; }
        public EnemyLoadout EnemyLoadout { get; init; }
        public Transform EnemySpawnPoint { get; init; }
        public EnemyEncounterLoadout EnemyEncounterLoadout { get; init; }
        public Transform[] EnemySpawnPoints { get; init; }
        public Transform OwnerTransform { get; init; }
        public CombatantState Player { get; init; }
        public CharacterRuntime PlayerRuntime { get; init; }
        public CombatantState Enemy { get; init; }
        public CharacterRuntime EnemyRuntime { get; init; }
        public HUDManager HudManager { get; init; }
    }

    public sealed class CombatantRosterService
    {
        private const int MaxActiveAllies = 4;

        private readonly List<CombatantState> allies = new();
        private readonly List<CombatantState> enemies = new();
        private readonly List<GameObject> spawnedPlayerInstances = new();
        private readonly List<GameObject> spawnedEnemyInstances = new();

        private HUDManager hudManager;
        private BattleRosterConfig config;
        private ScriptableObject enemyDropTable;
        private RosterSnapshot snapshot = RosterSnapshot.Empty;

        public RosterSnapshot Current => snapshot;

        public RosterSnapshot Rebuild(BattleRosterConfig rosterConfig, bool preservePlayerVitals, bool preserveEnemyVitals)
        {
            config = rosterConfig;
            hudManager = rosterConfig.HudManager;

            hudManager?.Clear();
            allies.Clear();
            enemies.Clear();
            enemyDropTable = null;

            if (!config.AutoSpawnPlayer)
            {
                if (config.Player != null)
                {
                    if (TryAddAlly(config.Player))
                    {
                        RegisterWrapperForCombatant(config.Player);
                    }
                }
            }
            else
            {
                DestroyAutoSpawned(spawnedPlayerInstances);
                EnsurePlayerSpawned();
            }

            if (!config.AutoSpawnEnemy)
            {
                if (config.Enemy != null)
                {
                    enemies.Add(config.Enemy);
                    RegisterWrapperForCombatant(config.Enemy);
                }
            }
            else
            {
                DestroyAutoSpawned(spawnedEnemyInstances);
                EnsureEnemySpawned();
            }

            var (player, playerRuntime, enemy, enemyRuntime) = BindCombatants(preservePlayerVitals, preserveEnemyVitals);

            if (!preservePlayerVitals)
            {
                ApplyStartingCp(allies);
            }

            if (!preserveEnemyVitals)
            {
                ApplyStartingCp(enemies);
            }

            hudManager?.RegisterCombatants(allies, isEnemy: false);
            hudManager?.RegisterCombatants(enemies, isEnemy: true);

            float averageSpeed = ComputeAverageSpeed(allies, enemies);

            snapshot = new RosterSnapshot(
                player,
                playerRuntime,
                enemy,
                enemyRuntime,
                allies,
                enemies,
                spawnedPlayerInstances,
                spawnedEnemyInstances,
                enemyDropTable,
                averageSpeed);

            return snapshot;
        }

        public void Cleanup()
        {
            DestroyAutoSpawned(spawnedPlayerInstances);
            DestroyAutoSpawned(spawnedEnemyInstances);
            allies.Clear();
            enemies.Clear();
            enemyDropTable = null;
            hudManager?.Clear();
            snapshot = RosterSnapshot.Empty;
        }

        public RosterSnapshot RefreshAfterDeath(CombatantState combatant)
        {
            if (combatant == null)
            {
                return snapshot;
            }

            bool removed = allies.Remove(combatant);
            if (!removed)
            {
                removed = enemies.Remove(combatant);
            }

            if (!removed)
            {
                return snapshot;
            }

            hudManager?.UnregisterCombatant(combatant);
            RemoveSpawnedInstance(combatant);

            var (player, playerRuntime, enemy, enemyRuntime) = BindCombatants(
                preservePlayerVitals: true,
                preserveEnemyVitals: true);

            float averageSpeed = ComputeAverageSpeed(allies, enemies);

            snapshot = new RosterSnapshot(
                player,
                playerRuntime,
                enemy,
                enemyRuntime,
                allies,
                enemies,
                spawnedPlayerInstances,
                spawnedEnemyInstances,
                enemyDropTable,
                averageSpeed);

            return snapshot;
        }

        private void EnsurePlayerSpawned()
        {
            if (ReachedPartyLimit())
            {
                return;
            }

            var fallbackParent = IsSceneTransform(config.PlayerSpawnPoint)
                ? config.PlayerSpawnPoint
                : config.OwnerTransform;

            if (config.PlayerPartyLoadout != null && config.PlayerPartyLoadout.Members.Count > 0)
            {
                var members = config.PlayerPartyLoadout.Members;
                Vector3[] patternOffsets = ResolvePlayerPatternOffsets(members);
                int formationIndex = 0;

                for (int i = 0; i < members.Count && !ReachedPartyLimit(); i++)
                {
                    var entry = members[i];
                    if (!entry.IsValid)
                    {
                        continue;
                    }

                    Transform spawnTransform = ResolveSpawnTransform(config.PlayerSpawnPoints, config.PlayerSpawnPoint, i);
                    var parent = spawnTransform != null ? spawnTransform : fallbackParent;
                    Vector3 basePosition = parent != null ? parent.position : Vector3.zero;
                    Quaternion baseRotation = parent != null ? parent.rotation : Quaternion.identity;

                    Vector3 offset = entry.SpawnOffset;
                    if (patternOffsets != null && patternOffsets.Length > 0)
                    {
                        offset += patternOffsets[Mathf.Clamp(formationIndex, 0, patternOffsets.Length - 1)];
                    }

                    Vector3 worldPosition = basePosition + offset;
                    Quaternion worldRotation = baseRotation;

                    var combatant = SpawnCombatant(entry, parent, worldPosition, worldRotation, spawnedPlayerInstances, out _);
                    if (combatant != null)
                    {
                        if (TryAddAlly(combatant))
                        {
                            formationIndex++;
                            RegisterWrapperForCombatant(combatant);
                        }
                    }
                }
            }
            else if (config.PlayerLoadout != null && config.PlayerLoadout.IsValid && !ReachedPartyLimit())
            {
                var entry = new CombatantLoadoutEntry(config.PlayerLoadout.PlayerPrefab, config.PlayerLoadout.SpawnOffset);
                var parent = ResolveSpawnTransform(config.PlayerSpawnPoints, config.PlayerSpawnPoint, 0) ?? fallbackParent;

                Vector3 worldPosition = parent.position + entry.SpawnOffset;
                Quaternion worldRotation = parent.rotation;

                var combatant = SpawnCombatant(entry, parent, worldPosition, worldRotation, spawnedPlayerInstances, out _);
                if (combatant != null)
                {
                    if (TryAddAlly(combatant))
                    {
                        RegisterWrapperForCombatant(combatant);
                    }
                }
            }
        }

        private void EnsureEnemySpawned()
        {
            var fallbackParent = IsSceneTransform(config.EnemySpawnPoint)
                ? config.EnemySpawnPoint
                : config.OwnerTransform;

            var encounterLoadout = config.EnemyEncounterLoadout;
            bool useSceneAnchors = encounterLoadout == null || encounterLoadout.UseSceneAnchors;

            IReadOnlyList<CombatantLoadoutEntry> entries = encounterLoadout != null &&
                                                          encounterLoadout.Enemies.Count > 0
                ? config.EnemyEncounterLoadout.Enemies
                : null;

            Vector3[] patternOffsets = null;
            if (encounterLoadout?.SpawnPattern != null && entries != null)
            {
                encounterLoadout.SpawnPattern.TryGetOffsets(entries.Count, out patternOffsets);
            }

            Vector3 fallbackOrigin = fallbackParent != null ? fallbackParent.position : Vector3.zero;
            Quaternion fallbackRotation = fallbackParent != null ? fallbackParent.rotation : Quaternion.identity;

            if (encounterLoadout != null && !useSceneAnchors)
            {
                var owner = config.OwnerTransform;
                var localOffset = encounterLoadout.FallbackOriginOffset;
                fallbackOrigin = owner != null ? owner.TransformPoint(localOffset) : localOffset;
                fallbackRotation = owner != null ? owner.rotation : Quaternion.identity;
            }

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (!entry.IsValid)
                    {
                        continue;
                    }

                    Transform spawnTransform = useSceneAnchors
                        ? ResolveSpawnTransform(config.EnemySpawnPoints, config.EnemySpawnPoint, i)
                        : null;

                    var parent = spawnTransform != null ? spawnTransform : fallbackParent;
                    Vector3 basePosition = spawnTransform != null ? spawnTransform.position : fallbackOrigin;
                    Quaternion baseRotation = spawnTransform != null ? spawnTransform.rotation : fallbackRotation;

                    Vector3 offset = entry.SpawnOffset;
                    if (patternOffsets != null && patternOffsets.Length > 0)
                    {
                        offset += patternOffsets[Mathf.Clamp(i, 0, patternOffsets.Length - 1)];
                    }

                    Vector3 worldPosition = basePosition + offset;
                    Quaternion worldRotation = baseRotation;

                    var combatant = SpawnCombatant(entry, parent, worldPosition, worldRotation, spawnedEnemyInstances, out var dropTable);
                    if (combatant != null)
                    {
                        enemies.Add(combatant);
                        RegisterWrapperForCombatant(combatant);
                        if (enemyDropTable == null && dropTable != null)
                        {
                            enemyDropTable = dropTable;
                        }
                    }
                }
            }
            else if (config.EnemyLoadout != null && config.EnemyLoadout.IsValid)
            {
                var entry = new CombatantLoadoutEntry(config.EnemyLoadout.EnemyPrefab, config.EnemyLoadout.SpawnOffset, config.EnemyLoadout.DropTable);
                var parent = ResolveSpawnTransform(config.EnemySpawnPoints, config.EnemySpawnPoint, 0) ?? fallbackParent;
                Vector3 worldPosition = parent.position + entry.SpawnOffset;
                Quaternion worldRotation = parent.rotation;

                var combatant = SpawnCombatant(entry, parent, worldPosition, worldRotation, spawnedEnemyInstances, out var dropTable);
                if (combatant != null)
                {
                    enemies.Add(combatant);
                    RegisterWrapperForCombatant(combatant);
                    if (dropTable != null)
                    {
                        enemyDropTable = dropTable;
                    }
                }
            }
        }

        private (CombatantState player, CharacterRuntime playerRuntime, CombatantState enemy, CharacterRuntime enemyRuntime) BindCombatants(
            bool preservePlayerVitals,
            bool preserveEnemyVitals)
        {
            CombatantState player = config.Player;
            CharacterRuntime playerRuntime = config.PlayerRuntime;
            CombatantState enemy = config.Enemy;
            CharacterRuntime enemyRuntime = config.EnemyRuntime;

            var boundAllies = new List<CombatantState>(allies.Count);
            for (int i = 0; i < allies.Count; i++)
            {
                var combatant = allies[i];
                if (CombatantBinder.TryBind(combatant, preservePlayerVitals, out var result))
                {
                    boundAllies.Add(result.Combatant);
                    if (i == 0)
                    {
                        player = result.Combatant;
                        playerRuntime = result.Runtime;
                    }
                }
            }

            if (boundAllies.Count == 0 && player != null && CombatantBinder.TryBind(player, preservePlayerVitals, out var playerResult))
            {
                boundAllies.Add(playerResult.Combatant);
                player = playerResult.Combatant;
                playerRuntime = playerResult.Runtime;
            }
            else if (boundAllies.Count == 0)
            {
                playerRuntime = ResolveRuntimeReference(player, playerRuntime);
            }

            allies.Clear();
            allies.AddRange(boundAllies);

            var boundEnemies = new List<CombatantState>(enemies.Count);
            for (int i = 0; i < enemies.Count; i++)
            {
                var combatant = enemies[i];
                if (CombatantBinder.TryBind(combatant, preserveEnemyVitals, out var result))
                {
                    boundEnemies.Add(result.Combatant);
                    if (i == 0)
                    {
                        enemy = result.Combatant;
                        enemyRuntime = result.Runtime;
                    }
                }
            }

            if (boundEnemies.Count == 0 && enemy != null && CombatantBinder.TryBind(enemy, preserveEnemyVitals, out var enemyResult))
            {
                boundEnemies.Add(enemyResult.Combatant);
                enemy = enemyResult.Combatant;
                enemyRuntime = enemyResult.Runtime;
            }
            else if (boundEnemies.Count == 0)
            {
                enemyRuntime = ResolveRuntimeReference(enemy, enemyRuntime);
            }

            enemies.Clear();
            enemies.AddRange(boundEnemies);

            if (allies.Count == 0)
            {
                player = null;
                playerRuntime = null;
            }

            if (enemies.Count == 0)
            {
                enemy = null;
                enemyRuntime = null;
            }

            return (player, playerRuntime, enemy, enemyRuntime);
        }

        private static CharacterRuntime ResolveRuntimeReference(CombatantState combatant, CharacterRuntime overrideRuntime)
        {
            if (overrideRuntime != null)
            {
                return overrideRuntime;
            }

            if (combatant == null)
            {
                return null;
            }

            if (combatant.CharacterRuntime != null)
            {
                return combatant.CharacterRuntime;
            }

            return combatant.GetComponent<CharacterRuntime>();
        }

        private void RemoveSpawnedInstance(CombatantState combatant)
        {
            if (combatant == null)
            {
                return;
            }

            DestroyInstance(spawnedPlayerInstances, combatant.gameObject);
            DestroyInstance(spawnedEnemyInstances, combatant.gameObject);
        }

        private static void DestroyInstance(List<GameObject> instances, GameObject instance)
        {
            if (instances == null || instance == null)
            {
                return;
            }

            for (int i = instances.Count - 1; i >= 0; i--)
            {
                if (instances[i] == instance)
                {
                    instances.RemoveAt(i);
                    if (Application.isPlaying)
                    {
                        Object.Destroy(instance);
                    }
                    else
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
            }
        }

        private static void DestroyAutoSpawned(List<GameObject> instances)
        {
            if (instances == null)
            {
                return;
            }

            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(instance);
                }
                else
                {
                    Object.DestroyImmediate(instance);
                }
            }

            instances.Clear();
        }

        private bool TryAddAlly(CombatantState combatant)
        {
            if (combatant == null || allies.Contains(combatant))
            {
                return false;
            }

            if (ReachedPartyLimit())
            {
                Debug.LogWarning("[CombatantRosterService] Party limit reached (4). Additional allies will be ignored.", combatant);
                return false;
            }

            allies.Add(combatant);
            return true;
        }

        private bool ReachedPartyLimit()
        {
            return allies.Count >= MaxActiveAllies;
        }

        private Vector3[] ResolvePlayerPatternOffsets(IReadOnlyList<CombatantLoadoutEntry> members)
        {
            var pattern = config.PlayerPartyLoadout?.SpawnPattern;
            if (pattern == null || members == null)
            {
                return null;
            }

            int desiredCount = 0;
            for (int i = 0; i < members.Count && desiredCount < MaxActiveAllies; i++)
            {
                if (members[i].IsValid)
                {
                    desiredCount++;
                }
            }

            if (desiredCount == 0)
            {
                return null;
            }

            return pattern.TryGetOffsets(desiredCount, out var offsets) ? offsets : null;
        }

        private void RegisterWrapperForCombatant(CombatantState combatant)
        {
            if (combatant == null)
            {
                return;
            }

            var wrappers = combatant.GetComponentsInChildren<AnimatorWrapper>(true);
            if (wrappers == null || wrappers.Length == 0)
            {
                Debug.LogWarning($"[CombatantRosterService] Combatant '{combatant.name}' is missing an AnimatorWrapper component.", combatant);
                return;
            }

            for (int i = 0; i < wrappers.Length; i++)
            {
                var wrapper = wrappers[i];
                if (wrapper == null)
                {
                    continue;
                }

                wrapper.AssignOwner(combatant);
                AnimatorRegistry.Instance.Register(wrapper);
                wrapper.RegisterAnimationSet();
            }
        }

        private Transform ResolveSpawnTransform(Transform[] points, Transform fallback, int index)
        {
            if (points != null && points.Length > 0)
            {
                int clamped = Mathf.Clamp(index, 0, points.Length - 1);
                Transform candidate = points[clamped];
                if (IsSceneTransform(candidate))
                {
                    return candidate;
                }
            }

            if (IsSceneTransform(fallback))
            {
                return fallback;
            }

            return config.OwnerTransform;
        }

        private static bool IsSceneTransform(Transform target)
        {
            return target != null && target.gameObject.scene.IsValid();
        }

        private CombatantState SpawnCombatant(
            CombatantLoadoutEntry entry,
            Transform parentTransform,
            Vector3 worldPosition,
            Quaternion worldRotation,
            List<GameObject> instanceCollector,
            out ScriptableObject dropTable)
        {
            dropTable = null;

            if (!entry.IsValid)
            {
                return null;
            }

            Transform parent = parentTransform != null ? parentTransform : config.OwnerTransform;
            GameObject instance;
            instance = Object.Instantiate(entry.Prefab, worldPosition, worldRotation, parent);
            instanceCollector?.Add(instance);

            var combatant = instance.GetComponentInChildren<CombatantState>();
            if (combatant == null)
            {
                Debug.LogError($"[CombatantRosterService] Spawned prefab '{entry.Prefab.name}' missing CombatantState. Destroying instance.");
                instanceCollector?.Remove(instance);
                Object.Destroy(instance);
                return null;
            }

            var tweenObserver = combatant.GetComponentInChildren<BattleV2.AnimationSystem.Execution.Runtime.Observers.RecipeTweenObserver>(true);
            tweenObserver?.CaptureHomePosition(force: true);

            AnimationSystemInstaller.Current?.RegisterActor(combatant);

            var resolvedDrop = entry.DropTable;
            if (resolvedDrop == null)
            {
                var metadata = instance.GetComponentInChildren<EnemyMetadata>(true);
                if (metadata != null && metadata.DropTable != null)
                {
                    resolvedDrop = metadata.DropTable;
                }
            }

            if (resolvedDrop == null)
            {
                var runtime = combatant.CharacterRuntime;
                var archetype = runtime != null ? runtime.Archetype : null;
                if (archetype != null && archetype.DropTable != null)
                {
                    resolvedDrop = archetype.DropTable;
                }
            }

            dropTable = resolvedDrop;
            return combatant;
        }

        private static void ApplyStartingCp(IReadOnlyList<CombatantState> combatants)
        {
            if (combatants == null)
            {
                return;
            }

            for (int i = 0; i < combatants.Count; i++)
            {
                var combatant = combatants[i];
                combatant?.ApplyStartingCpFromArchetype();
            }
        }

        private static float ComputeAverageSpeed(IReadOnlyList<CombatantState> allies, IReadOnlyList<CombatantState> enemies)
        {
            float total = 0f;
            int count = 0;

            Accumulate(allies, ref total, ref count);
            Accumulate(enemies, ref total, ref count);

            return count > 0 ? total / count : 1f;

            static void Accumulate(IReadOnlyList<CombatantState> list, ref float total, ref int count)
            {
                if (list == null)
                {
                    return;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    var combatant = list[i];
                    if (combatant == null || !combatant.IsAlive)
                    {
                        continue;
                    }

                    total += combatant.FinalStats.Speed;
                    count++;
                }
            }
        }
    }
}

