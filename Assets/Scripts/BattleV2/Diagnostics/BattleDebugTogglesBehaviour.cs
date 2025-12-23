using UnityEngine;
using BattleV2.Core;

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
        [SerializeField] private bool enableEX = false;
        [Tooltip("AP logging channel (plan/recipe-chain instrumentation).")]
        [SerializeField] private bool enableAPLogs = false;

        [Header("Diagnostics Logs (Dev Only)")]
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Tooltip("Enable CP trace logs (CPTRACE).")]
        [SerializeField] private bool enableCpTraceLogs = false;
        [Tooltip("Enable animation trace logs (ANIMTRACE).")]
        [SerializeField] private bool enableAnimTraceLogs = false;
        [Tooltip("Enable locomotion trace logs (LOCOMOTIONTRACE).")]
        [SerializeField] private bool enableLocomotionTraceLogs = false;
        [Tooltip("Enable unified flow trace logs (BATTLEFLOW).")]
        [SerializeField] private bool enableFlowTraceLogs = false;
#endif

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
            ApplyChannel("EX", enableEX);
            ApplyChannel("AP", enableAPLogs);
            ApplyChannel("APF", enableAPFeature);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BattleDiagnostics.DevCpTrace = enableCpTraceLogs;
            BattleDiagnostics.DevAnimTrace = enableAnimTraceLogs;
            BattleDiagnostics.DevLocomotionTrace = enableLocomotionTraceLogs;
            BattleDiagnostics.DevFlowTrace = enableFlowTraceLogs;
#endif
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
