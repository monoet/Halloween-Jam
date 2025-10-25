using System.Collections.Generic;
using UnityEngine;
using BattleV2.Orchestration;

namespace BattleV2.UI
{
    /// <summary>
    /// Keeps HUD widgets synchronized with the combatants currently managed by BattleManagerV2.
    /// Works with both the new CombatantHudWidget and the legacy BattleHUDController.
    /// </summary>
    public class BattleCombatantBinder : MonoBehaviour
    {
        [SerializeField] private BattleManagerV2 manager;
        [Header("New HUD Widgets")]
        [SerializeField] private CombatantHudWidget playerWidget;
        [SerializeField] private CombatantHudWidget enemyWidget;
        [Header("Legacy HUD Controllers")]
        [SerializeField] private BattleHUDController playerHudController;
        [SerializeField] private BattleHUDController enemyHudController;

        private void OnEnable()
        {
            if (manager == null)
            {
                manager = FindObjectOfType<BattleManagerV2>();
            }

            if (manager != null)
            {
                manager.OnCombatantsBound += HandleCombatantsBound;
                HandleCombatantsBound(manager.Allies, manager.Enemies);
            }
        }

        private void OnDisable()
        {
            if (manager != null)
            {
                manager.OnCombatantsBound -= HandleCombatantsBound;
            }
        }

        private void HandleCombatantsBound(IReadOnlyList<CombatantState> allies, IReadOnlyList<CombatantState> enemies)
        {
            var player = allies != null && allies.Count > 0 ? allies[0] : null;
            var enemy = enemies != null && enemies.Count > 0 ? enemies[0] : null;

            playerWidget?.Bind(player);
            enemyWidget?.Bind(enemy);

            if (playerHudController != null)
            {
                playerHudController.SetState(player);
            }

            if (enemyHudController != null)
            {
                enemyHudController.SetState(enemy);
            }
        }
    }
}
