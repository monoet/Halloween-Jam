using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using BattleV2.AnimationSystem.Runtime;
using UnityEngine;

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
            if (autoRegisterOnEnable)
            {
                TryRegister();
            }
        }

        private void OnDisable()
        {
            Unregister();
        }

        /// <summary>
        /// Receives audio-only context. IMPORTANT: Do NOT use CombatEventContext from AnimationSystem.
        /// This explicitly depends on BattleV2.Audio.CombatEventContext.
        /// </summary>
        public void OnCombatEventRaised(string flagId, CombatEventContext context)
        {
            if (database == null || string.IsNullOrWhiteSpace(flagId))
                return;

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

                SetParam(instance, entry.WeaponParamName, (float)context.Weapon);
                SetParam(instance, entry.ElementParamName, (float)context.Element);
                SetParam(instance, entry.CritParamName, context.IsCrit ? 1f : 0f);
                SetParam(instance, entry.TargetsParamName, Mathf.Max(1, context.TargetCount));

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
                Debug.Log($"[BattleAudio] FMOD not present; would play '{flagId}' ({entry.EventPath}), weapon={context.Weapon}, element={context.Element}, crit={context.IsCrit}, targets={context.TargetCount}", this);
            }
#endif
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
    }
}
