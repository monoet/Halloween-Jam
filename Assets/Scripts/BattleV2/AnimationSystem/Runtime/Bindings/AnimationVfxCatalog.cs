using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime.Bindings
{
    /// <summary>
    /// ScriptableObject catalog that instantiates prefabs for timeline VFX payloads.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Animation/VFX Catalog", fileName = "AnimationVfxCatalog")]
    public sealed class AnimationVfxCatalog : ScriptableObject, IAnimationVfxService
    {
        [Serializable]
        private struct Entry
        {
            public string id;
            public GameObject prefab;
            [Tooltip("If <= 0, the prefab persists until StopAllFor is invoked.")]
            public float lifetime;
            [Tooltip("Attach spawned instances to the actor transform instead of world space.")]
            public bool attachToActor;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField, Tooltip("Fallback lifetime used when entry.lifetime <= 0.")] private float defaultLifetime = 3f;

        private readonly Dictionary<string, Entry> lookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<CombatantState, List<GameObject>> activeInstances = new();

        private void OnEnable() => RebuildLookup();

#if UNITY_EDITOR
        private void OnValidate() => RebuildLookup();
#endif

        public bool TryPlay(string vfxId, in AnimationImpactEvent evt, in AnimationEventPayload payload)
        {
            if (string.IsNullOrWhiteSpace(vfxId))
            {
                return false;
            }

            if (!lookup.TryGetValue(vfxId, out var entry) || entry.prefab == null)
            {
                return false;
            }

            Vector3 position = ResolvePosition(evt);
            Quaternion rotation = entry.prefab.transform.rotation;
            Transform parent = entry.attachToActor && evt.Actor != null ? evt.Actor.transform : null;

            var instance = Instantiate(entry.prefab, position, rotation, parent);
            if (instance == null)
            {
                return false;
            }

            var key = evt.Actor;
            if (!activeInstances.TryGetValue(key, out var list))
            {
                list = new List<GameObject>();
                activeInstances[key] = list;
            }

            list.Add(instance);

            float lifetime = entry.lifetime;
            if (payload.TryGetFloat("duration", out var customLifetime))
            {
                lifetime = customLifetime;
            }

            if (lifetime <= 0f)
            {
                lifetime = defaultLifetime;
            }

            if (lifetime > 0f)
            {
                Destroy(instance, lifetime);
            }

            return true;
        }

        public void StopAllFor(CombatantState actor)
        {
            if (!activeInstances.TryGetValue(actor, out var list))
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    Destroy(list[i]);
                }
            }

            list.Clear();
            activeInstances.Remove(actor);
        }

        private void RebuildLookup()
        {
            lookup.Clear();
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.id) || entry.prefab == null)
                {
                    continue;
                }

                lookup[entry.id] = entry;
            }
        }

        private static Vector3 ResolvePosition(AnimationImpactEvent evt)
        {
            if (evt.Target != null)
            {
                return evt.Target.transform.position;
            }

            if (evt.Actor != null)
            {
                return evt.Actor.transform.position;
            }

            return Vector3.zero;
        }
    }
}
