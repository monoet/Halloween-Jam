using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleV2.UI.Lists
{
    /// <summary>
    /// Instancia y limpia filas de acciones bajo un ScrollRect Content. No usa pooling por simplicidad.
    /// </summary>
    public sealed class ActionListPopulator : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private SpellRowUI spellRowPrefab;
        [SerializeField] private ItemRowUI itemRowPrefab;

        private readonly List<Component> spawned = new List<Component>();
        private GameObject lastFocus;

        public void Clear()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                var comp = spawned[i];
                if (comp == null)
                {
                    continue;
                }

                if (comp is IActionRowUIBehaviour row)
                {
                    row.ClearHandlers();
                }

                if (comp != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(comp.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(comp.gameObject);
                    }
                }
            }

            spawned.Clear();
            lastFocus = null;
        }

        public void ShowSpells(
            IReadOnlyList<ISpellRowData> rows,
            System.Action<ISpellRowData> onHover,
            System.Action<ISpellRowData> onSubmit,
            System.Action<ISpellRowData> onBlocked)
        {
            Clear();
            if (rows == null || content == null || spellRowPrefab == null)
            {
                return;
            }

            SpellRowUI first = null;
            SpellRowUI firstEnabled = null;

            for (int i = 0; i < rows.Count; i++)
            {
                var data = rows[i];
                if (data == null)
                {
                    continue;
                }

                var row = Instantiate(spellRowPrefab, content);
                row.Bind(data, onHover, onSubmit, onBlocked);
                spawned.Add(row);

                first ??= row;
                if (firstEnabled == null && data.IsEnabled)
                {
                    firstEnabled = row;
                }
            }

            var target = firstEnabled != null ? firstEnabled.FocusTarget : first != null ? first.FocusTarget : null;
            FocusRow(target);
        }

        public void ShowItems(
            IReadOnlyList<IItemRowData> rows,
            System.Action<IItemRowData> onHover,
            System.Action<IItemRowData> onSubmit,
            System.Action<IItemRowData> onBlocked)
        {
            Clear();
            if (rows == null || content == null || itemRowPrefab == null)
            {
                return;
            }

            ItemRowUI first = null;
            ItemRowUI firstEnabled = null;

            for (int i = 0; i < rows.Count; i++)
            {
                var data = rows[i];
                if (data == null)
                {
                    continue;
                }

                var row = Instantiate(itemRowPrefab, content);
                row.Bind(data, onHover, onSubmit, onBlocked);
                spawned.Add(row);

                first ??= row;
                if (firstEnabled == null && row.IsEnabledForSubmit)
                {
                    firstEnabled = row;
                }
            }

            var target = firstEnabled != null ? firstEnabled.FocusTarget : first != null ? first.FocusTarget : null;
            FocusRow(target);
        }

        private void FocusRow(GameObject target)
        {
            if (target == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
            lastFocus = target;
        }

        public void FocusLast()
        {
            if (lastFocus != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(lastFocus);
            }
        }
    }
}
