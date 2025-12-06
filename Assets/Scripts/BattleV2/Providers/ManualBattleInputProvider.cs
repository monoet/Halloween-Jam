using System;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution.TimedHits;
using UnityEngine;
using BattleV2.UI;

namespace BattleV2.Providers
{
    /// <summary>
    /// Temporary manual provider that downgrades to automatic choice until the new UI is implemented.
    /// </summary>
    public class ManualBattleInputProvider : MonoBehaviour, IBattleInputProvider
    {
        [SerializeField] private ChargeProfile defaultChargeProfile;

        [SerializeField] private BattleUIInputDriver driver;

        private void Awake()
        {
            driver = FindObjectOfType<BattleUIInputDriver>();
        }

        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            if (driver == null)
            {
                // Fallback to auto-select if no driver (legacy behavior)
                if (context == null || context.AvailableActions == null || context.AvailableActions.Count == 0)
                {
                    onCancel?.Invoke();
                    return;
                }
                var action = context.AvailableActions[0];
                ResolveProfiles(context, action, out var chargeProfile, out var timedProfile, out var basicProfile, out var runnerKind);
                onSelected?.Invoke(new BattleSelection(action, 0, chargeProfile, timedProfile, basicTimedHitProfile: basicProfile, runnerKind: runnerKind));
                return;
            }

            // Use the driver to show the menu
            driver.ShowMenu(context, (action) =>
            {
                // On Selected
                ResolveProfiles(context, action, out var chargeProfile, out var timedProfile, out var basicProfile, out var runnerKind);
                onSelected?.Invoke(new BattleSelection(action, 0, chargeProfile, timedProfile, basicTimedHitProfile: basicProfile, runnerKind: runnerKind));
            }, 
            () =>
            {
                // On Cancel (Root Back)
                // This signals the Orchestrator that the player cancelled the turn (or wants to skip/defend if we had that).
                // For now, Back at root menu usually means "End Turn" or "Do Nothing" in some games, 
                // but here it might just mean "Stay in Menu" if handled internally, 
                // OR if the Orchestrator expects a result, we might need to signal Cancel.
                
                // However, the Orchestrator's DispatchToInputProvider wraps onCancel with:
                // cpIntent.Cancel("SelectionCanceled"); cpIntent.EndTurn("SelectionCanceled");
                
                // If we want "Back" to NOT end the turn, we shouldn't call onCancel here 
                // UNLESS we really mean to end the turn.
                
                // But RequestAction implies "Give me an action". If we don't give one, what happens?
                // The driver stays in the menu?
                
                // If the driver handles the loop (Menu -> Select -> Back -> Menu), 
                // then RequestAction shouldn't return until a FINAL selection is made.
                
                // So, we only call onSelected when a final selection is made.
                // We do NOT call onCancel for internal "Back" navigation.
                // onCancel should only be called if the player explicitly chooses "End Turn" or "Escape Battle" from the menu.
                
                // If the user presses Back at the root menu, maybe we just do nothing (stay in menu)?
                // The driver.ShowMenu should handle the UI state.
                
                // So, for "Back" from the root menu, we might just log it and NOT invoke onCancel.
                BattleDiagnostics.Log("Provider", "Root Menu Back - Ignoring (Stay in Menu)");
            });
        }

        private void ResolveProfiles(
            BattleActionContext context,
            BattleActionData action,
            out ChargeProfile chargeProfile,
            out Ks1TimedHitProfile timedProfile,
            out BasicTimedHitProfile basicProfile,
            out TimedHitRunnerKind runnerKind)
        {
            var catalog = context?.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;
            chargeProfile = defaultChargeProfile;
            timedProfile = action != null ? action.timedHitProfile : null;
            basicProfile = action != null ? action.basicTimedHitProfile : null;
            runnerKind = action != null ? action.runnerKind : TimedHitRunnerKind.Default;

            if (impl != null)
            {
                if (impl.ChargeProfile != null)
                {
                    chargeProfile = impl.ChargeProfile;
                }

                if (timedProfile == null && impl is ITimedHitAction timedHitAction)
                {
                    timedProfile = timedHitAction.TimedHitProfile;
                }

                if (basicProfile == null && impl is IBasicTimedHitAction basicTimedAction && basicTimedAction.BasicTimedHitProfile != null)
                {
                    basicProfile = basicTimedAction.BasicTimedHitProfile;
                    runnerKind = TimedHitRunnerKind.Basic;
                }
            }

            if (basicProfile != null)
            {
                runnerKind = TimedHitRunnerKind.Basic;
            }

            if (chargeProfile == null)
            {
                chargeProfile = defaultChargeProfile != null
                    ? defaultChargeProfile
                    : ChargeProfile.CreateRuntimeDefault();
            }
        }
    }
}
