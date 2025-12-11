namespace BattleV2.Core.Services
{
    /// <summary>
    /// Provides an optional starting CP override for a combatant.
    /// -1 means "no override; keep prefab/loadout CP".
    /// </summary>
    public interface IStartingCpSource
    {
        int StartingCpOverride { get; }
    }
}
