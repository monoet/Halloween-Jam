using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime.Bindings
{
    /// <summary>
    /// Simple ScriptableObject-backed implementation of <see cref="IAnimationSfxService"/>.
    /// Maps string identifiers to audio clips and plays them using transient AudioSources.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Animation/SFX Catalog", fileName = "AnimationSfxCatalog")]
    public sealed class AnimationSfxCatalog : ScriptableObject, IAnimationSfxService
    {
        [Serializable]
        private struct Entry
        {
            public string id;
            public AudioClip clip;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField, Tooltip("If true, clips are positioned at the actor transform. Otherwise Vector3.zero.")]
        private bool positionAtActor = true;
        [SerializeField, Tooltip("Optional default volume (0..1). Overridden by payload 'volume'.")]
        [Range(0f, 1f)] private float defaultVolume = 1f;

        private readonly Dictionary<string, AudioClip> lookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(CombatantState actor, string id), AudioSource> activeSources = new();

        private void OnEnable() => RebuildLookup();

#if UNITY_EDITOR
        private void OnValidate() => RebuildLookup();
#endif

        public bool TryPlay(
            string sfxId,
            CombatantState actor,
            AnimationImpactEvent? impactEvent,
            AnimationPhaseEvent? phaseEvent,
            in AnimationEventPayload payload)
        {
            if (string.IsNullOrWhiteSpace(sfxId))
            {
                return false;
            }

            if (!lookup.TryGetValue(sfxId, out var clip) || clip == null)
            {
                return false;
            }

            float volume = defaultVolume;
            if (payload.TryGetFloat("volume", out var customVolume))
            {
                volume = Mathf.Clamp01(customVolume);
            }

            Vector3 position = Vector3.zero;
            if (positionAtActor && actor != null)
            {
                position = actor.transform.position;
            }

            var key = (actor, sfxId);
            CleanupEntry(key);

            var go = new GameObject($"SFX_{sfxId}");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = positionAtActor ? 1f : 0f;
            source.playOnAwake = false;
            source.loop = false;
            source.Play();

            float lifetime = clip.length > 0f ? clip.length : 1f;
            UnityEngine.Object.Destroy(go, lifetime);

            activeSources[key] = source;
            return true;
        }

        public void StopAllFor(CombatantState actor)
        {
            if (actor == null || activeSources.Count == 0)
            {
                return;
            }

            var keysToRemove = new List<(CombatantState, string)>();
            foreach (var kvp in activeSources)
            {
                if (kvp.Key.actor != actor)
                {
                    continue;
                }

                CleanupSource(kvp.Value);
                keysToRemove.Add(kvp.Key);
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                activeSources.Remove(keysToRemove[i]);
            }
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
                if (string.IsNullOrWhiteSpace(entry.id) || entry.clip == null)
                {
                    continue;
                }

                lookup[entry.id] = entry.clip;
            }
        }

        private void CleanupEntry((CombatantState actor, string id) key)
        {
            if (!activeSources.TryGetValue(key, out var source) || source == null)
            {
                activeSources.Remove(key);
                return;
            }

            CleanupSource(source);
            activeSources.Remove(key);
        }

        private static void CleanupSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            UnityEngine.Object.Destroy(source.gameObject);
        }
    }
}
