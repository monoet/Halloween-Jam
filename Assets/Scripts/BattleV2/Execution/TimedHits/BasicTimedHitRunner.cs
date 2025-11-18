using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.Charge;
using UnityEngine;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Single-window runner for basic attacks. Listens to direct input and produces one TimedHitResult.
    /// </summary>
    public sealed class BasicTimedHitRunner : MonoBehaviour, ITimedHitRunner
    {
        [SerializeField] private AnimationSystemInstaller installer;
        [SerializeField] private KeyCode inputKey = KeyCode.Space;
        [SerializeField] private bool enableDebugLogs;

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
                Debug.Log("[BasicTimedHitRunner] Sequence started.", this);
            }

            OnSequenceStarted?.Invoke();
            OnPhaseStarted?.Invoke(new TimedHitPhaseInfo(1, 1, 0f, 1f));
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

            bool pressed = Input.GetKeyDown(inputKey);
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
            PublishEvent(judgment, elapsedMs, consumedInput, resolutionTimestamp);

            Complete(result);
        }

        private void PublishEvent(TimedHitJudgment judgment, double deltaMs, bool consumedInput, double resolutionTimestamp)
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
                windowIndex: 0,
                windowCount: 1,
                consumedInput,
                openedAt,
                resolutionTimestamp);

            bus.Publish(evt);

            if (enableDebugLogs)
            {
                Debug.Log($"[BasicTimedHitRunner] Event -> {judgment} (Δ={deltaMs:0.#}ms).", this);
            }
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
                    0,
                    1,
                    isFinal: true,
                    cpRefund: 0,
                    cancelled: cancelled,
                    successStreak: 0);

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
