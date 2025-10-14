using UnityEngine;

namespace HalloweenJam.Combat.Strategies
{
    public readonly struct AttackResult
    {
        public AttackResult(int damage, string description)
        {
            Damage = Mathf.Max(0, damage);
            Description = description;
        }

        public int Damage { get; }
        public string Description { get; }
    }
}
