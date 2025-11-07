using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;
using BattleV2.UI;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime.Bindings
{
    /// <summary>
    /// Placeholder UI service that logs payloads. Extend to hook into your HUD.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Animation/UI Service", fileName = "AnimationUiService")]
    public sealed class AnimationUiService : ScriptableObject, IAnimationUiService
    {
        [Serializable]
        private struct UiActionEntry
        {
            public string id;
            public AnimationUiAction action;
        }

        [SerializeField]
        [Tooltip("Acciones UI disponibles, configurables por id.")]
        private UiActionEntry[] actions = Array.Empty<UiActionEntry>();

        [Header("Spotlight")]
        [SerializeField] private TurnSpotlightController spotlightController;
        [SerializeField] private bool autoLocateSpotlightController = true;

        private readonly Dictionary<string, AnimationUiAction> lookup = new(StringComparer.OrdinalIgnoreCase);

        private void OnEnable() => RebuildLookup();

#if UNITY_EDITOR
        private void OnValidate() => RebuildLookup();
#endif

        public bool TryHandle(
            string uiId,
            CombatantState actor,
            AnimationPhaseEvent? phaseEvent,
            AnimationWindowEvent? windowEvent,
            AnimationImpactEvent? impactEvent,
            in AnimationEventPayload payload)
        {
            if (string.IsNullOrWhiteSpace(uiId))
            {
                return false;
            }

            if (HandleBuiltIn(uiId, actor, payload))
            {
                return true;
            }

            if (!lookup.TryGetValue(uiId, out var action) || action == null)
            {
                Debug.LogWarning($"[AnimUiService] UI id '{uiId}' has no action assigned.", this);
                return false;
            }

            return action.TryHandle(actor, phaseEvent, windowEvent, impactEvent, payload);
        }

        public void Clear(CombatantState actor)
        {
            if (actor == null)
            {
                return;
            }

            foreach (var kvp in lookup)
            {
                kvp.Value?.Clear(actor);
            }

            EnsureSpotlightController();
            spotlightController?.Hide(actor);
        }

        private void RebuildLookup()
        {
            lookup.Clear();
            if (actions == null)
            {
                return;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                var entry = actions[i];
                if (string.IsNullOrWhiteSpace(entry.id) || entry.action == null)
                {
                    continue;
                }

                lookup[entry.id] = entry.action;
            }
        }

        private bool HandleBuiltIn(string uiId, CombatantState actor, in AnimationEventPayload payload)
        {
            if (!string.Equals(uiId, "spotlight_in", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            EnsureSpotlightController();
            if (spotlightController == null)
            {
                Debug.LogWarning("[AnimUiService] spotlight_in requested but no TurnSpotlightController assigned.", this);
                return true;
            }

            spotlightController.Show(actor, payload);
            return true;
        }

        private void EnsureSpotlightController()
        {
            if (spotlightController != null || !autoLocateSpotlightController)
            {
                return;
            }

#if UNITY_2022_1_OR_NEWER
            spotlightController = FindAnyObjectByType<TurnSpotlightController>(FindObjectsInactive.Include);
#else
            spotlightController = FindObjectOfType<TurnSpotlightController>();
#endif
        }
    }
}
