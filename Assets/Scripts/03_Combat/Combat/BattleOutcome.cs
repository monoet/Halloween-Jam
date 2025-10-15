namespace HalloweenJam.Combat
{
    public enum BattleVictory
    {
        PlayerVictory,
        PlayerDefeat,
        Unknown
    }

    public readonly struct BattleOutcome
    {
        public BattleOutcome(BattleVictory victory, ICombatEntity winner, ICombatEntity defeated)
        {
            Victory = victory;
            Winner = winner;
            Defeated = defeated;
        }

        public BattleVictory Victory { get; }
        public ICombatEntity Winner { get; }
        public ICombatEntity Defeated { get; }

        public bool PlayerWon => Victory == BattleVictory.PlayerVictory;
        public bool PlayerLost => Victory == BattleVictory.PlayerDefeat;
    }
}

