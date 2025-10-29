using System;
using System.Collections.Generic;
using System.Text;
using BattleV2.Orchestration;
using BattleV2.Providers;
using BattleV2.Charge;
using UnityEngine;

namespace BattleV2.Debugging
{
    /// <summary>
    /// Produces player-facing combat log lines whenever the BattleManager resolves a player action.
    /// Drop it on the same GameObject as BattleManagerV2 (or assign the reference manually).
    /// </summary>
    public sealed class FriendlyCombatLogger : MonoBehaviour
    {
        [SerializeField] private BattleManagerV2 manager;
        [SerializeField] private bool logToConsole = true;

        private void Awake()
        {
            if (manager == null)
            {
                manager = GetComponent<BattleManagerV2>();
            }

            if (manager == null)
            {
                Debug.LogWarning("[FriendlyCombatLogger] No BattleManagerV2 assigned. Component disabled.", this);
                enabled = false;
                return;
            }

            manager.OnPlayerActionResolved += HandlePlayerActionResolved;
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.OnPlayerActionResolved -= HandlePlayerActionResolved;
            }
        }

        private void HandlePlayerActionResolved(BattleSelection selection, int cpBefore, int cpAfter)
        {
            var lines = BuildFriendlyLines(selection, cpBefore, cpAfter);
            if (lines.Count == 0)
            {
                return;
            }

            if (logToConsole)
            {
                foreach (var line in lines)
                {
                    Debug.Log(line);
                }
            }
        }

        private List<string> BuildFriendlyLines(BattleSelection selection, int cpBefore, int cpAfter)
        {
            var lines = new List<string>();

            string actorName = manager != null && manager.Player != null
                ? manager.Player.DisplayName
                : "Player";

            string abilityName = selection.Action != null
                ? (!string.IsNullOrWhiteSpace(selection.Action.displayName)
                    ? selection.Action.displayName
                    : selection.Action.id)
                : "(Acción)";

            lines.Add($"▶ {actorName} usó {abilityName}!");

            var timed = selection.TimedHitResult;
            if (timed.HasValue && timed.Value.TotalHits > 0)
            {
                lines.Add(DescribeTiming(timed.Value));
            }

            int damage = timed.HasValue ? Mathf.Max(0, timed.Value.TotalDamageApplied) : 0;
            lines.Add(DescribeDamage(damage, timed));

            int cpDelta = cpAfter - cpBefore;
            lines.Add(DescribeCpChange(cpDelta));

            return lines;
        }

        private static string DescribeTiming(TimedHitResult result)
        {
            if (result.TotalHits <= 0)
            {
                return "¡Sin ventanas de timing esta vez!";
            }

            if (result.HitsSucceeded >= result.TotalHits)
            {
                return "¡Timing perfecto!";
            }

            if (result.HitsSucceeded > 0)
            {
                return "¡Buen ritmo, sigue así!";
            }

            return "¡Se perdió el timing!";
        }

        private static string DescribeDamage(int damage, TimedHitResult? timed)
        {
            if (damage <= 0)
            {
                return "No causó daño...";
            }

            float mult = timed?.DamageMultiplier ?? 1f;
            if (mult >= 1.5f)
            {
                return $"¡Golpe crítico! {damage} de daño.";
            }

            if (mult <= 0.75f)
            {
                return $"No fue muy eficaz... {damage} de daño.";
            }

            return $"Infligió {damage} de daño.";
        }

        private static string DescribeCpChange(int cpDelta)
        {
            if (cpDelta > 0)
            {
                return $"Recuperó {cpDelta} CP.";
            }

            if (cpDelta < 0)
            {
                return $"Gastó {Math.Abs(cpDelta)} CP.";
            }

            return "Sin cambios de CP.";
        }
    }
}
