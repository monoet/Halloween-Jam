using UnityEngine;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Temporary helper to start BattleManagerV2 on scene load.
    /// </summary>
    public class BattleBootstrapper : MonoBehaviour
    {
        [SerializeField] private BattleManagerV2 battleManager;
        [SerializeField] private bool autoStart = true;

        private void Start()
        {
            if (!autoStart || battleManager == null)
            {
                return;
            }

            BattleLogger.Log("Bootstrap", "Starting battle (auto-start enabled).");
            battleManager.ResetBattle();
            battleManager.StartBattle();
        }
    }
}
