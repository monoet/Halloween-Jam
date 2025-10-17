using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Orchestration;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Debugging
{
    /// <summary>
    /// Immediate-mode debug overlay for charge playtesting.
    /// </summary>
    public class ChargeDebugSuite : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleManagerV2 battleManager;
        [SerializeField] private ManualBattleInputProviderV2 notchedProvider;
        [SerializeField] private HoldChargeInputProvider holdProvider;
        [SerializeField] private ManualBattleInputProviderUI uiProvider;

        [Header("Settings")]
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private string windowTitle = "Charge Debug";

        private Rect windowRect = new Rect(20, 20, 360, 260);
        private BattleSelection lastSelection;
        private int lastCpBefore;
        private int lastCpAfter;
        private TimedHitResult lastTimedHit;
        private string currentProviderName = "(default)";

        private void OnEnable()
        {
            if (battleManager != null)
            {
                battleManager.OnPlayerActionResolved += HandleActionResolved;
            }

            TimedHitModule.GlobalSequenceCompleted += HandleTimedHitCompleted;
        }

        private void OnDisable()
        {
            if (battleManager != null)
            {
                battleManager.OnPlayerActionResolved -= HandleActionResolved;
            }

            TimedHitModule.GlobalSequenceCompleted -= HandleTimedHitCompleted;
        }

        private void HandleActionResolved(BattleSelection selection, int cpBefore, int cpAfter)
        {
            lastSelection = selection;
            lastCpBefore = cpBefore;
            lastCpAfter = cpAfter;
        }

        private void HandleTimedHitCompleted(TimedHitResult result)
        {
            lastTimedHit = result;
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, windowTitle);
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label($"Provider: {currentProviderName}");
            GUILayout.Label("Controls: R=Increase/Hold, F=Decrease, Enter=Confirm, Esc=Cancel");

            if (GUILayout.Button("Use Notched Provider") && notchedProvider != null)
            {
                SetProvider(notchedProvider, "Notched");
            }

            if (GUILayout.Button("Use Hold Provider") && holdProvider != null)
            {
                SetProvider(holdProvider, "Hold");
            }

            if (GUILayout.Button("Use UI Provider") && uiProvider != null)
            {
                SetProvider(uiProvider, "UI Notched");
            }

            if (GUILayout.Button("Reset Stats"))
            {
                ResetStats();
            }

            GUILayout.Space(10);
            GUILayout.Label("Last Player Action:");
            if (lastSelection.Action != null)
            {
                GUILayout.Label($" • {lastSelection.Action.id} | CP Charge {lastSelection.CpCharge}");
                GUILayout.Label($" • CP Before: {lastCpBefore} | After: {lastCpAfter} | ? {(lastCpAfter - lastCpBefore)}");
            }
            else
            {
                GUILayout.Label(" • (no action yet)");
            }

            GUILayout.Space(10);
            GUILayout.Label("Last Timed Hit:");
            if (lastTimedHit.TotalHits > 0)
            {
                GUILayout.Label($" • Hits: {lastTimedHit.HitsSucceeded}/{lastTimedHit.TotalHits}");
                GUILayout.Label($" • Refund: {lastTimedHit.CpRefund} | Multiplier: {lastTimedHit.DamageMultiplier:F2}");
            }
            else
            {
                GUILayout.Label(" • (no timed hit)");
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void SetProvider(MonoBehaviour providerComponent, string label)
        {
            if (battleManager == null || providerComponent == null)
            {
                return;
            }

            if (notchedProvider != null)
            {
                notchedProvider.enabled = providerComponent == notchedProvider;
            }

            if (holdProvider != null)
            {
                holdProvider.enabled = providerComponent == holdProvider;
            }

            if (uiProvider != null)
            {
                uiProvider.enabled = providerComponent == uiProvider;
            }

            if (providerComponent is IBattleInputProvider provider)
            {
                battleManager.SetRuntimeInputProvider(provider);
                currentProviderName = label;
            }
        }

        private void ResetStats()
        {
            lastSelection = default;
            lastCpBefore = 0;
            lastCpAfter = 0;
            lastTimedHit = default;
        }
    }
}
