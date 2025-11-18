using System;
using System.Collections.Generic;
using UnityEngine;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using BattleV2.AnimationSystem.Runtime;
using AnimCombatEventContext = BattleV2.AnimationSystem.Execution.Runtime.CombatEvents.CombatEventContext;

namespace BattleV2.Audio
{
    /// <summary>
    /// MVP audio controller: listens to combat flags and plays 2D SFX/music.
    /// Audio-only; no gameplay logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleAudioController : MonoBehaviour, ICombatEventListener
    {
        [Header("Database")]
        [SerializeField] private BattleAudioDatabase database;

        [Header("Registration")]
        [SerializeField, Tooltip("If enabled, registers to CombatEventDispatcher on enable.")]
        private bool autoRegisterOnEnable = true;

        [Header("Logging")]
        [SerializeField] private bool logWhenFmodUnavailable = true;
        [SerializeField] private bool logMissingEntryOnce = true;

        private readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly HashSet<string> missingLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private CombatEventDispatcher dispatcher;
        private bool isRegistered;

        private void Awake()
        {
            if (database == null)
            {
                Debug.LogWarning("[BattleAudio] Database not assigned; audio controller will be inert.", this);
            }
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            ValidateAnchorsInScene();
#endif
        }

        private void Start()
        {
            if (!isRegistered && autoRegisterOnEnable)
            {
                TryRegister();
            }
        }

        private void OnDisable() { Unregister(); }

        /// <summary>
        /// Entry point from CombatEventDispatcher (AnimationSystem). Converts to audio context.
        /// </summary>
        public void OnCombatEventRaised(string flagId, AnimCombatEventContext context)
        {
            if (database == null || string.IsNullOrWhiteSpace(flagId))
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var map = database.SfxByFlagId;
            bool hasSfx = map != null && map.ContainsKey(flagId);
            Debug.Log($"[BattleAudio] Received flag='{flagId}' (hasSfx={hasSfx})", this);
#endif

            var audioCtx = ConvertContext(context);

            if (!database.TryGetSfx(flagId, out var entry))
            {
                MaybeLogMissing(flagId);
                return;
            }

            string cooldownKey = flagId;
            float now = Time.realtimeSinceStartup * 1000f; // ms
            if (entry.MinIntervalMs > 0 && cooldowns.TryGetValue(cooldownKey, out var nextAllowedMs) && now < nextAllowedMs)
            {
                return;
            }
            cooldowns[cooldownKey] = now + entry.MinIntervalMs;

#if FMOD_PRESENT
            try
            {
                var instance = FMODUnity.RuntimeManager.CreateInstance(entry.EventPath);

                SetParam(instance, entry.WeaponParamName, (float)audioCtx.Weapon);
                SetParam(instance, entry.ElementParamName, (float)audioCtx.Element);
                SetParam(instance, entry.CritParamName, audioCtx.IsCrit ? 1f : 0f);
                SetParam(instance, entry.TargetsParamName, Mathf.Max(1, audioCtx.TargetCount));

                instance.start();
                instance.release();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BattleAudio] Failed to play SFX '{flagId}': {ex}", this);
            }
#else
            if (logWhenFmodUnavailable)
            {
                Debug.Log($"[BattleAudio] FMOD not present; would play '{flagId}' ({entry.EventPath}), weapon={audioCtx.Weapon}, element={audioCtx.Element}, crit={audioCtx.IsCrit}, targets={audioCtx.TargetCount}", this);
            }
#endif
        }

        private CombatEventContext ConvertContext(AnimCombatEventContext context)
        {
            if (context == null)
            {
                return new CombatEventContext(WeaponFamily.None, ElementId.None, false, 1);
            }

            var weapon = MapWeapon(context.Action.WeaponKind);
            var element = MapElement(context.Action.Element);
            bool isCrit = IsCrit(context.Tags);
            int targets = context.Targets.Count;

            return new CombatEventContext(weapon, element, isCrit, targets);
        }

#if FMOD_PRESENT
        private static void SetParam(FMOD.Studio.EventInstance instance, string name, float value)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                instance.setParameterByName(name, value);
            }
        }
#endif

        // TODO(MVP+): Hook combat start/end to MusicConfig snapshots
        // (turn phase routing will live here)

        private void MaybeLogMissing(string flagId)
        {
            if (!logMissingEntryOnce)
            {
                Debug.LogWarning($"[BattleAudio] Missing SFX entry for flag '{flagId}'.", this);
                return;
            }

            if (missingLogged.Add(flagId))
            {
                Debug.LogWarning($"[BattleAudio] Missing SFX entry for flag '{flagId}'. (Logged once)", this);
            }
        }

        // Attempt registration with dispatcher now that installer might be ready
        private void TryRegister()
        {
            if (isRegistered)
            {
                return;
            }

            dispatcher ??= AnimationSystemInstaller.Current?.CombatEvents;
            if (dispatcher == null)
            {
                Debug.LogWarning("[BattleAudio] CombatEventDispatcher not available; will retry on next enable.", this);
                return;
            }

            dispatcher.RegisterListener(this);
            isRegistered = true;
        }

        private void Unregister()
        {
            if (!isRegistered)
            {
                return;
            }

            dispatcher?.UnregisterListener(this);
            isRegistered = false;
        }

#if UNITY_EDITOR
        private static void ValidateAnchorsInScene()
        {
            // Warn for CombatantState without a MotionAnchor in children.
            var combatants = FindObjectsOfType<CombatantState>();
            for (int i = 0; i < combatants.Length; i++)
            {
                var cs = combatants[i];
                if (cs == null)
                {
                    continue;
                }

                var anchor = cs.GetComponentInChildren<BattleV2.AnimationSystem.Execution.Runtime.Anchors.MotionAnchor>(true);
                if (anchor == null)
                {
                    Debug.LogWarning($"[BattleAudio] CombatantState '{cs.name}' is missing a MotionAnchor child. 3D audio/VFX anchoring will fall back or fail.", cs);
                }
            }
        }
#endif

        private static WeaponFamily MapWeapon(string weaponKind)
        {
            if (string.IsNullOrWhiteSpace(weaponKind))
            {
                return WeaponFamily.None;
            }

            switch (weaponKind.Trim().ToLowerInvariant())
            {
                case "sword": return WeaponFamily.Sword;
                case "heavysword":
                case "greatsword":
                    return WeaponFamily.HeavySword;
                case "dagger": return WeaponFamily.Dagger;
                case "staff": return WeaponFamily.Staff;
                case "mace": return WeaponFamily.Mace;
                case "fist":
                case "unarmed":
                    return WeaponFamily.Fist;
                case "bow": return WeaponFamily.Bow;
                case "gun":
                case "rifle":
                    return WeaponFamily.Gun;
                case "thrown":
                case "throwable":
                    return WeaponFamily.Thrown;
                default: return WeaponFamily.None;
            }
        }

        private static ElementId MapElement(string element)
        {
            if (string.IsNullOrWhiteSpace(element))
            {
                return ElementId.None;
            }

            switch (element.Trim().ToLowerInvariant())
            {
                case "moon": return ElementId.Moon;
                case "sun": return ElementId.Sun;
                case "mind": return ElementId.Mind;
                case "form": return ElementId.Form;
                case "chaos": return ElementId.Chaos;
                case "forge": return ElementId.Forge;
                case "axis": return ElementId.Axis;
                default: return ElementId.None;
            }
        }

        private static bool IsCrit(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], "crit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tags[i], "critical", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
