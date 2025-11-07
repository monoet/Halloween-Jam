namespace BattleV2.AnimationSystem.Strategies
{
    public interface IAmbientStrategy
    {
        void Start(AmbientSpec spec, AnimationContext context);
        void Stop(AmbientHandle handle, AnimationContext context);
    }
}
