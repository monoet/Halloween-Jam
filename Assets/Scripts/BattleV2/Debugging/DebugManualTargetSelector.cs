using System.Collections.Generic;
using System.Threading.Tasks;
using BattleV2.Core;
using BattleV2.Orchestration;
using BattleV2.Targeting;
using BattleV2.UI;
using UnityEngine;

namespace BattleV2.Debugging
{
    /// <summary>
    /// Simple target selector driven by keyboard input for the debug harness.
    /// </summary>
    public sealed class DebugManualTargetSelector : MonoBehaviour, ITargetSelectionInteractor
    {
        [Header("Bindings")]
        [SerializeField] private KeyCode previousKey = KeyCode.LeftArrow;
        [SerializeField] private KeyCode nextKey = KeyCode.RightArrow;
        [SerializeField] private KeyCode confirmKey = KeyCode.Return;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] private HUDManager hudManager;

        private readonly List<CombatantState> candidates = new();
        private readonly List<int> idBuffer = new();

        private TaskCompletionSource<TargetSet> pendingSelection;
        private int currentIndex;
        private bool selectionActive;
        private TargetSet proposedSet;
        private CombatantHudWidget highlightedWidget;
        private BattleManagerV2 manager;

        public void Initialise(BattleManagerV2 owner)
        {
            manager = owner;
            if (manager != null)
            {
                manager.SetTargetSelectionInteractor(this);
            }
        }

        private void Awake()
        {
            if (hudManager == null)
            {
                hudManager = FindObjectOfType<HUDManager>();
            }
        }

        private void OnDisable()
        {
            CancelSelectionInternal(TargetSet.None);
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.SetTargetSelectionInteractor(null);
            }
        }

        private void Update()
        {
            if (!selectionActive)
            {
                return;
            }

            if (Input.GetKeyDown(previousKey))
            {
                Step(-1);
            }

            if (Input.GetKeyDown(nextKey))
            {
                Step(1);
            }

            if (Input.GetKeyDown(confirmKey))
            {
                CompleteSelection(BuildCurrentSet());
            }
            else if (Input.GetKeyDown(cancelKey))
            {
                CompleteSelection(proposedSet.IsEmpty ? BuildCurrentSet() : proposedSet);
            }
        }

        public Task<TargetSet> SelectAsync(TargetContext context, TargetSet suggested)
        {
            if (context.Query.Shape == TargetShape.All)
            {
                var pool = ResolveAudience(context);
                if (pool == null)
                {
                    return Task.FromResult(suggested);
                }

                idBuffer.Clear();
                CollectAliveIds(pool, idBuffer);
                if (idBuffer.Count == 0)
                {
                    return Task.FromResult(TargetSet.None);
                }

                var ids = idBuffer.ToArray();
                idBuffer.Clear();
                return Task.FromResult(TargetSet.Group(ids));
            }

            if (context.Query.Audience == TargetAudience.Self)
            {
                return Task.FromResult(context.Origin != null
                    ? TargetSet.Single(context.Origin.GetInstanceID())
                    : TargetSet.None);
            }

            candidates.Clear();
            CollectAlive(ResolveAudience(context), candidates);

            if (candidates.Count == 0)
            {
                candidates.Clear();
                return Task.FromResult(TargetSet.None);
            }

            proposedSet = suggested;
            selectionActive = true;
            currentIndex = DetermineInitialIndex(suggested);
            ApplyHighlight();

            pendingSelection = new TaskCompletionSource<TargetSet>();
            return pendingSelection.Task;
        }

        private void Step(int direction)
        {
            if (candidates.Count == 0)
            {
                return;
            }

            currentIndex = (currentIndex + direction) % candidates.Count;
            if (currentIndex < 0)
            {
                currentIndex += candidates.Count;
            }

            ApplyHighlight();
        }

        private TargetSet BuildCurrentSet()
        {
            if (candidates.Count == 0)
            {
                return TargetSet.None;
            }

            var candidate = candidates[Mathf.Clamp(currentIndex, 0, candidates.Count - 1)];
            return candidate != null ? TargetSet.Single(candidate.GetInstanceID()) : TargetSet.None;
        }

        private void CompleteSelection(TargetSet result)
        {
            selectionActive = false;
            ClearHighlight();
            candidates.Clear();
            proposedSet = TargetSet.None;

            var tcs = pendingSelection;
            pendingSelection = null;
            tcs?.TrySetResult(result);
        }

        private void CancelSelectionInternal(TargetSet result)
        {
            if (!selectionActive && pendingSelection == null)
            {
                return;
            }

            selectionActive = false;
            ClearHighlight();
            candidates.Clear();
            proposedSet = TargetSet.None;

            var tcs = pendingSelection;
            pendingSelection = null;
            tcs?.TrySetResult(result);
        }

        private void ApplyHighlight()
        {
            ClearHighlight();

            if (candidates.Count == 0 || hudManager == null)
            {
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, candidates.Count - 1);
            var candidate = candidates[currentIndex];
            if (candidate == null)
            {
                return;
            }

            if (hudManager.TryGetWidget(candidate, out var widget) && widget != null)
            {
                highlightedWidget = widget;
                highlightedWidget.SetHighlighted(true);
            }
        }

        private void ClearHighlight()
        {
            if (highlightedWidget != null)
            {
                highlightedWidget.SetHighlighted(false);
                highlightedWidget = null;
            }
        }

        private IReadOnlyList<CombatantState> ResolveAudience(TargetContext context)
        {
            return context.Query.Audience switch
            {
                TargetAudience.Allies => context.Allies,
                TargetAudience.Self => context.Origin != null ? new[] { context.Origin } : null,
                _ => context.Enemies
            };
        }

        private void CollectAlive(IReadOnlyList<CombatantState> source, List<CombatantState> buffer)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                var candidate = source[i];
                if (candidate != null && candidate.IsAlive)
                {
                    buffer.Add(candidate);
                }
            }
        }

        private void CollectAliveIds(IReadOnlyList<CombatantState> source, List<int> buffer)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                var candidate = source[i];
                if (candidate != null && candidate.IsAlive)
                {
                    buffer.Add(candidate.GetInstanceID());
                }
            }
        }

        private int DetermineInitialIndex(TargetSet suggested)
        {
            if (suggested.TryGetSingle(out var id))
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (candidate != null && candidate.GetInstanceID() == id)
                    {
                        return i;
                    }
                }
            }

            return 0;
        }
    }
}
