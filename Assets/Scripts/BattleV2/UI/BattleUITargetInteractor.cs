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
                    inputDriver.SetState(new TargetSelectionState());
                }
                else if (uiRoot != null)
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
                uiRoot.OnCancel += HandleCancel;
            }

            return pendingTcs.Task;
        }

        private void Update()
        {
            // Solo modo virtual (sin panel) y con selección pendiente
            if (pendingTcs == null || targetPanel != null)
            {
                return;
            }

            if (lastCandidates.Length == 0)
            {
                return;
            }

            // Navegación
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                CycleTarget(1);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                CycleTarget(-1);
            }

            // Confirmar
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z))
            {
                HandleSelected(lastCandidates[currentIndex].GetInstanceID());
            }

            // Cancelar
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.X))
            {
                HandleCancel();
            }
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
            BattleDiagnostics.Log("Targeting", "Selection Cancelled", this);
            ClearAllHighlights();
            ResolveAndClear(proposed.IsEmpty ? TargetSet.None : proposed);

            // Regresar al estado de menú si el driver existe
            inputDriver?.SetState(new MenuState());
        }

        private void ResolveAndClear(TargetSet result)
        {
            if (targetPanel != null)
            {
                targetPanel.OnTargetSelected -= HandleSelected;
                targetPanel.OnCancelRequested -= HandleCancel;
            }

            if (uiRoot != null)
            {
                uiRoot.OnCancel -= HandleCancel;
            }

            pendingTcs?.TrySetResult(result);
            pendingTcs = null;
            proposed = TargetSet.None;
            lastCandidates = System.Array.Empty<CombatantState>();
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
