namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    public interface ICombatEventListener
    {
        void OnCombatEventRaised(string flagId, CombatEventContext context);
    }
}
