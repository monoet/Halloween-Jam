using BattleV2.Core;

namespace BattleV2.Orchestration.Services
{
    public sealed class CombatantReferenceService
    {
        public CombatantReferenceService()
        {
        }

        public CombatantReferenceService(
            CombatantState player,
            CharacterRuntime playerRuntime,
            CombatantState enemy,
            CharacterRuntime enemyRuntime)
        {
            SetPlayer(player, playerRuntime);
            SetEnemy(enemy, enemyRuntime);
        }

        public CombatantState Player { get; private set; }
        public CharacterRuntime PlayerRuntime { get; private set; }
        public CombatantState Enemy { get; private set; }
        public CharacterRuntime EnemyRuntime { get; private set; }

        public void SetPlayer(CombatantState player, CharacterRuntime runtime)
        {
            Player = player;
            PlayerRuntime = runtime;
        }

        public void SetEnemy(CombatantState enemy, CharacterRuntime runtime)
        {
            Enemy = enemy;
            EnemyRuntime = runtime;
        }

        public void Clear(CombatantState combatant)
        {
            if (combatant == null)
            {
                return;
            }

            if (combatant == Player)
            {
                Player = null;
                PlayerRuntime = null;
            }

            if (combatant == Enemy)
            {
                Enemy = null;
                EnemyRuntime = null;
            }
        }

        public void Reset()
        {
            Player = null;
            PlayerRuntime = null;
            Enemy = null;
            EnemyRuntime = null;
        }
    }
}
