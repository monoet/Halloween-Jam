using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime.Bindings
{
    /// <summary>
    /// Placeholder camera service that logs requested effects.
    /// Replace with Cinemachine or custom logic as needed.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Animation/Camera Service", fileName = "AnimationCameraService")]
    public sealed class AnimationCameraService : ScriptableObject, IAnimationCameraService
    {
        [Serializable]
        private struct CameraEffectEntry
        {
            public string id;
            public AnimationCameraEffect effect;
        }

        [SerializeField]
        [Tooltip("Lista de efectos disponibles, indexados por id.")]
        private CameraEffectEntry[] effects = Array.Empty<CameraEffectEntry>();

        private readonly Dictionary<string, AnimationCameraEffect> lookup = new(StringComparer.OrdinalIgnoreCase);

        private void OnEnable() => RebuildLookup();

#if UNITY_EDITOR
        private void OnValidate() => RebuildLookup();
#endif

        public bool TryApply(
            string effectId,
            CombatantState actor,
            AnimationImpactEvent? impactEvent,
            AnimationPhaseEvent? phaseEvent,
            in AnimationEventPayload payload)
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                return false;
            }

            if (!lookup.TryGetValue(effectId, out var effect) || effect == null)
            {
                Debug.LogWarning($"[AnimCameraService] Effect id '{effectId}' is not bound to an effect asset.", this);
                return false;
            }

            return effect.TryApply(actor, impactEvent, phaseEvent, payload);
        }

        public void Reset(CombatantState actor)
        {
            if (actor == null)
            {
                return;
            }

            foreach (var kvp in lookup)
            {
                kvp.Value?.Reset(actor);
            }
        }

        private void RebuildLookup()
        {
            lookup.Clear();
            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                var entry = effects[i];
                if (string.IsNullOrWhiteSpace(entry.id) || entry.effect == null)
                {
                    continue;
                }

                lookup[entry.id] = entry.effect;
            }
        }
    }
}
