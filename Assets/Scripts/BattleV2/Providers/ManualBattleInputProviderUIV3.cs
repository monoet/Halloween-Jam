using System;
using BattleV2.UI;
using UnityEngine;

namespace BattleV2.Providers
{
    /// <summary>
    /// Manual input provider that delegates action selection to the new battle command UI.
    /// </summary>
    public class ManualBattleInputProviderUIV3 : MonoBehaviour, IBattleInputProvider
    {
        [SerializeField] private BattleCommandUIPresenter presenter;

        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            if (presenter == null)
            {
                Debug.LogWarning("[ManualBattleInputProviderUIV3] Missing presenter. Cancelling request.");
                onCancel?.Invoke();
                return;
            }

            presenter.Present(context, selection =>
            {
                onSelected?.Invoke(selection);
            }, () =>
            {
                onCancel?.Invoke();
            });
        }
    }
}
