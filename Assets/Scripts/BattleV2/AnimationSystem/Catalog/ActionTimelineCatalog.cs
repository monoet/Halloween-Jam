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

        private readonly Dictionary<string, ActionTimeline> lookup = new(StringComparer.Ordinal);
        private bool initialized;

        public IReadOnlyList<ActionTimeline> Timelines => timelines;

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            lookup.Clear();
            for (int i = 0; i < timelines.Count; i++)
            {
                var asset = timelines[i];
                if (asset == null || string.IsNullOrWhiteSpace(asset.ActionId))
                {
                    continue;
                }

                lookup[asset.ActionId] = asset;
            }

            initialized = true;
        }

        public bool TryGetTimeline(string actionId, out ActionTimeline timeline)
        {
            Initialize();
            return lookup.TryGetValue(actionId, out timeline);
        }

        public ActionTimeline GetTimelineOrDefault(string actionId)
        {
            Initialize();
            lookup.TryGetValue(actionId, out var timeline);
            return timeline;
        }
    }
}
