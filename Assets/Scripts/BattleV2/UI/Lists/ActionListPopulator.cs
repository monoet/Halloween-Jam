using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        private readonly List<Selectable> lastSelectables = new List<Selectable>();
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
            lastSelectables.Clear();
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
            var selectables = new List<Selectable>();

            for (int i = 0; i < rows.Count; i++)
            {
                var data = rows[i];
                if (data == null)
                {
                    continue;
                }

                var row = Instantiate(spellRowPrefab, content);
                row.SetIndex(i);
                row.Bind(data, onHover, onSubmit, onBlocked);
                spawned.Add(row);
                if (row.Selectable != null)
                {
                    selectables.Add(row.Selectable);
                }

                first ??= row;
                if (firstEnabled == null && data.IsEnabled)
                {
                    firstEnabled = row;
                }
            }

            ForceRebuildLayout();
            BuildExplicitNavigation(selectables);
            CacheSelectables(selectables);
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
            var selectables = new List<Selectable>();

            for (int i = 0; i < rows.Count; i++)
            {
                var data = rows[i];
                if (data == null)
                {
                    continue;
                }

                var row = Instantiate(itemRowPrefab, content);
                row.SetIndex(i);
                row.Bind(data, onHover, onSubmit, onBlocked);
                spawned.Add(row);
                if (row.Selectable != null)
                {
                    selectables.Add(row.Selectable);
                }

                first ??= row;
                if (firstEnabled == null && row.IsEnabledForSubmit)
                {
                    firstEnabled = row;
                }
            }

            ForceRebuildLayout();
            BuildExplicitNavigation(selectables);
            CacheSelectables(selectables);
            var target = firstEnabled != null ? firstEnabled.FocusTarget : first != null ? first.FocusTarget : null;
            FocusRow(target);
        }

        private void FocusRow(GameObject target)
        {
            if (target == null || EventSystem.current == null)
            {
                return;
            }

            var es = EventSystem.current;
            var before = es != null ? es.currentSelectedGameObject : null;
            var hasFeedback = target.GetComponent<BattleV2.UI.UISelectionFeedback>() != null;
            var hasSelectable = target.GetComponent<Selectable>() != null;
            BattleV2.UI.Diagnostics.MagMenuDebug.Log("03", $"FocusRow target={(target ? target.name : "null")} before={(before ? before.name : "null")} hasSelectable={hasSelectable} hasFeedback={hasFeedback}", target);

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
            // Garantiza que el ISelectHandler se dispare aunque Unity no lo haga por la reasignación.
            ExecuteEvents.Execute(target, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
            var after = EventSystem.current.currentSelectedGameObject;
            BattleV2.UI.Diagnostics.MagMenuDebug.Log("03", $"FocusRow after={(after ? after.name : "null")}", after);
            lastFocus = target;
        }

        public void FocusLast()
        {
            if (lastFocus != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(lastFocus);
            }
        }

        public void FocusFirstRow(bool preferEnabled = true)
        {
            if (EventSystem.current == null || lastSelectables.Count == 0)
            {
                return;
            }

            Selectable target = null;
            if (preferEnabled)
            {
                for (int i = 0; i < lastSelectables.Count; i++)
                {
                    var sel = lastSelectables[i];
                    if (sel != null && sel.IsInteractable())
                    {
                        target = sel;
                        break;
                    }
                }
            }

            if (target == null)
            {
                for (int i = 0; i < lastSelectables.Count; i++)
                {
                    var sel = lastSelectables[i];
                    if (sel != null)
                    {
                        target = sel;
                        break;
                    }
                }
            }

            if (target != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(target.gameObject);
                lastFocus = target.gameObject;
            }
        }

        private void ForceRebuildLayout()
        {
            if (content == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
        }

        private void BuildExplicitNavigation(List<Selectable> selectables)
        {
            if (selectables == null || selectables.Count == 0)
            {
                return;
            }

            BattleV2.UI.Diagnostics.MagMenuDebug.Log("02", $"BuildNav count={selectables.Count}", this);
            for (int i = 0; i < selectables.Count; i++)
            {
                var current = selectables[i];
                if (current == null)
                {
                    continue;
                }

                var nav = current.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = FindPrev(selectables, i);
                nav.selectOnDown = FindNext(selectables, i);
                current.navigation = nav;

                var up = nav.selectOnUp != null ? nav.selectOnUp.gameObject.name : "null";
                var down = nav.selectOnDown != null ? nav.selectOnDown.gameObject.name : "null";
                BattleV2.UI.Diagnostics.MagMenuDebug.Log("02", $"[{i}] {current.gameObject.name} up={up} down={down}", current);
            }
        }

        private static Selectable FindPrev(List<Selectable> list, int index)
        {
            if (list == null || list.Count == 0)
            {
                return null;
            }

            int i = index - 1;
            while (i >= 0)
            {
                if (list[i] != null)
                {
                    return list[i];
                }
                i--;
            }

            // wrap to last non-null
            for (int j = list.Count - 1; j >= 0; j--)
            {
                if (list[j] != null)
                {
                    return list[j];
                }
            }

            return null;
        }

        private static Selectable FindNext(List<Selectable> list, int index)
        {
            if (list == null || list.Count == 0)
            {
                return null;
            }

            int i = index + 1;
            while (i < list.Count)
            {
                if (list[i] != null)
                {
                    return list[i];
                }
                i++;
            }

            // wrap to first non-null
            for (int j = 0; j < list.Count; j++)
            {
                if (list[j] != null)
                {
                    return list[j];
                }
            }

            return null;
        }

        private void CacheSelectables(List<Selectable> selectables)
        {
            lastSelectables.Clear();
            if (selectables == null)
            {
                return;
            }

            for (int i = 0; i < selectables.Count; i++)
            {
                if (selectables[i] != null)
                {
                    lastSelectables.Add(selectables[i]);
                }
            }
        }
    }
}

