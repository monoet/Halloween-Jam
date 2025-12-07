using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Instantiates and manages HUD widgets for combatants.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("Ally HUD")]
        [SerializeField] private CombatantHudWidget allyWidgetPrefab;
        [SerializeField] private Transform allyContainer;
        [SerializeField] private List<CombatantHudWidget> allyWidgets = new();

        [Header("Enemy HUD")]
        [SerializeField] private CombatantHudWidget enemyWidgetPrefab;
        [SerializeField] private Transform enemyContainer;

        private readonly Dictionary<CombatantState, CombatantHudWidget> widgets = new();
        private CombatantHudWidget lastHighlighted;

        public void RegisterCombatants(IEnumerable<CombatantState> combatants, bool isEnemy)
        {
            if (!isEnemy && HasPresetAllyWidgets())
            {
                BindAllyWidgets(combatants);
                return;
            }

            if (combatants == null)
            {
                return;
            }

            foreach (var combatant in combatants)
            {
                RegisterCombatant(combatant, isEnemy);
            }
        }

        public void RegisterCombatant(CombatantState combatant, bool isEnemy)
        {
            if (combatant == null)
            {
                return;
            }

            if (!isEnemy && HasPresetAllyWidgets())
            {
                var currentAllies = new List<CombatantState>();
                foreach (var pair in widgets)
                {
                    if (pair.Value != null && allyWidgets.Contains(pair.Value))
                    {
                        currentAllies.Add(pair.Key);
                    }
                }

                currentAllies.Add(combatant);
                BindAllyWidgets(currentAllies);
                return;
            }

            if (widgets.TryGetValue(combatant, out var existingWidget) && existingWidget != null)
            {
                existingWidget.Bind(combatant);
                return;
            }

            var prefab = isEnemy ? enemyWidgetPrefab : allyWidgetPrefab;
            var container = isEnemy ? enemyContainer : allyContainer;

            if (prefab == null || container == null)
            {
                Debug.LogWarning($"[HUDManager] Missing {(isEnemy ? "enemy" : "ally")} HUD prefab or container. Unable to register '{combatant.DisplayName}'.", this);
                return;
            }

            var widget = Instantiate(prefab, container);
            widget.Bind(combatant);
            widgets[combatant] = widget;
        }

        public void UnregisterCombatant(CombatantState combatant)
        {
            if (combatant == null)
            {
                return;
            }

            if (HasPresetAllyWidgets() &&
                widgets.TryGetValue(combatant, out var presetWidget) &&
                presetWidget != null &&
                allyWidgets.Contains(presetWidget))
            {
                widgets.Remove(combatant);
                presetWidget.Unbind();
                presetWidget.gameObject.SetActive(false);
                return;
            }

            if (!widgets.TryGetValue(combatant, out var widget))
            {
                return;
            }

            widgets.Remove(combatant);

            if (widget == null)
            {
                return;
            }

            widget.Unbind();
            Destroy(widget.gameObject);
        }

        public void Clear()
        {
            if (HasPresetAllyWidgets())
            {
                for (int i = 0; i < allyWidgets.Count; i++)
                {
                    var widget = allyWidgets[i];
                    if (widget == null)
                    {
                        continue;
                    }

                    widget.Unbind();
                    widget.gameObject.SetActive(false);
                }
            }

            foreach (var widget in widgets.Values)
            {
                if (HasPresetAllyWidgets() && allyWidgets.Contains(widget))
                {
                    continue;
                }

                if (widget == null)
                {
                    continue;
                }

                widget.Unbind();
                Destroy(widget.gameObject);
            }

            widgets.Clear();
        }

        public bool TryGetWidget(CombatantState combatant, out CombatantHudWidget widget)
        {
            return widgets.TryGetValue(combatant, out widget);
        }

        public void HighlightCombatant(CombatantState combatant)
        {
            CombatantHudWidget highlighted = null;
            foreach (var pair in widgets)
            {
                var widget = pair.Value;
                if (widget == null)
                {
                    continue;
                }

                bool isTarget = pair.Key == combatant;
                widget.SetHighlighted(isTarget);
                if (isTarget)
                {
                    highlighted = widget;
                }
            }

            lastHighlighted = highlighted;
        }

        private void BindAllyWidgets(IEnumerable<CombatantState> combatants)
        {
            if (!HasPresetAllyWidgets())
            {
                if (combatants == null)
                {
                    return;
                }

                foreach (var combatant in combatants)
                {
                    RegisterCombatant(combatant, isEnemy: false);
                }
                return;
            }

            var activeAllies = combatants != null ? new List<CombatantState>(combatants) : new List<CombatantState>();
            var activeSet = new HashSet<CombatantState>();

            for (int i = 0; i < allyWidgets.Count; i++)
            {
                var widget = allyWidgets[i];
                if (widget == null)
                {
                    continue;
                }

                if (i < activeAllies.Count)
                {
                    var combatant = activeAllies[i];
                    if (combatant == null)
                    {
                        widget.Unbind();
                        widget.gameObject.SetActive(false);
                        continue;
                    }

                    widget.gameObject.SetActive(true);
                    widget.Bind(combatant);
                    widgets[combatant] = widget;
                    activeSet.Add(combatant);
                }
                else
                {
                    widget.Unbind();
                    widget.gameObject.SetActive(false);
                }
            }

            if (activeAllies.Count > allyWidgets.Count)
            {
                Debug.LogWarning($"[HUDManager] Received {activeAllies.Count} allies but only {allyWidgets.Count} HUD slots are configured.", this);
            }

            var toRemove = new List<CombatantState>();
            foreach (var pair in widgets)
            {
                if (pair.Value != null && allyWidgets.Contains(pair.Value) && !activeSet.Contains(pair.Key))
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                widgets.Remove(toRemove[i]);
            }
        }

        private bool HasPresetAllyWidgets()
        {
            return allyWidgets != null && allyWidgets.Count > 0;
        }
    }
}
