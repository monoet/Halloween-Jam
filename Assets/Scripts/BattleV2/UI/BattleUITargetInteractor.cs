using System;
using System.Collections.Generic;
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

        private int sessionCounter;
        private int currentSessionId;
        private TaskCompletionSource<TargetSet> pendingTcs;
        private TargetSet proposed;
        private CombatantState[] lastCandidates = System.Array.Empty<CombatantState>();
        private int currentIndex;
        private bool confirmInFlight;
        private bool virtualSubscribed;

        private void LogB1(string phase, string details)
        {
            BattleDiagnostics.Log("PAE.BUITI", $"b=1 phase={phase} frame={Time.frameCount} {details}", this);
        }

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
            // Auto-confirm group/All sets (no manual selection path).
            if ((proposedSet.IsGroup && proposedSet.Ids != null && proposedSet.Ids.Count > 1) ||
                context.Query.Shape == TargetShape.All)
            {
                BattleDiagnostics.Log("Targeting", "Auto-confirming multi-id/All TargetSet; skipping manual UI.", this);
                return Task.FromResult(proposedSet.IsEmpty ? TargetSet.None : proposedSet);
            }

            if (pendingTcs != null)
            {
                // Evita quedar en limbo si la selección anterior no se limpió
                BattleDiagnostics.Log("Targeting", "SelectAsync called while pendingTcs still alive. Forcing cancel/back.", this);
                pendingTcs.TrySetResult(TargetSet.Back);
                pendingTcs = null;
            }

            currentSessionId = ++sessionCounter;
            BattleDiagnostics.Log("Targeting", "Manual Selection Started", this);
            proposed = proposedSet;
            CacheCandidates(context);
            LogB1("BUITI.Start", $"sessionId={currentSessionId} mode={(targetPanel == null ? "virtual" : "panel")} audience={context.Query.Audience} shape={context.Query.Shape} proposed={(proposedSet.IsGroup ? "group" : (proposedSet.IsEmpty ? "empty" : "single"))} candidates={lastCandidates.Length}");
            
            pendingTcs = new TaskCompletionSource<TargetSet>();
            confirmInFlight = false;

            if (targetPanel == null)
            {
                // Modo virtual (sin panel): usar el driver/inputs para ciclar y confirmar
                BattleDiagnostics.Log("Targeting", "Virtual targeting (no panel)", this);
                
                if (inputDriver != null)
                {
                    Debug.Log("[BattleUITargetInteractor] Virtual Mode: Setting InputDriver state to TargetSelectionState(true)");
                    inputDriver.SetState(new TargetSelectionState(true));
                    SubscribeVirtual();
                }
                
                if (lastCandidates.Length > 0)
                {
                    currentIndex = 0;
                    HighlightSet(TargetSet.Single(lastCandidates[0].GetInstanceID()));
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

                SubscribePanel();
            }

            SubscribeUiRoot(subscribeConfirm: targetPanel == null);

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
            // If virtual mode, confirm current index immediately (panel mode confirms via UI events)
            if (targetPanel == null && lastCandidates.Length > 0)
            {
                LogB1("BUITI.Confirm.Virtual", $"sessionId={currentSessionId} targetId={lastCandidates[currentIndex].GetInstanceID()} index={currentIndex} candidates={lastCandidates.Length}");
                ResolveOnce(TargetSet.Single(lastCandidates[currentIndex].GetInstanceID()));
            }
            // Panel mode confirmation is handled via OnTargetSelected from the panel.
        }

        private void ResolveOnce(TargetSet result)
        {
            var tcs = pendingTcs;
            if (tcs == null)
            {
                LogB1("BUITI.ResolveOnce.Block", $"reason=NoPending sessionId={currentSessionId}");
                return;
            }
            if (confirmInFlight)
            {
                LogB1("BUITI.ResolveOnce.Block", $"reason=InFlight sessionId={currentSessionId}");
                return;
            }

            pendingTcs = null;
            confirmInFlight = true;
            try
            {
                LogB1("BUITI.ResolveOnce.Ok", $"sessionId={currentSessionId}");
                ResolveAndClear(result, tcs);
            }
            catch (Exception ex)
            {
                LogB1("BUITI.ResolveOnce.Block", $"reason=Exception sessionId={currentSessionId} ex={ex.GetType().Name}");
                throw;
            }
        }

        private void ResolveAndClear(TargetSet result, TaskCompletionSource<TargetSet> tcs)
        {
            LogB1("BUITI.Resolve", $"sessionId={currentSessionId} result={(result.IsEmpty ? "empty" : (result.IsBack ? "back" : (result.IsGroup ? "group" : "single")))}");

            tcs?.TrySetResult(result);

            UnsubscribePanel();
            UnsubscribeVirtual();
            UnsubscribeUiRoot();
            confirmInFlight = false;
            ClearAllHighlights();
        }

        private void CycleTarget(int direction)
        {
            currentIndex = (currentIndex + direction + lastCandidates.Length) % lastCandidates.Length;
            var target = lastCandidates[currentIndex];
            HighlightSet(TargetSet.Single(target.GetInstanceID()));
            BattleDiagnostics.Log("Targeting", $"Virtual Cycle -> {target.name}", this);
            inputDriver?.PlayNavigateAudio();
        }

        private void HandleSelected(int targetId)
        {
            BattleDiagnostics.Log("Targeting", $"Target Selected: {targetId}", this);
            ClearAllHighlights();
            BattleDiagnostics.Log("PAE.BUITI", $"phase=BUITI.Selected sessionId={currentSessionId} targetId={targetId}", this);
            ResolveOnce(TargetSet.Single(targetId));
        }

        private void HandleCancel()
        {
            BattleDiagnostics.Log("Targeting", "Selection Cancelled (Back)", this);
            ClearAllHighlights();
            LogB1("BUITI.Cancel", $"sessionId={currentSessionId}");
            
            // Resolve with Back set to signal return to previous menu
            ResolveOnce(TargetSet.Back);

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
            HighlightSet(proposed);
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

        private void HighlightSet(TargetSet set)
        {
            ClearAllHighlights();

            if (set.IsEmpty || lastCandidates == null || lastCandidates.Length == 0)
            {
                return;
            }

            HashSet<int> ids = null;
            if (set.IsGroup && set.Ids != null)
            {
                ids = new HashSet<int>(set.Ids);
            }
            else if (set.TryGetSingle(out var singleId))
            {
                ids = new HashSet<int> { singleId };
            }

            if (ids == null || ids.Count == 0)
            {
                return;
            }

            for (int i = 0; i < lastCandidates.Length; i++)
            {
                var c = lastCandidates[i];
                if (c != null && ids.Contains(c.GetInstanceID()))
                {
                    TrySetHighlight(c, true);
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

        private void SubscribePanel()
        {
            if (targetPanel == null)
            {
                return;
            }

            targetPanel.OnTargetSelected -= HandleSelected;
            targetPanel.OnTargetSelected += HandleSelected;
            targetPanel.OnCancelRequested -= HandleCancel;
            targetPanel.OnCancelRequested += HandleCancel;

            if (uiRoot == null)
            {
                targetPanel.gameObject.SetActive(true);
            }
        }

        private void UnsubscribePanel()
        {
            if (targetPanel == null)
            {
                return;
            }

            targetPanel.OnTargetSelected -= HandleSelected;
            targetPanel.OnCancelRequested -= HandleCancel;
            if (uiRoot == null)
            {
                targetPanel.gameObject.SetActive(false);
            }
        }

        private void SubscribeVirtual()
        {
            if (inputDriver == null || targetPanel != null || virtualSubscribed)
            {
                return;
            }

            inputDriver.OnNavigate -= HandleNavigate;
            inputDriver.OnNavigate += HandleNavigate;
            virtualSubscribed = true;
        }

        private void UnsubscribeVirtual()
        {
            if (inputDriver == null || !virtualSubscribed)
            {
                virtualSubscribed = false;
                return;
            }

            inputDriver.OnNavigate -= HandleNavigate;
            virtualSubscribed = false;
        }

        private void SubscribeUiRoot(bool subscribeConfirm)
        {
            if (uiRoot == null)
            {
                return;
            }

            uiRoot.OnTargetCancel -= HandleCancel;
            uiRoot.OnTargetCancel += HandleCancel;

            if (subscribeConfirm)
            {
                uiRoot.OnTargetConfirmed -= HandleConfirm;
                uiRoot.OnTargetConfirmed += HandleConfirm;
            }
        }

        private void UnsubscribeUiRoot()
        {
            if (uiRoot == null)
            {
                return;
            }

            uiRoot.OnTargetCancel -= HandleCancel;
            uiRoot.OnTargetConfirmed -= HandleConfirm;
        }
    }
}
