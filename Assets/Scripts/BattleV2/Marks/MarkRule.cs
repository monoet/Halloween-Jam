using BattleV2.Execution;

namespace BattleV2.Marks
{
    public enum MarkRuleKind
    {
        Apply = 0,
        Detonate = 1
    }

    /// <summary>
    /// Gate/payload definition for applying or detonating a mark.
    /// </summary>
    [System.Serializable]
    public sealed class MarkRule
    {
        public MarkDefinition mark;
        public MarkRuleKind kind = MarkRuleKind.Apply;
        public bool requiresCp;
        public bool requiresTimedSuccess;
        public TimedGrade minTimedGrade = TimedGrade.Success;
        public int cpMin = 0;
        public int cpExact = -1;
        public float chance = 1f;
        public bool consumeOnDetonate = true;
    }
}
