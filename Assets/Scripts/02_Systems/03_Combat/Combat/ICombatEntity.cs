using System;
using HalloweenJam.Combat.Strategies;

namespace HalloweenJam.Combat
{
    /// <summary>
    /// Contract for any combat-ready entity. Allows the battle loop to stay agnostic
    /// of specific implementations (player, enemy, runtime adapters, etc.).
    /// </summary>
    public interface ICombatEntity
    {
        string DisplayName { get; }
        int CurrentHp { get; }
        int MaxHp { get; }
        int AttackPower { get; }
        bool IsAlive { get; }

        event Action<ICombatEntity> OnHealthChanged;
        event Action<ICombatEntity> OnDefeated;

        IAttackStrategy AttackStrategy { get; }

        void ReceiveDamage(int amount);
        void Heal(int amount);
    }
}
