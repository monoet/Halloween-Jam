using System;
using HalloweenJam.Combat;
using HalloweenJam.Combat.Animations;

namespace HalloweenJam.Combat.Strategies
{
    /// <summary>
    /// Immutable information bundle passed into a battle turn strategy.
    /// </summary>
    public readonly struct BattleTurnContext
    {
        private readonly Func<ICombatEntity, ICombatEntity, AttackResolutionContext> resolveAttack;

        public BattleTurnContext(
            ICombatEntity attacker,
            ICombatEntity defender,
            IAttackAnimator animator,
            float preAttackDelay,
            Func<bool> isBattleOver,
            Func<ICombatEntity, ICombatEntity, AttackResolutionContext> resolveAttack)
        {
            Attacker = attacker;
            Defender = defender;
            Animator = animator;
            PreAttackDelay = preAttackDelay;
            IsBattleOver = isBattleOver ?? throw new ArgumentNullException(nameof(isBattleOver));
            this.resolveAttack = resolveAttack ?? throw new ArgumentNullException(nameof(resolveAttack));
        }

        public ICombatEntity Attacker { get; }
        public ICombatEntity Defender { get; }
        public IAttackAnimator Animator { get; }
        public float PreAttackDelay { get; }
        public Func<bool> IsBattleOver { get; }

        public AttackResolutionContext ResolveAttack()
        {
            return resolveAttack(Attacker, Defender);
        }
    }
}
