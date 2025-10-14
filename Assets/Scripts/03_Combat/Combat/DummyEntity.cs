using System;
using HalloweenJam.Combat.Strategies;
using UnityEngine;

namespace HalloweenJam.Combat
{
    [DisallowMultipleComponent]
    public class DummyEntity : MonoBehaviour, ICombatEntity, IRewardProvider
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Entity";

        [Header("Stats")]
        [SerializeField] private int maxHp = 20;
        [SerializeField] private int attackPower = 5;

        [Header("Behavior")]
        [SerializeField] private bool resetHpOnEnable = true;
        [SerializeField] private AttackStrategyBase attackStrategy;
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Rewards")]
        [SerializeField] private int experienceReward = 25;
        [SerializeField] private int zReward = 10;

        private readonly IAttackStrategy fallbackStrategy = new FallbackAttackStrategy();
        private int currentHp;

        public string DisplayName => displayName;
        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public int AttackPower => attackPower;
        public bool IsAlive => currentHp > 0;
        public IAttackStrategy AttackStrategy => attackStrategy != null ? attackStrategy : fallbackStrategy;
        public int ExperienceReward => Mathf.Max(0, experienceReward);
        public int ZReward => Mathf.Max(0, zReward);

        public event Action<ICombatEntity> OnHealthChanged;
        public event Action<ICombatEntity> OnDefeated;

        private void Awake()
        {
            ClampValues();
            if (currentHp == 0 || currentHp > maxHp)
            {
                currentHp = maxHp;
            }
            DebugLog("Awake: {0} initialized with HP {1}/{2}, ATK {3}.", displayName, currentHp, maxHp, attackPower);
        }

        private void OnEnable()
        {
            if (!resetHpOnEnable)
            {
                DebugLog("OnEnable: {0} keeps HP at {1}/{2}.", displayName, currentHp, maxHp);
                return;
            }

            currentHp = maxHp;
            RaiseHealthChanged();
            DebugLog("OnEnable: {0} reset HP to {1}/{2}.", displayName, currentHp, maxHp);
        }

        private void OnValidate()
        {
            ClampValues();

            if (!Application.isPlaying)
            {
                if (resetHpOnEnable)
                {
                    currentHp = maxHp;
                }
                else
                {
                    currentHp = Mathf.Clamp(currentHp, 0, maxHp);
                }
            }
        }

        public void ReceiveDamage(int amount)
        {
            if (amount <= 0 || !IsAlive)
            {
                DebugLog("ReceiveDamage ignored on {0}: amount={1}, IsAlive={2}.", displayName, amount, IsAlive);
                return;
            }

            currentHp = Mathf.Max(0, currentHp - amount);
            RaiseHealthChanged();
            DebugLog("ReceiveDamage: {0} took {1}, HP now {2}/{3}.", displayName, amount, currentHp, maxHp);

            if (currentHp == 0)
            {
                OnDefeated?.Invoke(this);
                DebugLog("ReceiveDamage: {0} defeated.", displayName);
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || currentHp >= maxHp)
            {
                DebugLog("Heal ignored on {0}: amount={1}, currentHp={2}.", displayName, amount, currentHp);
                return;
            }

            currentHp = Mathf.Min(maxHp, currentHp + amount);
            RaiseHealthChanged();
            DebugLog("Heal: {0} healed {1}, HP now {2}/{3}.", displayName, amount, currentHp, maxHp);
        }

        private void RaiseHealthChanged()
        {
            OnHealthChanged?.Invoke(this);
            DebugLog("RaiseHealthChanged: {0} notifying listeners.", displayName);
        }

        private void ClampValues()
        {
            maxHp = Mathf.Max(1, maxHp);
            attackPower = Mathf.Max(0, attackPower);
        }

        private void DebugLog(string message, params object[] args)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            if (args != null && args.Length > 0)
            {
                Debug.LogFormat("[DummyEntity] " + message, args);
            }
            else
            {
                Debug.Log("[DummyEntity] " + message);
            }
        }

        private sealed class FallbackAttackStrategy : IAttackStrategy
        {
            public AttackResult Execute(ICombatEntity attacker, ICombatEntity defender)
            {
                var damage = Mathf.Max(1, attacker.AttackPower);
                return new AttackResult(damage, $"strikes for {damage} damage.");
            }
        }
    }
}
