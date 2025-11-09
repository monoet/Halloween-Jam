using System;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    public enum CombatEventRole
    {
        Any = 0,
        Ally = 1,
        Enemy = 2
    }

    public enum CombatEventDirection
    {
        Any = 0,
        Outgoing = 1,
        Incoming = 2
    }

    public enum CombatEventScope
    {
        CasterOnly = 0,
        TargetOnly = 1,
        Broadcast = 2
    }

    [Serializable]
    public struct CombatEventFilter
    {
        public CombatEventRole role;
        public CombatEventDirection direction;
        public CombatEventScope scope;

        public static CombatEventFilter DefaultCaster =>
            new CombatEventFilter
            {
                role = CombatEventRole.Any,
                direction = CombatEventDirection.Outgoing,
                scope = CombatEventScope.CasterOnly
            };

        public static CombatEventFilter DefaultImpact =>
            new CombatEventFilter
            {
                role = CombatEventRole.Any,
                direction = CombatEventDirection.Incoming,
                scope = CombatEventScope.TargetOnly
            };

        public static CombatEventFilter Broadcast =>
            new CombatEventFilter
            {
                role = CombatEventRole.Any,
                direction = CombatEventDirection.Any,
                scope = CombatEventScope.Broadcast
            };
    }
}
