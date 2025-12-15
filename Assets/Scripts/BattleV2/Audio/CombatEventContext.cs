using UnityEngine;

namespace BattleV2.Audio
{
    /// <summary>
    /// Minimal context for combat audio. 2D-only for MVP.
    /// </summary>
    public readonly struct CombatEventContext
    {
        public readonly WeaponFamily Weapon;
        public readonly ElementId Element;
        public readonly bool IsCrit;
        public readonly int TargetCount;
        public readonly MarkDetonationPayload? MarkPayload;

        public CombatEventContext(
            WeaponFamily weapon,
            ElementId element,
            bool isCrit,
            int targetCount,
            MarkDetonationPayload? markPayload = null)
        {
            Weapon = weapon;
            Element = element;
            IsCrit = isCrit;
            TargetCount = targetCount;
            MarkPayload = markPayload;
        }
    }
}
