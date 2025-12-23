using System;
using System.Collections.Generic;
using HalloweenJam.Combat.Strategies;
using UnityEngine;

/// <summary>
/// Adapter that exposes the reference CharacterRuntime/CombatantState combo through the ICombatEntity contract.
/// Keeps the existing combat loop untouched while sourcing data from the stats system.
/// </summary>
namespace HalloweenJam.Combat
{
    [DisallowMultipleComponent]
    public sealed class RuntimeCombatEntity : MonoBehaviour, ICombatEntity
    {
        [Header("Stats Runtime")]
        [SerializeField] private CharacterRuntime characterRuntime;
        [SerializeField] private CombatantState combatantState;
        [SerializeField, HideInInspector] private ActionData[] availableActions = Array.Empty<ActionData>();
        [SerializeField, HideInInspector] private string[] battleV2AvailableActionIds = Array.Empty<string>();

        [Header("Combat Behavior")]
        [SerializeField] private AttackStrategyBase attackStrategy;
        [SerializeField] private bool initializeOnAwake = true;

        private readonly IAttackStrategy fallbackStrategy = new FallbackAttackStrategy();
        private bool previousAliveState = true;

        public CharacterRuntime CharacterRuntime => characterRuntime;
        public CombatantState CombatantState => combatantState;
        public IReadOnlyList<ActionData> AvailableActions => availableActions;

        public string DisplayName
        {
            get
            {
                if (characterRuntime != null)
                {
                    string runtimeName = characterRuntime.Core.characterName;
                    if (!string.IsNullOrWhiteSpace(runtimeName))
                    {
                        return runtimeName;
                    }

                    if (characterRuntime.Archetype != null && !string.IsNullOrWhiteSpace(characterRuntime.Archetype.characterName))
                    {
                        return characterRuntime.Archetype.characterName;
                    }
                }

                return gameObject.name;
            }
        }

        public int CurrentHp => combatantState != null ? combatantState.CurrentHP : 0;
        public int MaxHp => combatantState != null ? combatantState.MaxHP : (characterRuntime != null ? Mathf.Max(1, characterRuntime.Final.HP) : 0);
        public int AttackPower => characterRuntime != null ? Mathf.Max(0, Mathf.RoundToInt(characterRuntime.Final.Physical)) : 0;
        public bool IsAlive => combatantState == null || combatantState.IsAlive;

        public IAttackStrategy AttackStrategy => attackStrategy != null ? attackStrategy : fallbackStrategy;

        public event Action<ICombatEntity> OnHealthChanged;
        public event Action<ICombatEntity> OnDefeated;

        private ActionData queuedAction;
        private string lastBattleV2ActionIdsSignature;

        private void Awake()
        {
            ResolveDependencies();

            if (initializeOnAwake)
            {
                InitializeVitals();
            }

            RecomputeAvailableActions("Awake");
        }

        private void OnEnable()
        {
            Subscribe();
            previousAliveState = IsAlive;
            RaiseHealthChanged();
            RecomputeAvailableActions("OnEnable");
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void ReceiveDamage(int amount)
        {
            if (combatantState == null)
            {
                Debug.LogWarning($"[{nameof(RuntimeCombatEntity)}] {name} has no CombatantState to receive damage.");
                return;
            }

            combatantState.TakeDamage(amount);
        }

        public void Heal(int amount)
        {
            if (combatantState == null)
            {
                Debug.LogWarning($"[{nameof(RuntimeCombatEntity)}] {name} has no CombatantState to heal.");
                return;
            }

            combatantState.Heal(amount);
        }

        private void ResolveDependencies()
        {
            if (characterRuntime == null)
            {
                characterRuntime = GetComponent<CharacterRuntime>();
            }

            if (combatantState == null)
            {
                combatantState = GetComponent<CombatantState>();
            }
        }

        private void InitializeVitals()
        {
            if (combatantState != null && characterRuntime != null)
            {
                combatantState.EnsureInitialized(characterRuntime);
            }
        }

        private void Subscribe()
        {
            if (combatantState != null && combatantState.OnVitalsChanged != null)
            {
                combatantState.OnVitalsChanged.AddListener(HandleVitalsChanged);
            }

            if (characterRuntime != null && characterRuntime.OnStatsChanged != null)
            {
                characterRuntime.OnStatsChanged.AddListener(HandleStatsChanged);
            }
        }

        private void Unsubscribe()
        {
            if (combatantState != null && combatantState.OnVitalsChanged != null)
            {
                combatantState.OnVitalsChanged.RemoveListener(HandleVitalsChanged);
            }

            if (characterRuntime != null && characterRuntime.OnStatsChanged != null)
            {
                characterRuntime.OnStatsChanged.RemoveListener(HandleStatsChanged);
            }
        }

        private void HandleVitalsChanged()
        {
            RaiseHealthChanged();
            CheckDefeatedState();
        }

        private void HandleStatsChanged()
        {
            if (combatantState != null && characterRuntime != null)
            {
                combatantState.InitializeFrom(characterRuntime, preserveCurrentFraction: true);
            }

            RaiseHealthChanged();
        }

        private void RaiseHealthChanged()
        {
            OnHealthChanged?.Invoke(this);
        }

        private void CheckDefeatedState()
        {
            bool currentlyAlive = IsAlive;
            if (previousAliveState && !currentlyAlive)
            {
                OnDefeated?.Invoke(this);
            }

            previousAliveState = currentlyAlive;
        }

        public void QueueAction(ActionData action)
        {
            queuedAction = action;
        }

        public ActionData ConsumeQueuedAction()
        {
            var action = queuedAction;
            queuedAction = null;
            return action;
        }

        public void SetAvailableActions(IEnumerable<ActionData> actions)
        {
            if (actions == null)
            {
                availableActions = Array.Empty<ActionData>();
                return;
            }

            availableActions = actions is ActionData[] array ? array : new List<ActionData>(actions).ToArray();
        }

        public IReadOnlyList<string> CurrentAvailableActionIds => battleV2AvailableActionIds;

        public void RecomputeAvailableActions(string reason)
        {
            var ids = combatantState != null ? combatantState.AllowedActionIds : null;
            if (ids == null || ids.Count == 0)
            {
                battleV2AvailableActionIds = Array.Empty<string>();
                lastBattleV2ActionIdsSignature = null;
                return;
            }

            var next = new string[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                next[i] = ids[i];
            }

            battleV2AvailableActionIds = next;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleV2.Core.BattleDiagnostics.DevFlowTrace)
            {
                var signature = string.Join("|", next);
                if (!string.Equals(signature, lastBattleV2ActionIdsSignature, StringComparison.Ordinal))
                {
                    BattleV2.Core.BattleDiagnostics.Log(
                        "BATTLEFLOW",
                        $"AVAILABLE_ACTIONS_UPDATE actor={DisplayName}#{(combatantState != null ? combatantState.GetInstanceID() : 0)} reason={reason} source=CombatantState.AllowedActionIds after=[{signature}]",
                        combatantState != null ? combatantState : this);
                    lastBattleV2ActionIdsSignature = signature;
                }
            }
#endif
        }

        private sealed class FallbackAttackStrategy : IAttackStrategy
        {
            public AttackResult Execute(ICombatEntity attacker, ICombatEntity defender)
            {
                int damage = Mathf.Max(1, attacker.AttackPower);
                return new AttackResult(damage, $"strikes for {damage} damage.");
            }
        }
    }
}
