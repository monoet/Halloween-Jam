using System;
using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Bridge entre BattleUIRoot (UGUI) y el BattleManager. Arma BattleSelection con la acción y CP elegidos.
    /// </summary>
    public sealed class BattleUIInputProvider : MonoBehaviour, IBattleInputProvider
    {
        [SerializeField] private BattleUIRoot uiRoot;
        [SerializeField] private ChargeProfile fallbackChargeProfile;

        private BattleActionContext pendingContext;
        private Action<BattleSelection> pendingOnSelected;
        private Action pendingOnCancel;
        private int pendingCp;

        private void Awake()
        {
            uiRoot ??= GetComponent<BattleUIRoot>();
        }

        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            ClearPending();

            if (context == null || context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                onCancel?.Invoke();
                return;
            }

            pendingContext = context;
            pendingOnSelected = onSelected;
            pendingOnCancel = onCancel;
            pendingCp = 0;

            if (uiRoot != null)
            {
                uiRoot.OnAttackChosen += HandleAttackChosen;
                uiRoot.OnSpellChosen += HandleSpellChosen;
                uiRoot.OnItemChosen += HandleItemChosen;
                uiRoot.OnRootActionSelected += HandleRootActionSelected;
                uiRoot.OnChargeCommitted += HandleChargeCommitted;
                uiRoot.OnCancel += HandleCancel;
                uiRoot.EnterRoot();
            }
            else
            {
                // Sin UI asignada, no se puede procesar. Cancelar.
                onCancel?.Invoke();
                ClearPending();
            }
        }

        private void HandleAttackChosen(string actionId)
        {
            ConfirmSelection(actionId);
        }

        private void HandleSpellChosen(string spellId)
        {
            ConfirmSelection(spellId);
        }

        private void HandleItemChosen(string itemId)
        {
            ConfirmSelection(itemId);
        }

        private void HandleRootActionSelected(string category)
        {
            // Útil para acciones sin submenú (Defend, Flee).
            ConfirmSelection(category);
        }

        private void HandleChargeCommitted(int amount)
        {
            pendingCp = Mathf.Max(0, amount);
        }

        private void HandleCancel()
        {
            pendingOnCancel?.Invoke();
            ClearPending();
        }

        private void ConfirmSelection(string actionId)
        {
            if (pendingContext == null || pendingOnSelected == null)
            {
                return;
            }

            var action = FindAction(pendingContext.AvailableActions, actionId);
            if (action == null)
            {
                pendingOnCancel?.Invoke();
                ClearPending();
                return;
            }

            int cp = Mathf.Clamp(pendingCp, 0, Mathf.Max(0, pendingContext.MaxCpCharge));
            var chargeProfile = fallbackChargeProfile != null ? fallbackChargeProfile : ChargeProfile.CreateRuntimeDefault();

            var selection = new BattleSelection(action, cp, chargeProfile, timedHitProfile: null);
            pendingOnSelected?.Invoke(selection);
            ClearPending();
        }

        private static BattleActionData FindAction(IReadOnlyList<BattleActionData> list, string id)
        {
            if (list == null || string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var action = list[i];
                if (action == null)
                {
                    continue;
                }

                if (string.Equals(action.id, id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(action.displayName, id, StringComparison.OrdinalIgnoreCase))
                {
                    return action;
                }
            }

            return null;
        }

        private void ClearPending()
        {
            if (uiRoot != null)
            {
                uiRoot.OnAttackChosen -= HandleAttackChosen;
                uiRoot.OnSpellChosen -= HandleSpellChosen;
                uiRoot.OnItemChosen -= HandleItemChosen;
                uiRoot.OnRootActionSelected -= HandleRootActionSelected;
                uiRoot.OnChargeCommitted -= HandleChargeCommitted;
                uiRoot.OnCancel -= HandleCancel;
                uiRoot.HideAll();
            }

            pendingContext = null;
            pendingOnSelected = null;
            pendingOnCancel = null;
            pendingCp = 0;
        }
    }
}
