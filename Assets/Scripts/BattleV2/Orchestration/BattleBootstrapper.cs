using BattleV2.Core;
using BattleV2.Debugging;
using UnityEngine;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Starts and optionally resets the battle at runtime.
    /// </summary>
    public class BattleBootstrapper : MonoBehaviour
    {
        [SerializeField] private BattleManagerV2 battleManager;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private KeyCode resetKey = KeyCode.R;
        [Header("Debug")]
        [SerializeField] private bool enableCombatantLogs;

        private void Start()
        {
            CombatDebugOptions.EnableCombatantLogs = enableCombatantLogs;

            if (autoStart && battleManager != null)
            {
                BattleLogger.Log("Bootstrap", "Starting battle (auto-start enabled).");
                battleManager.ResetBattle();
                battleManager.StartBattle();
            }
        }

        private void Update()
        {
            if (battleManager == null)
                return;

            if (Input.GetKeyDown(resetKey))
            {
                BattleLogger.Log("Bootstrap", $"Manual reset triggered with {resetKey}.");
                battleManager.ResetBattle();
                battleManager.StartBattle();
            }
        }

        /// <summary>
        /// Public API to reset battle from UI or other scripts.
        /// </summary>
        public void TriggerReset()
        {
            if (battleManager == null)
                return;

            BattleLogger.Log("Bootstrap", "Manual reset triggered (via UI).");
            battleManager.ResetBattle();
            battleManager.StartBattle();
        }
    }
}
