using BattleV2.Execution;

namespace BattleV2.Marks
{
    /// <summary>
    /// Gate/payload definition for applying or detonating a mark.
    /// </summary>
    [System.Serializable]
    public sealed class MarkRule
    {
        public MarkDefinition mark;
        public bool requiresCp;
        public bool requiresTimedSuccess;
        public TimedGrade minTimedGrade = TimedGrade.Success;
        public float chance = 1f;
        public bool consumeOnDetonate = true;
    }
}
