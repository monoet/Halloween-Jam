using System;
using System.Collections;
using System.Threading.Tasks;
using BattleV2.Charge;
using BattleV2.Orchestration;
using UnityEngine;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Runtime KS1 runner that advances the timed-hit sequence phase by phase,
    /// emitting events as input is processed so middlewares can respond in real time.
    /// </summary>
    public sealed class Ks1TimedHitRunner : MonoBehaviour, ITimedHitRunner
    {
        [SerializeField] private BattleManagerV2 manager;
        [SerializeField] private KeyCode inputKey = KeyCode.Space;
        [SerializeField, Min(0f)] private float autoMissGrace = 0.05f;
        [SerializeField] private bool fallBackToInstantWhenDisabled = true;

        public event Action OnSequenceStarted;
        public event Action<TimedHitPhaseInfo> OnPhaseStarted;
        public event Action<TimedHitPhaseResult> OnPhaseResolved;
        public event Action<TimedHitResult> OnSequenceCompleted;

        private TaskCompletionSource<TimedHitResult> pendingRun;
        private Coroutine runRoutine;
        private TimedHitRequest currentRequest;
        private bool sequenceActive;

        private void Awake()
        {
            if (manager == null)
            {
                manager = TryFindManager();
            }

            if (manager != null)
            {
                Debug.Log("[Ks1TimedHitRunner] Awake registering runner.", this);
                manager.SetTimedHitRunner(this);
            }
        }

        private void OnEnable()
        {
            if (manager == null)
            {
                manager = TryFindManager();
            }

            if (manager != null)
            {
                Debug.Log("[Ks1TimedHitRunner] OnEnable registering runner.", this);
                manager.SetTimedHitRunner(this);
            }
        }

        private static BattleManagerV2 TryFindManager()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<BattleManagerV2>();
#else
            return UnityEngine.Object.FindObjectOfType<BattleManagerV2>();
#endif
        }

        private void OnDisable()
        {
            if (sequenceActive)
            {
                AbortSequence(cancelled: true);
            }

            Debug.Log("[Ks1TimedHitRunner] OnDisable (sequenceActive cleared).");
        }

        private void OnDestroy()
        {
            if (sequenceActive)
            {
                AbortSequence(cancelled: true);
            }

            if (manager != null && ReferenceEquals(manager.TimedHitRunner, this) && fallBackToInstantWhenDisabled)
            {
                Debug.Log("[Ks1TimedHitRunner] OnDestroy falling back to instant runner.", this);
                manager.SetTimedHitRunner(null);
            }
        }

        public Task<TimedHitResult> RunAsync(TimedHitRequest request)
        {
            if (!isActiveAndEnabled)
            {
                return InstantTimedHitRunner.Shared.RunAsync(request);
            }

            if (sequenceActive)
            {
                throw new InvalidOperationException("Timed hit runner is already executing a sequence.");
            }

            currentRequest = request;
            pendingRun = new TaskCompletionSource<TimedHitResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (request.Profile == null)
            {
                var result = new TimedHitResult(0, 0, 0, 1f, cancelled: false, successStreak: 0);
                OnSequenceStarted?.Invoke();
                OnSequenceCompleted?.Invoke(result);
                pendingRun.SetResult(result);
                return pendingRun.Task;
            }

            runRoutine = StartCoroutine(RunSequence());
            return pendingRun.Task;
        }

        private IEnumerator RunSequence()
        {
            sequenceActive = true;

            var request = currentRequest;
            var token = request.CancellationToken;
            var profile = request.Profile;
            var tier = profile.GetTierForCharge(request.CpCharge);
            int totalPhases = Mathf.Max(1, tier.Hits);

            OnSequenceStarted?.Invoke();
            Debug.Log($"[Ks1TimedHitRunner] Sequence start -> CP:{request.CpCharge} totalPhases:{totalPhases}", this);

            float basePhaseDuration = tier.TimelineDuration > 0f ? tier.TimelineDuration : 1f;
            float phaseDuration = basePhaseDuration;
            float timelineDuration = basePhaseDuration * Mathf.Max(1, totalPhases);
            float resultHold = Mathf.Max(0f, tier.ResultHoldDuration);

            int perfectCount = 0;
            int goodCount = 0;
            int missCount = 0;
            bool forceMisses = false;

            for (int phaseIndex = 1; phaseIndex <= totalPhases; phaseIndex++)
            {
                if (token.IsCancellationRequested)
                {
                    AbortSequence(cancelled: true);
                    yield break;
                }

                var window = ResolveWindows(tier);
                OnPhaseStarted?.Invoke(new TimedHitPhaseInfo(
                    phaseIndex,
                    totalPhases,
                    window.SuccessWindowStart,
                    window.SuccessWindowEnd));
                Debug.Log($"[Ks1TimedHitRunner] Phase {phaseIndex}/{totalPhases} window=({window.SuccessWindowStart:0.00}-{window.SuccessWindowEnd:0.00})", this);

                float normalizedTime = 1f;
                bool autoMiss = forceMisses;

                if (!autoMiss)
                {
                    float startTime = Time.time;
                    while (true)
                    {
                        if (token.IsCancellationRequested)
                        {
                            AbortSequence(cancelled: true);
                            yield break;
                        }

                        float elapsed = phaseDuration > 0f
                            ? (Time.time - startTime) / phaseDuration
                            : float.PositiveInfinity;

                        normalizedTime = Mathf.Clamp01(elapsed);

                        if (Input.GetKeyDown(inputKey))
                        {
                            autoMiss = false;
                            break;
                        }

                        if (elapsed >= 1f + autoMissGrace)
                        {
                            autoMiss = true;
                            break;
                        }

                        yield return null;
                    }
                }

                var outcome = EvaluateOutcome(tier, window, normalizedTime, autoMiss);

                switch (outcome.Kind)
                {
                    case PhaseOutcomeKind.Perfect:
                        perfectCount++;
                        break;
                    case PhaseOutcomeKind.Good:
                        goodCount++;
                        break;
                    default:
                        missCount++;
                        forceMisses = true;
                        break;
                }

                OnPhaseResolved?.Invoke(new TimedHitPhaseResult(
                    phaseIndex,
                    outcome.Kind != PhaseOutcomeKind.Miss,
                    outcome.Multiplier,
                    outcome.AccuracyNormalized));
                Debug.Log($"[Ks1TimedHitRunner] Phase {phaseIndex} result: {outcome.Kind} mult={outcome.Multiplier:F2} acc={outcome.AccuracyNormalized:F2}", this);

                if (resultHold > 0f)
                {
                    float holdTarget = Time.time + resultHold;
                    while (Time.time < holdTarget)
                    {
                        if (token.IsCancellationRequested)
                        {
                            AbortSequence(cancelled: true);
                            yield break;
                        }
                        yield return null;
                    }
                }

                if (forceMisses)
                {
                    // Resolve remaining phases instantly as misses without waiting for input.
                    continue;
                }
            }

            var result = BuildResult(tier, perfectCount, goodCount, missCount);
            CompleteSequence(result);
            Debug.Log($"[Ks1TimedHitRunner] Sequence completed hits={result.HitsSucceeded}/{result.TotalHits} refund={result.CpRefund}", this);
        }

        private void AbortSequence(bool cancelled)
        {
            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
                runRoutine = null;
            }

            var tier = currentRequest.Profile != null
                ? currentRequest.Profile.GetTierForCharge(currentRequest.CpCharge)
                : default;
            int totalHits = Mathf.Max(0, tier.Hits);

            var result = new TimedHitResult(
                hitsSucceeded: 0,
                totalHits: totalHits,
                cpRefund: 0,
                damageMultiplier: 0f,
                cancelled: cancelled,
                successStreak: 0);

            CompleteSequence(result);
        }

        private void CompleteSequence(TimedHitResult result)
        {
            sequenceActive = false;

            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
                runRoutine = null;
            }

            pendingRun?.TrySetResult(result);
            pendingRun = null;
            currentRequest = default;

            OnSequenceCompleted?.Invoke(result);
        }

        private static PhaseOutcome EvaluateOutcome(
            Ks1TimedHitProfile.Tier tier,
            PhaseWindows window,
            float normalizedTime,
            bool autoMiss)
        {
            if (autoMiss)
            {
                float missMultiplier = tier.MissHitMultiplier > 0f ? tier.MissHitMultiplier : 0f;
                return PhaseOutcome.Miss(missMultiplier);
            }

            float delta = Mathf.Abs(normalizedTime - window.PerfectCenter);
            float accuracy = window.SuccessRadius > 0f
                ? Mathf.Clamp01(1f - (delta / window.SuccessRadius))
                : 1f;

            if (delta <= window.PerfectRadius)
            {
                float perfectMultiplier = tier.PerfectHitMultiplier > 0f ? tier.PerfectHitMultiplier : 1.5f;
                return PhaseOutcome.Perfect(perfectMultiplier, accuracy);
            }

            if (delta <= window.SuccessRadius)
            {
                float successMultiplier = tier.SuccessHitMultiplier > 0f ? tier.SuccessHitMultiplier : 1f;
                return PhaseOutcome.Good(successMultiplier, accuracy);
            }

            float missMult = tier.MissHitMultiplier > 0f ? tier.MissHitMultiplier : 0f;
            return PhaseOutcome.Miss(missMult);
        }

        private static TimedHitResult BuildResult(
            Ks1TimedHitProfile.Tier tier,
            int perfectCount,
            int goodCount,
            int missCount)
        {
            int totalHits = Mathf.Max(0, perfectCount + goodCount + missCount);
            int hitsSucceeded = Mathf.Max(0, perfectCount + goodCount);
            int refund = Mathf.Clamp(hitsSucceeded, 0, tier.RefundMax);

            float multiplier = CalculateCombinedMultiplier(tier, perfectCount, goodCount);

            return new TimedHitResult(
                hitsSucceeded,
                totalHits,
                refund,
                multiplier,
                cancelled: false,
                successStreak: hitsSucceeded);
        }

        private static float CalculateCombinedMultiplier(
            Ks1TimedHitProfile.Tier tier,
            int perfectCount,
            int goodCount)
        {
            int successCount = Mathf.Max(0, perfectCount + goodCount);
            if (successCount == 0)
            {
                return 0f;
            }

            float perfectMultiplier = tier.PerfectHitMultiplier > 0f ? tier.PerfectHitMultiplier : 1.5f;
            float successMultiplier = tier.SuccessHitMultiplier > 0f ? tier.SuccessHitMultiplier : 1f;
            float tierMultiplier = tier.DamageMultiplier > 0f ? tier.DamageMultiplier : 1f;

            float totalContribution = perfectCount * perfectMultiplier + goodCount * successMultiplier;
            float averageContribution = totalContribution / successCount;
            return averageContribution * tierMultiplier;
        }

        private static PhaseWindows ResolveWindows(Ks1TimedHitProfile.Tier tier)
        {
            float perfectCenter = (tier.PerfectWindowCenter <= 0f && tier.PerfectWindowRadius <= 0f)
                ? 0.5f
                : Mathf.Clamp01(tier.PerfectWindowCenter);
            float perfectRadius = Mathf.Max(0f, tier.PerfectWindowRadius);
            float successRadius = Mathf.Max(perfectRadius, tier.SuccessWindowRadius);

            float successStart = Mathf.Clamp01(perfectCenter - successRadius);
            float successEnd = Mathf.Clamp01(perfectCenter + successRadius);

            return new PhaseWindows(perfectCenter, perfectRadius, successRadius, successStart, successEnd);
        }

        private readonly struct PhaseWindows
        {
            public PhaseWindows(float perfectCenter, float perfectRadius, float successRadius, float successWindowStart, float successWindowEnd)
            {
                PerfectCenter = perfectCenter;
                PerfectRadius = perfectRadius;
                SuccessRadius = successRadius;
                SuccessWindowStart = successWindowStart;
                SuccessWindowEnd = successWindowEnd;
            }

            public float PerfectCenter { get; }
            public float PerfectRadius { get; }
            public float SuccessRadius { get; }
            public float SuccessWindowStart { get; }
            public float SuccessWindowEnd { get; }
        }

        private readonly struct PhaseOutcome
        {
            private PhaseOutcome(PhaseOutcomeKind kind, float multiplier, float accuracyNormalized)
            {
                Kind = kind;
                Multiplier = multiplier;
                AccuracyNormalized = Mathf.Clamp01(accuracyNormalized);
            }

            public PhaseOutcomeKind Kind { get; }
            public float Multiplier { get; }
            public float AccuracyNormalized { get; }

            public static PhaseOutcome Perfect(float multiplier, float accuracyNormalized) =>
                new PhaseOutcome(PhaseOutcomeKind.Perfect, multiplier, accuracyNormalized);

            public static PhaseOutcome Good(float multiplier, float accuracyNormalized) =>
                new PhaseOutcome(PhaseOutcomeKind.Good, multiplier, accuracyNormalized);

            public static PhaseOutcome Miss(float multiplier) =>
                new PhaseOutcome(PhaseOutcomeKind.Miss, multiplier, 0f);
        }

        private enum PhaseOutcomeKind
        {
            Perfect,
            Good,
            Miss
        }
    }
}



