using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    /// <summary>
    /// Resolves presentation presets (tweens/SFX) for combat flags and routes them to listeners using filters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEventRouter : MonoBehaviour, ICombatEventListener
    {
        private const string LogTag = "CombatEventRouter";

        [Header("Registration")]
        [SerializeField] private bool autoRegisterOnEnable = true;
        [SerializeField] private bool logMissingAssets = true;

        [Header("Listeners")]
        [SerializeField] private TweenListenerBinding tweenListener = new TweenListenerBinding
        {
            filter = CombatEventFilter.DefaultCaster
        };

        [SerializeField] private SfxListenerBinding sfxListener = new SfxListenerBinding
        {
            filter = CombatEventFilter.DefaultImpact
        };

        [Header("Tween Presets")]
        [SerializeField] private List<TweenTriggerEntry> tweenPresets = new List<TweenTriggerEntry>();

        [Header("SFX Presets")]
        [SerializeField] private List<SfxPresetEntry> sfxPresets = new List<SfxPresetEntry>
        {
            new SfxPresetEntry { key = "default" }
        };

        [Header("Telemetry")]
        [SerializeField, Tooltip("Total combat events observed by the router.")]
        private int eventsRaised;
        [SerializeField] private int tweenCacheHit;
        [SerializeField] private int tweenCacheMiss;
        [SerializeField] private int missingTween;
        [SerializeField] private int sfxCacheHit;
        [SerializeField] private int sfxCacheMiss;

        private readonly Dictionary<string, TweenPreset> tweenLookup = new Dictionary<string, TweenPreset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SfxPreset> sfxLookup = new Dictionary<string, SfxPreset>(StringComparer.OrdinalIgnoreCase);
        private CombatEventDispatcher dispatcher;
        private bool isRegistered;

        public int TweenPresetCount => tweenLookup.Count;
        public int SfxPresetCount => sfxLookup.Count;

        private void Awake()
        {
            RebuildLookups();
            ResolveListeners();
        }

        private void OnEnable()
        {
            if (autoRegisterOnEnable)
            {
                Register();
            }
        }

        public void EnsureTweenPreset(string triggerId, TweenPreset preset)
        {
            if (string.IsNullOrWhiteSpace(triggerId) || preset == null)
            {
                return;
            }

            if (tweenLookup.ContainsKey(triggerId))
            {
                return;
            }

            tweenPresets.Add(new TweenTriggerEntry(triggerId, preset));
            RebuildLookups();
        }

        public void EnsureSfxPreset(string key, SfxPreset preset)
        {
            if (string.IsNullOrWhiteSpace(key) || preset == null)
            {
                return;
            }

            if (sfxLookup.ContainsKey(key))
            {
                return;
            }

            sfxPresets.Add(new SfxPresetEntry(key, preset));
            RebuildLookups();
        }

        private void Start()
        {
            if (autoRegisterOnEnable && !isRegistered)
            {
                Register();
            }
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnValidate()
        {
            RebuildLookups();
            ResolveListeners();
        }

        public void OnCombatEventRaised(string flagId, CombatEventContext context)
        {
            if (!isActiveAndEnabled || context == null)
            {
                return;
            }

            eventsRaised++;
            var meta = ResolveEventMeta(flagId);

            RouteTween(flagId, context, meta);
            RouteSfx(flagId, context, meta);
        }

        public void Register()
        {
            if (isRegistered)
            {
                return;
            }

            dispatcher = AnimationSystemInstaller.Current?.CombatEvents;
            if (dispatcher == null)
            {
                if (autoRegisterOnEnable)
                {
                    Debug.LogWarning($"[{LogTag}] CombatEventDispatcher not available. Router will remain disabled until installer is ready.", this);
                }
                return;
            }

            dispatcher.RegisterListener(this);
            isRegistered = true;
        }

        public void Unregister()
        {
            if (!isRegistered)
            {
                return;
            }

            dispatcher?.UnregisterListener(this);
            isRegistered = false;
        }

        [ContextMenu("Reset Counters")]
        public void ResetCounters()
        {
            eventsRaised = 0;
            tweenCacheHit = 0;
            tweenCacheMiss = 0;
            missingTween = 0;
            sfxCacheHit = 0;
            sfxCacheMiss = 0;
        }

        private void ResolveListeners()
        {
            tweenListener.ResolveRuntime(this);
            sfxListener.ResolveRuntime(this);
        }

        private void RouteTween(string flagId, CombatEventContext context, in EventMeta meta)
        {
            if (!tweenListener.CanHandle)
            {
                return;
            }

            if (!MatchesFilter(tweenListener.Filter, meta, context))
            {
                return;
            }

            if (TryGetTweenPreset(flagId, out var preset))
            {
                tweenCacheHit++;
                tweenListener.Invoke(flagId, context, preset);
                return;
            }

            tweenCacheMiss++;
            missingTween++;

            if (logMissingAssets)
            {
                BattleLogger.Warn(LogTag, $"Missing tween preset for flag '{flagId}'.");
            }
        }

        private void RouteSfx(string flagId, CombatEventContext context, in EventMeta meta)
        {
            if (!sfxListener.CanHandle)
            {
                return;
            }

            if (!MatchesFilter(sfxListener.Filter, meta, context))
            {
                return;
            }

            if (TryResolveSfxPreset(context, out var preset, out var resolvedKey))
            {
                sfxCacheHit++;
                sfxListener.Invoke(flagId, context, preset, resolvedKey);
                return;
            }

            sfxCacheMiss++;

            if (logMissingAssets)
            {
                var actionId = context.Action.ActionId ?? "(action)";
                BattleLogger.Warn(LogTag, $"Missing SFX preset for action '{actionId}' (key='{BuildPrimarySfxKey(context)}').");
            }
        }

        private bool MatchesFilter(CombatEventFilter filter, in EventMeta meta, CombatEventContext context)
        {
            if (!MatchesScope(filter.scope, meta.Scope))
            {
                return false;
            }

            if (!MatchesDirection(filter.direction, meta.Direction))
            {
                return false;
            }

            return MatchesRole(filter.role, meta.Scope, context);
        }

        private static bool MatchesScope(CombatEventScope filter, CombatEventScope actual)
        {
            if (filter == CombatEventScope.Broadcast)
            {
                return true;
            }

            if (actual == CombatEventScope.Broadcast)
            {
                return filter == CombatEventScope.Broadcast || filter == CombatEventScope.CasterOnly;
            }

            return filter == actual;
        }

        private static bool MatchesDirection(CombatEventDirection filter, CombatEventDirection actual)
        {
            return filter == CombatEventDirection.Any || filter == actual;
        }

        private static bool MatchesRole(CombatEventRole filterRole, CombatEventScope scope, CombatEventContext context)
        {
            if (filterRole == CombatEventRole.Any)
            {
                return true;
            }

            var expectedAlignment = filterRole == CombatEventRole.Ally ? CombatantAlignment.Ally : CombatantAlignment.Enemy;

            if (scope == CombatEventScope.TargetOnly)
            {
                var targets = context.Targets.All;
                if (targets == null || targets.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i].Alignment == expectedAlignment)
                    {
                        return true;
                    }
                }

                return false;
            }

            return context.Actor.Alignment == expectedAlignment;
        }

        private bool TryGetTweenPreset(string flagId, out TweenPreset preset)
        {
            return tweenLookup.TryGetValue(flagId ?? string.Empty, out preset);
        }

        private bool TryResolveSfxPreset(CombatEventContext context, out SfxPreset preset, out string resolvedKey)
        {
            var keys = BuildSfxKeys(context);
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (key == null)
                {
                    continue;
                }

                if (sfxLookup.TryGetValue(key, out preset))
                {
                    resolvedKey = key;
                    return true;
                }
            }

            preset = null;
            resolvedKey = null;
            return false;
        }

        private List<string> BuildSfxKeys(CombatEventContext context)
        {
            string family = !string.IsNullOrWhiteSpace(context.Action.Family) ? context.Action.Family : "attack/basic";
            string weapon = !string.IsNullOrWhiteSpace(context.Action.WeaponKind) ? context.Action.WeaponKind : "none";
            string element = !string.IsNullOrWhiteSpace(context.Action.Element) ? context.Action.Element : "neutral";

            return new List<string>
            {
                $"{family}:{weapon}:{element}",
                $"{family}:{weapon}:*",
                $"{family}:*:*",
                "default"
            };
        }

        private string BuildPrimarySfxKey(CombatEventContext context)
        {
            string family = !string.IsNullOrWhiteSpace(context.Action.Family) ? context.Action.Family : "attack/basic";
            string weapon = !string.IsNullOrWhiteSpace(context.Action.WeaponKind) ? context.Action.WeaponKind : "none";
            string element = !string.IsNullOrWhiteSpace(context.Action.Element) ? context.Action.Element : "neutral";
            return $"{family}:{weapon}:{element}";
        }

        private static EventMeta ResolveEventMeta(string flagId)
        {
            if (string.Equals(flagId, CombatEventFlags.Impact, StringComparison.OrdinalIgnoreCase))
            {
                return new EventMeta(CombatEventScope.TargetOnly, CombatEventDirection.Incoming);
            }

            if (string.Equals(flagId, CombatEventFlags.ActionCancel, StringComparison.OrdinalIgnoreCase))
            {
                return new EventMeta(CombatEventScope.CasterOnly, CombatEventDirection.Any);
            }

            return new EventMeta(CombatEventScope.CasterOnly, CombatEventDirection.Outgoing);
        }

        private void RebuildLookups()
        {
            tweenLookup.Clear();
            for (int i = 0; i < tweenPresets.Count; i++)
            {
                var entry = tweenPresets[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.triggerId) || entry.preset == null)
                {
                    continue;
                }

                tweenLookup[entry.triggerId] = entry.preset;
            }

            sfxLookup.Clear();
            for (int i = 0; i < sfxPresets.Count; i++)
            {
                var entry = sfxPresets[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) || entry.preset == null)
                {
                    continue;
                }

                sfxLookup[entry.key] = entry.preset;
            }
        }

        private readonly struct EventMeta
        {
            public EventMeta(CombatEventScope scope, CombatEventDirection direction)
            {
                Scope = scope;
                Direction = direction;
            }

            public CombatEventScope Scope { get; }
            public CombatEventDirection Direction { get; }
        }

        [Serializable]
        private sealed class TweenListenerBinding
        {
            public bool enabled = true;
            public MonoBehaviour listener;
            public CombatEventFilter filter = CombatEventFilter.DefaultCaster;

            [NonSerialized] private ICombatEventTweenListener runtime;

            public bool CanHandle => enabled && runtime != null;
            public CombatEventFilter Filter => filter;

            public void ResolveRuntime(Component owner)
            {
                runtime = null;
                if (!enabled || listener == null)
                {
                    return;
                }

                runtime = listener as ICombatEventTweenListener;
                if (runtime == null)
                {
                    Debug.LogWarning($"[{LogTag}] Assigned tween listener '{listener.name}' does not implement ICombatEventTweenListener.", owner);
                }
            }

            public void Invoke(string flagId, CombatEventContext context, TweenPreset preset)
            {
                runtime?.PlayTween(flagId, context, preset);
            }
        }

        [Serializable]
        private sealed class SfxListenerBinding
        {
            public bool enabled = true;
            public MonoBehaviour listener;
            public CombatEventFilter filter = CombatEventFilter.DefaultImpact;

            [NonSerialized] private ICombatEventSfxListener runtime;

            public bool CanHandle => enabled && runtime != null;
            public CombatEventFilter Filter => filter;

            public void ResolveRuntime(Component owner)
            {
                runtime = null;
                if (!enabled || listener == null)
                {
                    return;
                }

                runtime = listener as ICombatEventSfxListener;
                if (runtime == null)
                {
                    Debug.LogWarning($"[{LogTag}] Assigned SFX listener '{listener.name}' does not implement ICombatEventSfxListener.", owner);
                }
            }

            public void Invoke(string flagId, CombatEventContext context, SfxPreset preset, string resolvedKey)
            {
                runtime?.PlaySfx(flagId, context, preset, resolvedKey);
            }
        }

        [Serializable]
        private sealed class TweenTriggerEntry
        {
            public string triggerId = CombatEventFlags.Runup;
            public TweenPreset preset;

            public TweenTriggerEntry()
            {
            }

            public TweenTriggerEntry(string triggerId, TweenPreset preset)
            {
                this.triggerId = triggerId;
                this.preset = preset;
            }
        }

        [Serializable]
        private sealed class SfxPresetEntry
        {
            public string key = "default";
            public SfxPreset preset = new SfxPreset();

            public SfxPresetEntry()
            {
            }

            public SfxPresetEntry(string key, SfxPreset preset)
            {
                this.key = key;
                this.preset = preset;
            }
        }
    }
}
