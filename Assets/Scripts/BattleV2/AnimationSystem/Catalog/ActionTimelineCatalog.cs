using System;
using System.Collections.Generic;
using UnityEngine;
using BattleV2.AnimationSystem.Timelines;

namespace BattleV2.AnimationSystem.Catalog
{
    [CreateAssetMenu(menuName = "Battle/Animation/Action Timeline Catalog", fileName = "ActionTimelineCatalog")]
    public sealed class ActionTimelineCatalog : ScriptableObject
    {
        [SerializeField] private List<ActionTimeline> timelines = new();
        [SerializeField] private bool logMissingTimelines = true;

        private readonly Dictionary<string, ActionTimeline> lookup = new(StringComparer.Ordinal);
        private bool initialized;

        public IReadOnlyList<ActionTimeline> Timelines => timelines;
        public int TimelineCount => timelines?.Count ?? 0;

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            RebuildLookup();
        }

        [ContextMenu("Rebuild Lookup")]
        public void ForceRebuild()
        {
            initialized = false;
            RebuildLookup();
        }

        private void RebuildLookup()
        {
            lookup.Clear();
            bool anyRegistered = false;

            for (int i = 0; i < timelines.Count; i++)
            {
                var asset = timelines[i];
                if (asset == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(asset.ActionId))
                {
                    Debug.LogWarning($"[ActionTimelineCatalog] Timeline '{asset.name}' no tiene ActionId asignado.");
                    continue;
                }

                if (lookup.ContainsKey(asset.ActionId))
                {
                    Debug.LogWarning($"[ActionTimelineCatalog] Timeline duplicado para ActionId '{asset.ActionId}'. Se conservara la primera referencia.");
                    continue;
                }

                lookup[asset.ActionId] = asset;
                anyRegistered = true;
            }

            initialized = anyRegistered;
        }

        public bool TryGetTimeline(string actionId, out ActionTimeline timeline)
        {
            Initialize();
            if (lookup.TryGetValue(actionId, out timeline))
            {
                return true;
            }

            if (logMissingTimelines && !string.IsNullOrWhiteSpace(actionId))
            {
                Debug.LogWarning($"[ActionTimelineCatalog] No se encontro timeline para ActionId '{actionId}'.");
            }

            return false;
        }

        public ActionTimeline GetTimelineOrDefault(string actionId)
        {
            return TryGetTimeline(actionId, out var timeline) ? timeline : null;
        }
    }
}
