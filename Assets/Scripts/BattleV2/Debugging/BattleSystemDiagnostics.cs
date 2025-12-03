using UnityEngine;
using BattleV2.Orchestration;
using BattleV2.UI;
using BattleV2.AnimationSystem.Runtime;

namespace BattleV2.Debugging
{
    public class BattleSystemDiagnostics : MonoBehaviour
    {
        [ContextMenu("Run Diagnostics")]
        public void RunDiagnostics()
        {
            // Using LogError for everything to ensure visibility in Console
            Debug.LogError("=== BATTLE SYSTEM DIAGNOSTICS (Force Error Level) ===");

            // 1. Check BattleManagerV2
            var manager = FindObjectOfType<BattleManagerV2>();
            if (manager == null)
            {
                Debug.LogError("FAIL: BattleManagerV2 not found in scene.");
            }
            else
            {
                Debug.LogError($"PASS: BattleManagerV2 found on '{manager.name}'.");
            }

            // 2. Check TimedHitOverlay
            var overlay = FindObjectOfType<TimedHitOverlay>();
            if (overlay == null)
            {
                Debug.LogError("FAIL: TimedHitOverlay not found in scene.");
            }
            else
            {
                Debug.LogError($"PASS: TimedHitOverlay found on '{overlay.name}'. Active: {overlay.gameObject.activeInHierarchy}");
            }

            // 3. Check BattleUIInputDriver
            var driver = FindObjectOfType<BattleUIInputDriver>();
            if (driver == null)
            {
                Debug.LogError("FAIL: BattleUIInputDriver not found in scene.");
            }
            else
            {
                Debug.LogError($"PASS: BattleUIInputDriver found on '{driver.name}'.");
            }

            // 4. Check AnimationSystemInstaller
            var installer = AnimationSystemInstaller.Current;
            if (installer == null)
            {
                Debug.LogError("WARNING: AnimationSystemInstaller.Current is null. Is it in the scene?");
            }
            else
            {
                Debug.LogError($"PASS: AnimationSystemInstaller active on '{installer.name}'.");
                if (installer.EventBus == null)
                {
                    Debug.LogError("FAIL: Installer has no EventBus!");
                }

                // Check Runners via Reflection (since fields are private/serialized)
                var basicRunner = installer.GetType().GetField("basicTimedHitRunner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(installer) as MonoBehaviour;
                
                if (basicRunner == null)
                {
                    Debug.LogError("FAIL: 'BasicTimedHitRunner' is NOT assigned in AnimationSystemInstaller!");
                }
                else
                {
                    Debug.LogError($"PASS: BasicTimedHitRunner assigned: '{basicRunner.name}'. Active: {basicRunner.isActiveAndEnabled}");
                    if (!basicRunner.isActiveAndEnabled)
                    {
                        Debug.LogError("FAIL: BasicTimedHitRunner is DISABLED! System will fallback to InstantRunner.");
                    }
                }
            }

            Debug.LogError("=== END DIAGNOSTICS ===");
        }

        private void Start()
        {
            RunDiagnostics();
        }
    }
}
