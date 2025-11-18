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
        [SerializeField] private Ks1MultiWindowTimedHitRunner ks1Runner;

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
        private float resultTimer;
        private float phaseTimer;
        private bool runnerSubscribed;
        private bool ks1SequenceActive;

        private void Awake()
        {
            installer ??= AnimationSystemInstaller.Current;
            manager ??= TryFindManager();
            ks1Runner ??= GetComponent<Ks1MultiWindowTimedHitRunner>();
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
            DetachRunner();

            ClearFinalLabel();
            ClearPhaseLabel();
        }

        private void OnDestroy()
        {
            resultSubscription?.Dispose();
            resultSubscription = null;
            DetachRunner();
        }

        private void Update()
        {
            TickFinalTimer();
            TickPhaseTimer();

            if (!runnerSubscribed || ks1Runner == null || !ks1Runner.isActiveAndEnabled)
            {
                TryAttachRunner();
            }
        }

        private void Subscribe()
        {
            if (installer?.EventBus == null)
            {
                return;
            }

            resultSubscription = installer.EventBus.Subscribe<TimedHitResultEvent>(HandleTimedHitResult);
            TryAttachRunner();
        }

        private void TryAttachRunner()
        {
            if (runnerSubscribed && ks1Runner != null && ks1Runner.isActiveAndEnabled)
            {
                return;
            }

            DetachRunner();

            ks1Runner ??= GetComponent<Ks1MultiWindowTimedHitRunner>();
            if (ks1Runner == null)
            {
                manager ??= TryFindManager();
                if (manager?.TimedHitRunner is Ks1MultiWindowTimedHitRunner managerRunner)
                {
                    ks1Runner = managerRunner;
                }
            }

            if (ks1Runner != null && ks1Runner.isActiveAndEnabled)
            {
                ks1Runner.OnSequenceStarted += HandleSequenceStarted;
                ks1Runner.OnSequenceCompleted += HandleSequenceCompleted;
                ks1Runner.PhaseResolved += HandlePhaseOutcome;
                runnerSubscribed = true;
            }
        }

        private void DetachRunner()
        {
            if (ks1Runner != null && runnerSubscribed)
            {
                ks1Runner.OnSequenceStarted -= HandleSequenceStarted;
                ks1Runner.OnSequenceCompleted -= HandleSequenceCompleted;
                ks1Runner.PhaseResolved -= HandlePhaseOutcome;
            }

            runnerSubscribed = false;
            ks1SequenceActive = false;
        }

        private static BattleManagerV2 TryFindManager()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<BattleManagerV2>();
#else
            return Object.FindObjectOfType<BattleManagerV2>();
#endif
        }

        private void HandleTimedHitResult(TimedHitResultEvent evt)
        {
            if (feedbackLabel == null || !ShouldDisplayServiceResult(evt))
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
            if (ks1SequenceActive)
            {
                return false;
            }

            if (evt.WindowCount > 0)
            {
                int normalizedIndex = Math.Max(0, evt.WindowIndex);
                return normalizedIndex + 1 >= evt.WindowCount;
            }

            return true;
        }

        private void HandleSequenceStarted()
        {
            ks1SequenceActive = true;
            ClearFinalLabel();
            ClearPhaseLabel();
        }

        private void HandleSequenceCompleted(TimedHitResult result)
        {
            ks1SequenceActive = false;
            ClearPhaseLabel();

            if (enableResultDebugLogs)
            {
                Debug.Log($"[TimedHitHUD] KS1 final -> {result.Judgment} ({result.HitsSucceeded}/{Mathf.Max(1, result.TotalHits)} hits)", this);
            }

            DisplayFinalFeedback(
                $"{result.Judgment} ({Mathf.Clamp(result.HitsSucceeded, 0, result.TotalHits)}/{Mathf.Max(1, result.TotalHits)} hits)",
                result.Judgment);
        }

        private void HandlePhaseOutcome(Ks1PhaseOutcome outcome)
        {
            if (phaseFeedbackLabel == null)
            {
                return;
            }

            if (enablePhaseDebugLogs)
            {
                Debug.Log($"[KS1 HUD] Phase {outcome.PhaseIndex + 1}/{outcome.TotalPhases}: {outcome.Judgment}", this);
            }

            var phaseHits = $"{outcome.Result.HitsSucceeded}/{outcome.Result.TotalHits}";
            phaseFeedbackLabel.gameObject.SetActive(true);
            phaseFeedbackLabel.text = $"Phase {outcome.PhaseIndex + 1}/{outcome.TotalPhases}: {outcome.Judgment} ({phaseHits})";
            phaseFeedbackLabel.color = ResolveColor(outcome.Judgment);

            phaseTimer = phaseHoldSeconds > 0f ? phaseHoldSeconds : 0f;

            if (outcome.ChainCancelled || outcome.ChainCompleted)
            {
                phaseTimer = terminalPhaseHoldSeconds > 0f
                    ? terminalPhaseHoldSeconds
                    : 0f;
                if (phaseTimer <= 0f)
                {
                    ClearPhaseLabel();
                }
            }
            else if (outcome.TotalPhases <= 1)
            {
                phaseTimer = Mathf.Min(phaseTimer, terminalPhaseHoldSeconds);
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
