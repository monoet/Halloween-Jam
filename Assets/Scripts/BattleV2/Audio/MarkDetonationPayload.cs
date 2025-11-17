namespace BattleV2.Audio
{
    /// <summary>
    /// Optional extra for mark detonation SFX variations.
    /// Holds lightweight info; expand later if needed.
    /// </summary>
    public readonly struct MarkDetonationPayload
    {
        public readonly int StackCount;

        public MarkDetonationPayload(int stackCount)
        {
            StackCount = stackCount;
        }
    }
}
