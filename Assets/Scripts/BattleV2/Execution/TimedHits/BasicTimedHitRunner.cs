using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Charge;
using BattleV2.Core;
using UnityEngine;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Single-window runner for basic attacks. Listens to direct input and produces one TimedHitResult.
    /// </summary>
    public sealed class BasicTimedHitRunner : MonoBehaviour, ITimedHitRunner
    {
        [SerializeField] private AnimationSystemInstaller installer;
        [SerializeField] private KeyCode inputKey = KeyCode.Space;
        [SerializeField] private bool enableDebugLogs = true;

        private TaskCompletionSource<TimedHitResult> pendingRun;
        private TimedHitRequest activeRequest;
        private BasicTimedHitProfile activeProfile;
        private bool awaitingInput;
        private bool sequenceActive;
        private double windowOpenedAt;
        private CancellationTokenRegistration cancellationRegistration;

        public event Action OnSequenceStarted;
        public event Action<TimedHitPhaseInfo> OnPhaseStarted;
        public event Action<TimedHitPhaseResult> OnPhaseResolved;
        public event Action<TimedHitResult> OnSequenceCompleted;

        private void Awake()
        {
            installer ??= AnimationSystemInstaller.Current;
        }

        private void OnDisable()
        {
            AbortSequence(cancelled: true);
        }

        public Task<TimedHitResult> RunAsync(TimedHitRequest request)
        {
            if (!isActiveAndEnabled || request.BasicProfile == null)
            {
                return InstantTimedHitRunner.Shared.RunAsync(request);
            }

            if (sequenceActive)
            {
                throw new InvalidOperationException("BasicTimedHitRunner is already executing a timed hit.");
            }

            installer ??= AnimationSystemInstaller.Current;

            activeRequest = request;
            activeProfile = request.BasicProfile;
            pendingRun = new TaskCompletionSource<TimedHitResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            sequenceActive = true;
            awaitingInput = true;
            windowOpenedAt = Time.timeAsDouble;
            cancellationRegistration = request.CancellationToken.Register(() => AbortSequence(true));

            if (enableDebugLogs)
            {
                Debug.Log($"[BasicTimedHitRunner] Sequence started. Bus Hash: {installer?.EventBus?.GetHashCode()}", this);
            }

            OnSequenceStarted?.Invoke();
            OnPhaseStarted?.Invoke(new TimedHitPhaseInfo(1, 1, 0f, 1f));

            // Publish Window Open Event
            if (installer?.EventBus != null)
            {
                float durationSeconds = (float)activeProfile.WindowTimeoutMs / 1000f;
                // Construct payload with duration
                string payload = $"duration={durationSeconds};perfect=0.0"; // Basic hit usually starts immediately, perfect at 0? Or maybe perfect is at start? 
                // Actually BasicTimedHitRunner logic: "elapsedMs <= activeProfile.PerfectThresholdMs" means perfect is at 0.
                
                installer.EventBus.Publish(new AnimationWindowEvent(
                    request.Attacker,
                    activeProfile.EventTag,
                    payload,
                    0f,
                    1f, // Normalized Window End
                    isOpening: true,
                    windowIndex: 0,
                    windowCount: 1));
            }

            return pendingRun.Task;
        }

        private void Update()
        {
            if (!sequenceActive || !awaitingInput || activeProfile == null)
            {
                return;
            }

            double now = Time.timeAsDouble;
            double elapsedMs = (now - windowOpenedAt) * 1000d;

            bool pressed = false;
            // Try consume from service provider first
            if (installer?.TimedHitService != null && installer.TimedHitService.TryConsumeInput(out var timestamp))
            {
                pressed = true;
                // Use the timestamp from provider if needed, but for BasicRunner we mostly care about "now" vs window start.
                // Ideally we should use (timestamp - windowOpenedAt) but for simplicity and safety with frame alignment:
                now = timestamp; 
                elapsedMs = (now - windowOpenedAt) * 1000d;
            }
            else if (Input.GetKeyDown(inputKey))
            {
                pressed = true;
            }

            bool timedOut = elapsedMs >= activeProfile.WindowTimeoutMs;

            if (!pressed && !timedOut)
            {
                return;
            }

            awaitingInput = false;
            var judgment = pressed ? EvaluateJudgment((float)elapsedMs) : TimedHitJudgment.Miss;
            ResolveSequence(judgment, elapsedMs, pressed, now);
        }

        private TimedHitJudgment EvaluateJudgment(float elapsedMs)
        {
            if (elapsedMs <= activeProfile.PerfectThresholdMs)
            {
                return TimedHitJudgment.Perfect;
            }

            if (elapsedMs <= activeProfile.GoodThresholdMs)
            {
                return TimedHitJudgment.Good;
            }

            return TimedHitJudgment.Miss;
        }

        private float EvaluateMultiplier(TimedHitJudgment judgment)
        {
            return judgment switch
            {
                TimedHitJudgment.Perfect => activeProfile.PerfectMultiplier,
                TimedHitJudgment.Good => activeProfile.GoodMultiplier,
                _ => activeProfile.MissMultiplier
            };
        }

        private void ResolveSequence(TimedHitJudgment judgment, double elapsedMs, bool consumedInput, double resolutionTimestamp)
        {
            int phaseIndex = 1;
            int hitsSucceeded = judgment == TimedHitJudgment.Miss ? 0 : 1;
            float multiplier = EvaluateMultiplier(judgment);
            var result = new TimedHitResult(
                judgment,
                hitsSucceeded,
                totalHits: 1,
                multiplier,
                phaseIndex: phaseIndex,
                totalPhases: 1,
                isFinal: true,
                cpRefund: hitsSucceeded > 0 ? activeProfile.ComboPointReward : 0,
                cancelled: false,
                successStreak: hitsSucceeded);

            float accuracy = judgment switch
            {
                TimedHitJudgment.Perfect => 1f,
                TimedHitJudgment.Good => 0.75f,
                _ => 0f
            };

            OnPhaseResolved?.Invoke(new TimedHitPhaseResult(
                phaseIndex,
                hitsSucceeded > 0,
                multiplier,
                accuracy,
                activeRequest.Attacker));
            PublishPhaseEvent(judgment, accuracy, phaseIndex, isFinal: true, cancelled: false, totalPhases: 1);
            PublishResultEvent(judgment, elapsedMs, consumedInput, resolutionTimestamp, phaseIndex, totalPhases: 1);

            Complete(result);

            // Publish Window Close Event
            if (installer?.EventBus != null)
            {
                installer.EventBus.Publish(new AnimationWindowEvent(
                    activeRequest.Attacker,
                    activeProfile.EventTag,
                    "Basic",
                    (float)elapsedMs / 1000f,
                    (float)activeProfile.WindowTimeoutMs / 1000f,
                    isOpening: false,
                    windowIndex: 0,
                    windowCount: 1));
            }
        }

        private void PublishResultEvent(
            TimedHitJudgment judgment,
            double deltaMs,
            bool consumedInput,
            double resolutionTimestamp,
            int phaseIndex,
            int totalPhases)
        {
            var bus = installer?.EventBus;
            if (bus == null || activeRequest.Attacker == null)
            {
                return;
            }

            double inputTimestamp = resolutionTimestamp;
            double openedAt = windowOpenedAt;
            var evt = new TimedHitResultEvent(
                activeRequest.Attacker,
                activeProfile.EventTag,
                judgment,
                deltaMs,
                inputTimestamp,
                windowIndex: Mathf.Max(1, phaseIndex),
                windowCount: Mathf.Max(1, totalPhases),
                consumedInput,
                openedAt,
                resolutionTimestamp,
                TimedHitResultScope.Final,
                weaponKind: "none",
                element: "neutral",
                isCritical: false,
                targetCount: 1);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevCpTrace)
            {
                BattleDiagnostics.Log(
                    "CPTRACE",
                    $"TH_RESULT_FINAL exec={activeRequest.ExecutionId} judgment={judgment} deltaMs={deltaMs:0.#} phase={phaseIndex}/{totalPhases} consumed={consumedInput}",
                    activeRequest.Attacker);
            }
            if (BattleDiagnostics.DevFlowTrace)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"TH_RESULT_FINAL exec={activeRequest.ExecutionId} judgment={judgment} deltaMs={deltaMs:0.#} phase={phaseIndex}/{totalPhases} consumed={consumedInput}",
                    activeRequest.Attacker);
            }
#endif
            bus.Publish(evt);

            if (enableDebugLogs)
            {
                Debug.Log($"[BasicTimedHitRunner] Event -> {judgment} (dt={deltaMs:0.#}ms).", this);
            }
        }

        private void PublishPhaseEvent(
            TimedHitJudgment judgment,
            float accuracy,
            int phaseIndex,
            bool isFinal,
            bool cancelled,
            int totalPhases)
        {
            var bus = installer?.EventBus;
            if (bus == null || activeRequest.Attacker == null)
            {
                return;
            }

            bus.Publish(new TimedHitPhaseEvent(
                activeRequest.Attacker,
                judgment,
                accuracy,
                Mathf.Max(1, phaseIndex),
                Mathf.Max(1, totalPhases),
                cancelled,
                isFinal));
        }

        private void AbortSequence(bool cancelled)
        {
            if (!sequenceActive)
            {
                return;
            }

            awaitingInput = false;
            cancellationRegistration.Dispose();

            if (pendingRun != null && !pendingRun.Task.IsCompleted)
            {
                var fallback = new TimedHitResult(
                    TimedHitJudgment.Miss,
                    0,
                    1,
                    activeProfile != null ? activeProfile.MissMultiplier : 0f,
                    1,
                    1,
                    isFinal: true,
                    cpRefund: 0,
                    cancelled: cancelled,
                    successStreak: 0);

                PublishPhaseEvent(TimedHitJudgment.Miss, 0f, phaseIndex: 1, isFinal: true, cancelled: cancelled, totalPhases: 1);
                PublishResultEvent(TimedHitJudgment.Miss, deltaMs: activeProfile != null ? activeProfile.WindowTimeoutMs : 0d, consumedInput: false, resolutionTimestamp: Time.timeAsDouble, phaseIndex: 1, totalPhases: 1);
                pendingRun.TrySetResult(fallback);
                OnSequenceCompleted?.Invoke(fallback);
            }

            ClearState();
        }

        private void Complete(TimedHitResult result)
        {
            awaitingInput = false;
            cancellationRegistration.Dispose();
            pendingRun?.TrySetResult(result);
            OnSequenceCompleted?.Invoke(result);
            ClearState();
        }

        private void ClearState()
        {
            sequenceActive = false;
            awaitingInput = false;
            activeProfile = null;
            activeRequest = default;
            pendingRun = null;
        }
    }
}
