using System;
using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Providers;
using BattleV2.UI.ActionMenu;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Bridges battle action requests to the battle menus (Root, Attack, Magic, Item).
    /// Converts available actions into menu options and routes selections back to the caller.
    /// </summary>
    public class BattleCommandUIPresenter : MonoBehaviour
    {
        [Header("Menu References")]
        [SerializeField] private BattleMenuManager menuManager;
        [SerializeField] private ActionMenuController rootMenu;
        [SerializeField] private AttackMenuController attackMenu;
        [SerializeField] private AttackMenuController magicMenu;
        [SerializeField] private AttackMenuController itemMenu;

        [Header("Root Option Ids")]
        [SerializeField] private string attackOptionId = "Attack";
        [SerializeField] private string magicOptionId = "Magic";
        [SerializeField] private string itemOptionId = "Item";

        private readonly List<ActionMenuOption> attackOptions = new();
        private readonly List<ActionMenuOption> magicOptions = new();
        private readonly List<ActionMenuOption> itemOptions = new();

        private BattleActionContext pendingContext;
        private Action<BattleSelection> onSelected;
        private Action onCancelled;
        private bool awaitingSelection;

        private void Awake()
        {
            if (rootMenu != null)
            {
                rootMenu.OnOptionSelected += HandleRootOptionSelected;
                rootMenu.OnBackRequested += HandleRootBackRequested;
            }

            ConfigureSubmenu(attackMenu);
            ConfigureSubmenu(magicMenu);
            ConfigureSubmenu(itemMenu);
        }

        private void OnDestroy()
        {
            if (rootMenu != null)
            {
                rootMenu.OnOptionSelected -= HandleRootOptionSelected;
                rootMenu.OnBackRequested -= HandleRootBackRequested;
            }

            ClearSubmenu(attackMenu);
            ClearSubmenu(magicMenu);
            ClearSubmenu(itemMenu);
        }

        public void Present(BattleActionContext context, Action<BattleSelection> onSelection, Action onCancel)
        {
            pendingContext = context;
            onSelected = onSelection;
            onCancelled = onCancel;
            awaitingSelection = true;

            BuildOptionLists();
            UpdateRootInteractable();

            if (menuManager != null)
            {
                menuManager.OpenRootMenu();
            }
        }

        public void CancelIfPending()
        {
            if (!awaitingSelection)
            {
                return;
            }

            awaitingSelection = false;
            onCancelled?.Invoke();
            Cleanup();
        }

        private void HandleRootOptionSelected(string optionId)
        {
            if (!awaitingSelection)
            {
                return;
            }

            if (optionId == attackOptionId)
            {
                ShowSubmenu(attackMenu, attackOptions);
            }
            else if (optionId == magicOptionId)
            {
                ShowSubmenu(magicMenu, magicOptions);
            }
            else if (optionId == itemOptionId)
            {
                ShowSubmenu(itemMenu, itemOptions);
            }
            else
            {
                // Non-submenu buttons (e.g., Guard/Flee) can be handled directly via root event consumers.
            }
        }

        private void HandleRootBackRequested()
        {
            CancelIfPending();
        }

        private void ConfigureSubmenu(AttackMenuController controller)
        {
            if (controller == null || controller.Context == null)
            {
                return;
            }

            var ctx = controller.Context;
            ctx.OnOptionSelected = HandleSubmenuSelection;
            ctx.OnBackRequested = () =>
            {
                if (menuManager != null)
                {
                    menuManager.CloseCurrent();
                }
            };
            ctx.OnChargeRequested = HandleChargeRequested;
        }

        private void ClearSubmenu(AttackMenuController controller)
        {
            if (controller == null || controller.Context == null)
            {
                return;
            }

            var ctx = controller.Context;
            ctx.OnOptionSelected = null;
            ctx.OnBackRequested = null;
            ctx.OnChargeRequested = null;
        }

        private void ShowSubmenu(AttackMenuController controller, List<ActionMenuOption> options)
        {
            if (controller == null || options == null || options.Count == 0)
            {
                return;
            }

            controller.ShowOptions(options);
            if (menuManager != null)
            {
                menuManager.Open(controller.gameObject);
            }
        }

        private void HandleSubmenuSelection(ActionMenuOption option)
        {
            if (!awaitingSelection || option.ActionData == null)
            {
                return;
            }

            awaitingSelection = false;
            var selection = new BattleSelection(option.ActionData);

            onSelected?.Invoke(selection);
            Cleanup();
        }

        private void HandleChargeRequested()
        {
            // TODO: integrate charge UI.
        }

        private void BuildOptionLists()
        {
            attackOptions.Clear();
            magicOptions.Clear();
            itemOptions.Clear();

            if (pendingContext == null || pendingContext.AvailableActions == null)
            {
                return;
            }

            var catalog = pendingContext.Context?.Catalog;

            foreach (var action in pendingContext.AvailableActions)
            {
                if (action == null)
                {
                    continue;
                }

                var option = BuildOption(action);

                if (catalog != null)
                {
                    if (catalog.IsMagic(action))
                    {
                        magicOptions.Add(option);
                        continue;
                    }

                    if (catalog.IsItem(action))
                    {
                        itemOptions.Add(option);
                        continue;
                    }

                    attackOptions.Add(option);
                }
                else
                {
                    attackOptions.Add(option);
                }
            }
        }

        private ActionMenuOption BuildOption(BattleActionData action)
        {
            string display = !string.IsNullOrEmpty(action.displayName) ? action.displayName : action.id;
            string description = $"SP {action.costSP} / CP {action.costCP}";
            return new ActionMenuOption(display, description, action);
        }

        private void UpdateRootInteractable()
        {
            if (rootMenu == null)
            {
                return;
            }

            rootMenu.SetOptionInteractable(attackOptionId, attackOptions.Count > 0);
            rootMenu.SetOptionInteractable(magicOptionId, magicOptions.Count > 0);
            rootMenu.SetOptionInteractable(itemOptionId, itemOptions.Count > 0);
        }

        private void Cleanup()
        {
            attackOptions.Clear();
            magicOptions.Clear();
            itemOptions.Clear();

            pendingContext = null;
            onSelected = null;
            onCancelled = null;
            awaitingSelection = false;

            if (menuManager != null)
            {
                menuManager.CloseAll();
            }
        }
    }
}
