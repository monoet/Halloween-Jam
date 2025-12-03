using System.Threading.Tasks;
using BattleV2.Targeting;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Interactor para selección de objetivos. Soporta panel de selección y modo virtual (highlight/ciclo) si el panel no está asignado.
    /// </summary>
    public sealed class BattleUITargetInteractor : MonoBehaviour, ITargetSelectionInteractor
    {
        [SerializeField] private TargetSelectionPanel targetPanel;
        [SerializeField] private HUDManager hudManager;
        [SerializeField] private BattleUIRoot uiRoot;
        [SerializeField] private BattleUIInputDriver inputDriver;

        private TaskCompletionSource<TargetSet> pendingTcs;
        private TargetSet proposed;
        private CombatantState[] lastCandidates = System.Array.Empty<CombatantState>();
        private int currentIndex;

        private void Awake()
        {
            targetPanel ??= GetComponent<TargetSelectionPanel>();
            hudManager ??= FindFirstObjectByType<HUDManager>();
            uiRoot ??= FindFirstObjectByType<BattleUIRoot>();
            inputDriver ??= FindFirstObjectByType<BattleUIInputDriver>();
        }

        private void Start()
        {
            var manager = FindFirstObjectByType<BattleV2.Orchestration.BattleManagerV2>();
            if (manager != null)
            {
                manager.SetTargetSelectionInteractor(this);
            }
            else
            {
                Debug.LogWarning("[BattleUITargetInteractor] BattleManagerV2 not found. Manual targeting will not work.");
            }

            if (inputDriver == null)
            {
                inputDriver = FindFirstObjectByType<BattleUIInputDriver>();
            }
        }



        public Task<TargetSet> SelectAsync(TargetContext context, TargetSet proposedSet)
        {
            if (pendingTcs != null)
            {
                return pendingTcs.Task;
            }

            BattleDiagnostics.Log("Targeting", "Manual Selection Started", this);
            proposed = proposedSet;
            CacheCandidates(context);
            
            pendingTcs = new TaskCompletionSource<TargetSet>();

            if (targetPanel == null)
            {
                // Modo virtual (sin panel): usar el driver/inputs para ciclar y confirmar
                BattleDiagnostics.Log("Targeting", "Virtual targeting (no panel)", this);
                
                if (inputDriver != null)
                {
                    Debug.Log("[BattleUITargetInteractor] Virtual Mode: Setting InputDriver state to TargetSelectionState(true)");
                    inputDriver.SetState(new TargetSelectionState(true));
                    inputDriver.OnNavigate += HandleNavigate;
                }
                
                if (lastCandidates.Length > 0)
                {
                    currentIndex = 0;
                    HighlightSingle(TargetSet.Single(lastCandidates[0].GetInstanceID()));
                }
            }
            else
            {
                if (inputDriver != null)
                {
                    Debug.Log("[BattleUITargetInteractor] Setting InputDriver state to TargetSelectionState");
                    inputDriver.SetState(new TargetSelectionState());
                }
                else
                {
                    Debug.LogError("[BattleUITargetInteractor] InputDriver is NULL! Cannot set state.");
                }

                if (uiRoot != null)
                {
                    uiRoot.EnterTarget();
                }
                else
                {
                    targetPanel.gameObject.SetActive(true);
                }

                targetPanel.OnTargetSelected += HandleSelected;
                targetPanel.OnCancelRequested += HandleCancel;
            }

            if (uiRoot != null)
            {
                uiRoot.OnTargetCancel += HandleCancel;
                uiRoot.OnTargetConfirmed += HandleConfirm;
            }

            return pendingTcs.Task;
        }

        private void HandleNavigate(Vector2 direction)
        {
            if (pendingTcs == null || targetPanel != null) return; // Only for virtual mode
            if (lastCandidates.Length == 0) return;

            if (direction.x > 0) CycleTarget(1);
            else if (direction.x < 0) CycleTarget(-1);
        }

        private void HandleConfirm()
        {
            if (pendingTcs == null) return;
            
            // If virtual mode, confirm current index
            if (targetPanel == null && lastCandidates.Length > 0)
            {
                HandleSelected(lastCandidates[currentIndex].GetInstanceID());
            }
            // If panel mode, the panel handles selection via OnTargetSelected, 
            // but if we want to support "Confirm" button on gamepad triggering the current selection in panel:
            // We might need to know what the panel has selected. 
            // For now, let's assume panel handles its own confirmation via UI buttons.
            // But wait, TargetSelectionState calls ConfirmTarget() on Enter/Space/Z.
            // So we MUST handle it here for panel mode too if we want keyboard support for panel buttons.
            // However, Unity UI usually handles Enter on selected button.
            // If we are using Unity UI navigation, the button OnClick fires.
            // So we might not need to do anything here for panel mode, 
            // UNLESS the user presses a button that isn't "Submit" but we mapped it to confirm?
            // But TargetSelectionState uses "AllowConfirm" which checks ConfirmKeys.
            // If Unity UI handles it, we might double confirm?
            // TargetSelectionState calls ExecuteEvents.submitHandler in MenuState, but in TargetSelectionState it calls ConfirmTarget().
            // So we should probably NOT call ConfirmTarget() if we want Unity UI to handle it?
            // Actually, TargetSelectionState in my previous edit calls ConfirmTarget().
            // If I want Unity UI to work, I should probably let Unity UI handle it OR manually invoke the selected button.
            // Let's stick to Virtual Mode support for now in HandleConfirm.
            // If Panel Mode needs it, we can add it later.
        }

        private void ResolveAndClear(TargetSet result)
        {
            if (pendingTcs != null)
            {
                pendingTcs.TrySetResult(result);
                pendingTcs = null;
            }

            if (targetPanel != null)
            {
                targetPanel.OnTargetSelected -= HandleSelected;
                targetPanel.OnCancelRequested -= HandleCancel;
                targetPanel.gameObject.SetActive(false);
            }

            if (uiRoot != null)
            {
                uiRoot.OnTargetCancel -= HandleCancel;
                uiRoot.OnTargetConfirmed -= HandleConfirm;
            }

            if (inputDriver != null)
            {
                inputDriver.OnNavigate -= HandleNavigate;
            }

            ClearAllHighlights();
        }

        private void CycleTarget(int direction)
        {
            currentIndex = (currentIndex + direction + lastCandidates.Length) % lastCandidates.Length;
            var target = lastCandidates[currentIndex];
            HighlightSingle(TargetSet.Single(target.GetInstanceID()));
            BattleDiagnostics.Log("Targeting", $"Virtual Cycle -> {target.name}", this);
            inputDriver?.PlayNavigateAudio();
        }

        private void HandleSelected(int targetId)
        {


            BattleDiagnostics.Log("Targeting", $"Target Selected: {targetId}", this);
            ClearAllHighlights();
            ResolveAndClear(TargetSet.Single(targetId));
        }

        private void HandleCancel()
        {
            BattleDiagnostics.Log("Targeting", "Selection Cancelled (Back)", this);
            ClearAllHighlights();
            
            // Resolve with Back set to signal return to previous menu
            ResolveAndClear(TargetSet.Back);

            // Regresar al estado de menú si el driver existe
            // NOTE: With the new refactor, the Provider/Manager handles the state transition based on the result.
            // We remove the direct state change here to avoid double-pushing state or flickers.
            // inputDriver?.SetState(new MenuState());
        }



        private void CacheCandidates(TargetContext context)
        {
            var audience = ResolveAudience(context);
            if (audience == null || audience.Count == 0)
            {
                lastCandidates = System.Array.Empty<CombatantState>();
                return;
            }

            var list = new System.Collections.Generic.List<CombatantState>(audience.Count);
            for (int i = 0; i < audience.Count; i++)
            {
                var c = audience[i];
                if (c != null && c.IsAlive)
                {
                    list.Add(c);
                }
            }

            lastCandidates = list.ToArray();
            HighlightSingle(proposed);
        }

        private System.Collections.Generic.IReadOnlyList<CombatantState> ResolveAudience(TargetContext context)
        {
            return context.Query.Audience switch
            {
                TargetAudience.Allies => context.Allies,
                TargetAudience.Self => context.Origin != null ? new[] { context.Origin } : null,
                _ => context.Enemies
            };
        }

        private void HighlightSingle(TargetSet set)
        {
            ClearAllHighlights();

            if (set.TryGetSingle(out var id))
            {
                for (int i = 0; i < lastCandidates.Length; i++)
                {
                    var c = lastCandidates[i];
                    if (c != null && c.GetInstanceID() == id)
                    {
                        TrySetHighlight(c, true);
                        break;
                    }
                }
            }
        }

        private void ClearAllHighlights()
        {
            if (lastCandidates == null || lastCandidates.Length == 0)
            {
                return;
            }

            for (int i = 0; i < lastCandidates.Length; i++)
            {
                var c = lastCandidates[i];
                if (c != null)
                {
                    TrySetHighlight(c, false);
                }
            }
        }

        private void TrySetHighlight(CombatantState combatant, bool highlighted)
        {
            if (hudManager == null || combatant == null)
            {
                return;
            }

            if (hudManager.TryGetWidget(combatant, out var widget) && widget != null)
            {
                widget.SetHighlighted(highlighted);
            }
        }
    }
}
