namespace BattleV2.Execution
{
    /// <summary>
    /// Lightweight reasons for target resolution failure in P2-lite helpers.
    /// </summary>
    public enum TargetResolveFailReason
    {
        Ok = 0,
        NoTargets = 1,
        SelfOnly = 2,
        NotOffensiveSingle = 3,
        InvalidShape = 4,
        NullAction = 5,
        Unknown = 99
    }
}
