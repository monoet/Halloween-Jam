namespace HalloweenJam.Combat
{
    /// <summary>
    /// Optional contract for combat entities that should award rewards when defeated.
    /// </summary>
    public interface IRewardProvider
    {
        int ExperienceReward { get; }
        int ZReward { get; }
    }
}
