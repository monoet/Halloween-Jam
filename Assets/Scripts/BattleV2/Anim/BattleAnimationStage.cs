namespace BattleV2.Anim
{
    /// <summary>
    /// Logical phases of the battle animation pipeline, used to coordinate locks with the battle manager.
    /// </summary>
    public enum BattleAnimationStage
    {
        None = 0,
        PlayerAttack = 1,
        EnemyAttack = 2
    }
}
