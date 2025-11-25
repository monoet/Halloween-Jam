using System.Threading.Tasks;
using BattleV2.Targeting;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Interactor simple que usa TargetSelectionPanel para seleccionar un objetivo.
    /// Los ids deben estar configurados en los botones del panel.
    /// </summary>
    public sealed class BattleUITargetInteractor : MonoBehaviour, ITargetSelectionInteractor
    {
        [SerializeField] private TargetSelectionPanel targetPanel;

        private TaskCompletionSource<TargetSet> pendingTcs;
        private TargetSet proposed;

        private void Awake()
        {
            targetPanel ??= GetComponent<TargetSelectionPanel>();
        }

        public Task<TargetSet> SelectAsync(TargetContext context, TargetSet proposedSet)
        {
            if (pendingTcs != null)
            {
                return pendingTcs.Task;
            }

            proposed = proposedSet;

            if (targetPanel == null)
            {
                return Task.FromResult(proposedSet);
            }

            pendingTcs = new TaskCompletionSource<TargetSet>();
            targetPanel.gameObject.SetActive(true);
            targetPanel.OnTargetSelected += HandleSelected;
            targetPanel.OnCancel += HandleCancel;
            return pendingTcs.Task;
        }

        private void HandleSelected(int targetId)
        {
            ResolveAndClear(TargetSet.Single(targetId));
        }

        private void HandleCancel()
        {
            ResolveAndClear(proposed.IsEmpty ? TargetSet.None : proposed);
        }

        private void ResolveAndClear(TargetSet result)
        {
            if (targetPanel != null)
            {
                targetPanel.OnTargetSelected -= HandleSelected;
                targetPanel.OnCancel -= HandleCancel;
                targetPanel.gameObject.SetActive(false);
            }

            pendingTcs?.TrySetResult(result);
            pendingTcs = null;
            proposed = TargetSet.None;
        }
    }
}
