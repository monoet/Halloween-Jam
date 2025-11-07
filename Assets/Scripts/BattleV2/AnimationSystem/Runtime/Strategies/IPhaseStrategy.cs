namespace BattleV2.AnimationSystem.Strategies
{
    public interface IPhaseStrategy
    {
        void OnEnter(StrategyContext context);
        void OnExit(StrategyContext context);
    }
}
