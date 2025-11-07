namespace BattleV2.AnimationSystem.Strategies
{
    public interface IPhaseStrategy
    {
        void OnEnter(AnimationContext context);
        void OnExit(AnimationContext context);
    }
}
