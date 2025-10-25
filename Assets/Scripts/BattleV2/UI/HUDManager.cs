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

        [Header("Enemy HUD")]
        [SerializeField] private CombatantHudWidget enemyWidgetPrefab;
        [SerializeField] private Transform enemyContainer;

        private readonly Dictionary<CombatantState, CombatantHudWidget> widgets = new();

        public void RegisterCombatants(IEnumerable<CombatantState> combatants, bool isEnemy)
        {
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
            foreach (var widget in widgets.Values)
            {
                if (widget == null)
                {
                    continue;
                }

                widget.Unbind();
                Destroy(widget.gameObject);
            }

            widgets.Clear();
        }
    }
}
