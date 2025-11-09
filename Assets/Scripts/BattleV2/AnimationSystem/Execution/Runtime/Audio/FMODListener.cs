using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    /// <summary>
    /// Minimal FMOD bridge for combat event SFX. Falls back to debug logs if FMOD is not present.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FMODListener : MonoBehaviour, ICombatEventSfxListener
    {
        [SerializeField] private bool logWhenUnavailable = true;

        public void PlaySfx(string flagId, CombatEventContext context, SfxPreset preset, string resolvedKey)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.eventPath))
            {
                return;
            }

#if FMOD_PRESENT
            if (string.Equals(flagId, CombatEventFlags.ActionCancel, System.StringComparison.OrdinalIgnoreCase))
            {
                // Future: stop sustained instances when we support looping events.
                return;
            }
#else
            if (string.Equals(flagId, CombatEventFlags.ActionCancel, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
#endif

#if FMOD_PRESENT
            var instance = FMODUnity.RuntimeManager.CreateInstance(preset.eventPath);
            if (!instance.isValid())
            {
                if (logWhenUnavailable)
                {
                    Debug.LogWarning($"[FMODListener] Failed to create instance for '{preset.eventPath}'.", this);
                }
                return;
            }

            if (preset.pitchVariance > 0f)
            {
                float variance = Random.Range(-preset.pitchVariance, preset.pitchVariance);
                instance.setPitch(1f + variance);
            }

            if (preset.volume < 1f - Mathf.Epsilon)
            {
                instance.setVolume(Mathf.Max(0f, preset.volume));
            }

            instance.start();
            instance.release();
#else
            if (logWhenUnavailable)
            {
                Debug.LogWarning($"[FMODListener] FMOD_PRESENT define missing. Cannot play '{preset.eventPath}' (flag '{flagId}', key '{resolvedKey ?? "(unknown)"}').", this);
            }
#endif
        }
    }
}
