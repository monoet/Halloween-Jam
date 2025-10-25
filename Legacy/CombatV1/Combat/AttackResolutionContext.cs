using HalloweenJam.Combat.Strategies;

namespace HalloweenJam.Combat
{
    /// <summary>
    /// Data returned after resolving a combat attack between two entities.
    /// </summary>
    public readonly struct AttackResolutionContext
    {
        public AttackResolutionContext(ICombatEntity attacker, ICombatEntity defender, AttackResult result, string logMessage)
        {
            Attacker = attacker;
            Defender = defender;
            Result = result;
            LogMessage = logMessage;
        }

        public ICombatEntity Attacker { get; }
        public ICombatEntity Defender { get; }
        public AttackResult Result { get; }
        public string LogMessage { get; }
    }
}

