namespace BattleV2.Audio
{
    /// <summary>
    /// Weapon family indices must stay aligned with the FMOD parameter "weapon" (0=None, 1=Sword, ...).
    /// </summary>
    public enum WeaponFamily
    {
        None = 0,
        Sword = 1,
        HeavySword = 2,
        Dagger = 3,
        Staff = 4,
        Mace = 5,
        Fist = 6,
        Bow = 7,
        Gun = 8,
        Thrown = 9
    }
}
