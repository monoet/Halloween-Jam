namespace BattleV2.AnimationSystem.Strategies
{
    internal sealed class NoOpPhaseStrategy : IPhaseStrategy
    {
        public static readonly NoOpPhaseStrategy Instance = new NoOpPhaseStrategy();

        private NoOpPhaseStrategy()
        {
        }

        public void OnEnter(StrategyContext context)
        {
        }

        public void OnExit(StrategyContext context)
        {
        }
    }
}
