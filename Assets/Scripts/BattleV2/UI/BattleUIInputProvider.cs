using System;
using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Execution.TimedHits;
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
            ClearPending(true);

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
                if (uiRoot.StackCount == 0)
                {
                    uiRoot.EnterRoot();
                }
            }
            else
            {
                // Sin UI asignada, no se puede procesar. Cancelar.
                onCancel?.Invoke();
                ClearPending(true);
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
            ClearPending(true);
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
                ClearPending(true);
                return;
            }

            int cp = Mathf.Clamp(pendingCp, 0, Mathf.Max(0, pendingContext.MaxCpCharge));
            ResolveProfiles(pendingContext, action, out var chargeProfile, out var timedProfile);

            var selection = new BattleSelection(action, cp, chargeProfile, timedProfile);
            pendingOnSelected?.Invoke(selection);
            // Do NOT hide UI here. Let the next step (Targeting or Execution) handle UI state.
            ClearPending(false);
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

        private void ClearPending(bool hideUI = true)
        {
            if (uiRoot != null)
            {
                uiRoot.OnAttackChosen -= HandleAttackChosen;
                uiRoot.OnSpellChosen -= HandleSpellChosen;
                uiRoot.OnItemChosen -= HandleItemChosen;
                uiRoot.OnRootActionSelected -= HandleRootActionSelected;
                uiRoot.OnChargeCommitted -= HandleChargeCommitted;
                uiRoot.OnCancel -= HandleCancel;
                
                if (hideUI)
                {
                    uiRoot.HideAll();
                }
            }

            pendingContext = null;
            pendingOnSelected = null;
            pendingOnCancel = null;
            pendingCp = 0;
        }

        private void ResolveProfiles(BattleActionContext context, BattleActionData action, out ChargeProfile chargeProfile, out Ks1TimedHitProfile timedProfile)
        {
            chargeProfile = fallbackChargeProfile;
            timedProfile = null;

            var impl = context?.Context?.Catalog?.Resolve(action);
            if (impl != null)
            {
                if (impl.ChargeProfile != null)
                {
                    chargeProfile = impl.ChargeProfile;
                }

                if (impl is ITimedHitAction timedHitAction)
                {
                    timedProfile = timedHitAction.TimedHitProfile;
                }
            }

            if (chargeProfile == null)
            {
                chargeProfile = fallbackChargeProfile != null ? fallbackChargeProfile : ChargeProfile.CreateRuntimeDefault();
            }
        }
    }
}
