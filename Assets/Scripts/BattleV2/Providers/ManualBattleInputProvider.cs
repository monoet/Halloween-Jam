using System;
using BattleV2.Actions;
using BattleV2.Core;
using HalloweenJam.UI.Combat;
using UnityEngine;

namespace BattleV2.Providers
{
    /// <summary>
    /// Manual provider that drives the legacy BattleActionMenu + ActionSelectionUI.
    /// </summary>
    public class ManualBattleInputProvider : MonoBehaviour, IBattleInputProvider
    {
        [SerializeField] private BattleActionMenu actionMenu;
        [SerializeField] private ActionSelectionUI selectionUI;

        private Action<ActionData> onSelected;
        private Action onCancel;
        private BattleActionContext currentContext;

        private void Awake()
        {
            if (actionMenu == null)
            {
                actionMenu = GetComponentInChildren<BattleActionMenu>(true);
            }

            if (selectionUI == null)
            {
                selectionUI = GetComponentInChildren<ActionSelectionUI>(true);
            }

            if (actionMenu == null || selectionUI == null)
            {
                BattleLogger.Warn("Provider", "Manual provider missing UI references. It will degrade to auto.");
            }
        }

        public void RequestAction(BattleActionContext context, Action<ActionData> onSelected, Action onCancel)
        {
            if (context == null || context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                BattleLogger.Warn("Provider", "Manual provider received no actions. Cancelling.");
                onCancel?.Invoke();
                return;
            }

            if (actionMenu == null || selectionUI == null)
            {
                BattleLogger.Warn("Provider", "UI missing, degrading to auto.");
                onSelected?.Invoke(context.AvailableActions[0]);
                return;
            }

            this.onSelected = onSelected;
            this.onCancel = onCancel;
            currentContext = context;

            BattleLogger.Log("Provider", $"Awaiting player selection for {context.Player.name}");

            actionMenu.Show(MenuAttackConfirmed, HandleMenuCancelled);
        }

        private void MenuAttackConfirmed()
        {
            if (currentContext == null)
            {
                BattleLogger.Warn("Provider", "MenuAttackConfirmed called without context.");
                onCancel?.Invoke();
                return;
            }

            selectionUI.Show(currentContext.Player, action =>
            {
                if (action == null)
                {
                    BattleLogger.Warn("Selection", "Player cancelled selection.");
                    actionMenu.HideMenu();
                    onCancel?.Invoke();
                    return;
                }

                BattleLogger.Log("Selection", $"{action.id} chosen by player.");
                actionMenu.HideMenu();
                onSelected?.Invoke(action);
            });
        }

        private void HandleMenuCancelled()
        {
            BattleLogger.Log("Provider", "Player closed the base action menu.");
            actionMenu.HideMenu();
            onCancel?.Invoke();
        }
    }
}
