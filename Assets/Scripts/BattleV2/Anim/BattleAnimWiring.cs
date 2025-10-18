using UnityEngine;

namespace BattleV2.Anim
{
    /// <summary>
    /// Lightweight helper that keeps both combatant controllers in sync for manual resets or global pause/play.
    /// </summary>
    public class BattleAnimWiring : MonoBehaviour
    {
        [SerializeField] private BattleAnimationController playerController;
        [SerializeField] private BattleAnimationController enemyController;

        private void OnEnable()
        {
            BattleEvents.OnCombatReset += HandleCombatReset;
            ResetControllers();
        }

        private void OnDisable()
        {
            BattleEvents.OnCombatReset -= HandleCombatReset;
            PauseAll();
        }

        public void PauseAll()
        {
            playerController?.PauseAnim();
            enemyController?.PauseAnim();
        }

        public void ResumeAll()
        {
            playerController?.ResumeAnim();
            enemyController?.ResumeAnim();
        }

        private void HandleCombatReset()
        {
            ResetControllers();
        }

        private void ResetControllers()
        {
            playerController?.ResetToIdle();
            enemyController?.ResetToIdle();
        }
    }
}
