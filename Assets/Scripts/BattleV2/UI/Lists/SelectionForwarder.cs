using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleV2.UI.Lists
{
    /// <summary>
    /// Forwards select/deselect events to provided callbacks. Useful when the selectable lives on a child GameObject.
    /// </summary>
    public sealed class SelectionForwarder : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private Action<BaseEventData> onSelect;
        private Action<BaseEventData> onDeselect;

        public void Configure(Action<BaseEventData> select, Action<BaseEventData> deselect)
        {
            onSelect = select;
            onDeselect = deselect;
        }

        public void OnSelect(BaseEventData eventData)
        {
            onSelect?.Invoke(eventData);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            onDeselect?.Invoke(eventData);
        }
    }
}
