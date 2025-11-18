using System;
using BattleV2.Charge;
using BattleV2.Execution.TimedHits;
using BattleV2.Orchestration;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Displays timed-hit feedback both for the global result and for KS1 per-phase outcomes.
    /// </summary>
    public sealed class TimedHitHudBridge : MonoBehaviour
    {
        [SerializeField] private AnimationSystemInstaller installer;
        [SerializeField] private BattleManagerV2 manager;

        [Header("Final Result")]
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField, Min(0f)] private float holdSeconds = 1f;

        [Header("Per-Phase Feedback")]
        [SerializeField] private TMP_Text phaseFeedbackLabel;
        [SerializeField, Min(0f)] private float phaseHoldSeconds = 0.5f;
        [SerializeField, Min(0f)] private float terminalPhaseHoldSeconds = 0.1f;

        [Header("Colors")]
        [SerializeField] private Color perfectColor = new(0.1f, 0.9f, 0.2f);
        [SerializeField] private Color goodColor = new(0.9f, 0.8f, 0.1f);
        [SerializeField] private Color missColor = new(0.9f, 0.2f, 0.2f);

        [Header("Debug")]
        [SerializeField] private bool enableResultDebugLogs;
        [SerializeField] private bool enablePhaseDebugLogs;

        private IDisposable resultSubscription;
        private IDisposable phaseSubscription;
        private float resultTimer;
        private float phaseTimer;

        private void Awake()
        {
            installer ??= AnimationSystemInstaller.Current;
            manager ??= TryFindManager();
        }

        private void OnEnable()
        {
            ClearFinalLabel();
            ClearPhaseLabel();
            Invoke(nameof(Subscribe), 0f);
        }

        private void OnDisable()
        {
            resultSubscription?.Dispose();
            resultSubscription = null;
            phaseSubscription?.Dispose();
            phaseSubscription = null;

            ClearFinalLabel();
            ClearPhaseLabel();
        }

        private void OnDestroy()
        {
            resultSubscription?.Dispose();
            resultSubscription = null;
            phaseSubscription?.Dispose();
            phaseSubscription = null;
        }

        private void Update()
        {
            TickFinalTimer();
            TickPhaseTimer();
        }

        private void Subscribe()
        {
            if (installer?.EventBus == null)
            {
                return;
            }

            resultSubscription = installer.EventBus.Subscribe<TimedHitResultEvent>(HandleTimedHitResult);
            phaseSubscription = installer.EventBus.Subscribe<TimedHitPhaseEvent>(HandlePhaseEvent);
        }

        private static BattleManagerV2 TryFindManager()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<BattleManagerV2>();
#else
            return Object.FindObjectOfType<BattleManagerV2>();
#endif
        }

        private bool IsPlayerActor(CombatantState actor)
        {
            if (actor == null)
            {
                return false;
            }

            manager ??= TryFindManager();
            return manager != null && actor == manager.PrimaryPlayer;
        }

        private void HandleTimedHitResult(TimedHitResultEvent evt)
        {
            if (feedbackLabel == null || !IsPlayerActor(evt.Actor) || !ShouldDisplayServiceResult(evt))
            {
                return;
            }

            if (enableResultDebugLogs)
            {
                Debug.Log($"[TimedHitHUD] Result (service) -> J={evt.Judgment}, window {evt.WindowIndex}/{evt.WindowCount}", this);
            }

            DisplayFinalFeedback(evt.Judgment.ToString(), evt.Judgment);
        }

        private bool ShouldDisplayServiceResult(TimedHitResultEvent evt)
        {
            int total = evt.WindowCount > 0 ? evt.WindowCount : 1;
            int index = evt.WindowIndex;
            if (index <= 0)
            {
                index = total;
            }

            return index >= total;
        }

        private void HandlePhaseEvent(TimedHitPhaseEvent evt)
        {
            if (phaseFeedbackLabel == null || !IsPlayerActor(evt.Actor))
            {
                return;
            }

            if (evt.PhaseIndex <= 1 && (evt.Cancelled || evt.IsFinal || evt.TotalPhases <= 1))
            {
                ClearFinalLabel();
            }

            if (enablePhaseDebugLogs)
            {
                Debug.Log($"[TimedHitHUD] Phase {evt.PhaseIndex}/{evt.TotalPhases}: {evt.Judgment} (cancelled={evt.Cancelled}, final={evt.IsFinal})", this);
            }

            phaseFeedbackLabel.gameObject.SetActive(true);
            int total = Mathf.Max(1, evt.TotalPhases);
            int index = Mathf.Clamp(evt.PhaseIndex, 1, total);
            phaseFeedbackLabel.text = $"Phase {index}/{total}: {evt.Judgment}";
            phaseFeedbackLabel.color = ResolveColor(evt.Judgment);

            phaseTimer = phaseHoldSeconds > 0f ? phaseHoldSeconds : 0f;

            if (evt.Cancelled || evt.IsFinal)
            {
                phaseTimer = terminalPhaseHoldSeconds > 0f ? terminalPhaseHoldSeconds : 0f;
                if (phaseTimer <= 0f)
                {
                    ClearPhaseLabel();
                }
            }
        }

        private void DisplayFinalFeedback(string message, TimedHitJudgment judgment)
        {
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.gameObject.SetActive(true);
            feedbackLabel.text = message;
            feedbackLabel.color = ResolveColor(judgment);
            resultTimer = holdSeconds;
        }

        private void TickFinalTimer()
        {
            if (feedbackLabel == null || holdSeconds <= 0f || resultTimer <= 0f)
            {
                return;
            }

            resultTimer -= Time.deltaTime;
            if (resultTimer <= 0f)
            {
                ClearFinalLabel();
            }
        }

        private void TickPhaseTimer()
        {
            if (phaseFeedbackLabel == null || phaseHoldSeconds <= 0f || phaseTimer <= 0f)
            {
                return;
            }

            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                ClearPhaseLabel();
            }
        }

        private Color ResolveColor(TimedHitJudgment judgment)
        {
            return judgment switch
            {
                TimedHitJudgment.Perfect => perfectColor,
                TimedHitJudgment.Good => goodColor,
                _ => missColor
            };
        }

        private void ClearFinalLabel()
        {
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.text = string.Empty;
            resultTimer = 0f;
        }

        private void ClearPhaseLabel()
        {
            if (phaseFeedbackLabel == null)
            {
                return;
            }

            phaseFeedbackLabel.text = string.Empty;
            phaseTimer = 0f;
        }
    }
}
