using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Orchestration;
using TMPro;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Coordinates high-level HUD feedback (state/last action) and forwards CombatantState refs to widgets.
    /// </summary>
    public class BattleHudController : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private BattleStateController stateController;
        [SerializeField] private BattleManagerV2 battleManager;

        [Header("Combatants")]
        [SerializeField] private CombatantState playerState;
        [SerializeField] private CombatantState enemyState;
        [SerializeField] private CombatantHudWidget playerWidget;
        [SerializeField] private CombatantHudWidget enemyWidget;

        [Header("Labels")]
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private TMP_Text lastActionLabel;
        [SerializeField] private string stateFormat = "State: {0}";
        [SerializeField] private string lastActionFormat = "Last Action: {0}";
        [SerializeField] private string noActionText = "(none)";

        private BattleActionData cachedAction;

        private void Awake()
        {
            ApplyWidgets();
            UpdateStateLabel(stateController != null ? stateController.State : BattleState.Idle);
            UpdateLastAction(null);
        }

        private void OnEnable()
        {
            if (stateController != null)
            {
                stateController.OnChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (stateController != null)
            {
                stateController.OnChanged -= HandleStateChanged;
            }
        }

        private void Update()
        {
            if (battleManager == null)
            {
                return;
            }

            var latest = battleManager.LastExecutedAction;
            if (!ReferenceEquals(latest, cachedAction))
            {
                UpdateLastAction(latest);
            }
        }

        public void SetBattleManager(BattleManagerV2 manager)
        {
            battleManager = manager;
            ApplyWidgets();
        }

        public void SetStateController(BattleStateController controller)
        {
            if (stateController != null)
            {
                stateController.OnChanged -= HandleStateChanged;
            }

            stateController = controller;

            if (stateController != null)
            {
                stateController.OnChanged += HandleStateChanged;
                UpdateStateLabel(stateController.State);
            }
        }

        public void SetCombatants(CombatantState player, CombatantState enemy)
        {
            playerState = player;
            enemyState = enemy;
            ApplyWidgets();
        }

        private void ApplyWidgets()
        {
            if (playerWidget != null)
            {
                playerWidget.SetSource(playerState);
            }

            if (enemyWidget != null)
            {
                enemyWidget.SetSource(enemyState);
            }
        }

        private void HandleStateChanged(BattleState newState)
        {
            UpdateStateLabel(newState);
        }

        private void UpdateStateLabel(BattleState state)
        {
            if (stateLabel != null)
            {
                stateLabel.text = string.Format(stateFormat, state);
            }
        }

        private void UpdateLastAction(BattleActionData action)
        {
            cachedAction = action;

            if (lastActionLabel != null)
            {
                string display = action != null
                    ? (!string.IsNullOrWhiteSpace(action.displayName) ? action.displayName : action.id)
                    : noActionText;

                lastActionLabel.text = string.Format(lastActionFormat, display);
            }
        }
    }
}
