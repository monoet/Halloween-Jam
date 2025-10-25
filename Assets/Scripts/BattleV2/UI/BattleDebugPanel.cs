using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Orchestration;
using TMPro;
using UnityEngine;

namespace BattleV2.UI
{
    public class BattleDebugPanel : MonoBehaviour
    {
        [SerializeField] private BattleStateController stateController;
        [SerializeField] private BattleManagerV2 battleManager;
        [SerializeField] private CombatantState player;
        [Header("UI Elements")]
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text actionText;
        [SerializeField] private TMP_Text cpText;
        [SerializeField] private TMP_Text spText;
        [SerializeField] private TMP_Text logText;

        private readonly Queue<string> lastLogs = new();

        private void OnEnable()
        {
            if (stateController != null)
            {
                stateController.OnChanged += HandleStateChanged;
                HandleStateChanged(stateController.State);
            }

            BattleLogger.OnLogged += HandleLog;
            UpdateVitals();
        }

        private void OnDisable()
        {
            if (stateController != null)
            {
                stateController.OnChanged -= HandleStateChanged;
            }

            BattleLogger.OnLogged -= HandleLog;
        }

        private void HandleStateChanged(BattleState newState)
        {
            if (stateText != null)
            {
                stateText.text = $"State: {newState}";
            }

            UpdateVitals();
        }

        private void HandleLog(string tag, string message)
        {
            var formatted = $"{tag}: {message}";
            lastLogs.Enqueue(formatted);
            while (lastLogs.Count > 8)
            {
                lastLogs.Dequeue();
            }

            if (logText != null)
            {
                logText.text = string.Join("\n", lastLogs);
            }
        }

        private void UpdateVitals()
        {
            if (player != null)
            {
                if (cpText != null)
                {
                    cpText.text = $"CP {player.CurrentCP}/{player.MaxCP}";
                }

                if (spText != null)
                {
                    spText.text = $"SP {player.CurrentSP}/{player.MaxSP}";
                }
            }

            if (battleManager != null && actionText != null)
            {
                var last = battleManager.LastExecutedAction;
                actionText.text = last != null ? $"Last Action: {last.id}" : "Last Action: (none)";
            }
        }

        private void Update()
        {
            UpdateVitals();
        }
    }
}
