using UnityEngine;

namespace BattleV2.Diagnostics
{
    /// <summary>
    /// Simple inspector-driven switchboard for BattleDebug channels.
    /// Attach to any always-on object (e.g., AnimationSystemInstaller root).
    /// </summary>
    public sealed class BattleDebugTogglesBehaviour : MonoBehaviour
    {
        [Header("Enable Channels")]
        [SerializeField] private bool enableEG = false;
        [SerializeField] private bool enableMS = false;
        [SerializeField] private bool enableRTO = false;
        [SerializeField] private bool enableSS = false;
        [Tooltip("AP logging channel (plan/recipe-chain instrumentation).")]
        [SerializeField] private bool enableAPLogs = false;

        [Header("Experimental Feature Flags")]
        [Tooltip("Enables experimental multi-recipe injection (e.g. move_to_target before basic_attack).")]
        [SerializeField] private bool enableAPFeature = false;

        [Header("Persistence (PlayerPrefs)")]
        [Tooltip("Persist the inspector values into PlayerPrefs (main-thread only).")]
        [SerializeField] private bool persistToPlayerPrefs = true;

        private void Awake()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                Apply();
            }
        }
#endif

        [ContextMenu("Apply Debug Toggles")]
        public void Apply()
        {
            ApplyChannel("EG", enableEG);
            ApplyChannel("MS", enableMS);
            ApplyChannel("RTO", enableRTO);
            ApplyChannel("SS", enableSS);
            ApplyChannel("AP", enableAPLogs);
            ApplyChannel("APF", enableAPFeature);
        }

        private void ApplyChannel(string channel, bool enabled)
        {
            if (persistToPlayerPrefs)
            {
                BattleDebug.SetEnabled(channel, enabled, persist: true);
            }
            else
            {
                BattleDebug.SetEnabled(channel, enabled, persist: false);
            }
        }
    }
}
