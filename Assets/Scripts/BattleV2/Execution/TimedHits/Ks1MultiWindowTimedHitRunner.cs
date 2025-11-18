using System;
using System.Collections;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.Charge;
using BattleV2.Orchestration;
using UnityEngine;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Experimental runner that emits per-phase outcomes for KS1 chains.
    /// </summary>
    public sealed class Ks1MultiWindowTimedHitRunner : MonoBehaviour, ITimedHitRunner
    {
        [SerializeField] private BattleManagerV2 manager;
        [SerializeField] private KeyCode inputKey = KeyCode.Space;
        [SerializeField, Min(0f)] private float autoMissGrace = 0.05f;
        [SerializeField] private bool enableExperimentalRunner = false;

        public event Action OnSequenceStarted;
        public event Action<TimedHitPhaseInfo> OnPhaseStarted;
        public event Action<TimedHitPhaseResult> OnPhaseResolved;
        public event Action<TimedHitResult> OnSequenceCompleted;
        public event Action<Ks1PhaseOutcome> PhaseResolved;

        private Coroutine runRoutine;
        private TaskCompletionSource<TimedHitResult> pendingRun;
        private TimedHitRequest currentRequest;
        private bool sequenceActive;

        private void Awake()
        {
            if (manager == null)
            {
                manager = FindManager();
            }

            TryRegister();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            if (sequenceActive)
            {
                AbortSequence(cancelled: true);
            }

            if (manager != null && ReferenceEquals(manager.TimedHitRunner, this))
            {
                manager.SetTimedHitRunner(null);
            }
        }

        private BattleManagerV2 FindManager()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<BattleManagerV2>();
#else
            return UnityEngine.Object.FindObjectOfType<BattleManagerV2>();
#endif
        }

        private void TryRegister()
        {
            if (!enableExperimentalRunner)
            {
                return;
            }

            if (manager == null)
            {
                manager = FindManager();
            }

            if (manager != null)
            {
                manager.SetTimedHitRunner(this);
            }
        }

        public Task<TimedHitResult> RunAsync(TimedHitRequest request)
        {
            if (!enableExperimentalRunner || !isActiveAndEnabled)
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
                var empty = new TimedHitResult(0, 0, 0, 1f, cancelled: false, successStreak: 0);
                OnSequenceStarted?.Invoke();
                OnSequenceCompleted?.Invoke(empty);
                pendingRun.SetResult(empty);
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
            float basePhaseDuration = tier.TimelineDuration > 0f ? tier.TimelineDuration : 1f;
            float resultHold = Mathf.Max(0f, tier.ResultHoldDuration);

            OnSequenceStarted?.Invoke();

            int perfectCount = 0;
            int goodCount = 0;

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

                float normalizedTime = 1f;
                bool autoMiss = false;

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

                        float elapsed = basePhaseDuration > 0f
                            ? (Time.time - startTime) / basePhaseDuration
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
                bool success = outcome.Kind != PhaseOutcomeKind.Miss;
                if (success)
                {
                    if (outcome.Kind == PhaseOutcomeKind.Perfect)
                    {
                        perfectCount++;
                    }
                    else
                    {
                        goodCount++;
                    }
                }

                var phaseResult = new TimedHitPhaseResult(
                    phaseIndex,
                    success,
                    outcome.Multiplier,
                    outcome.AccuracyNormalized);
                OnPhaseResolved?.Invoke(phaseResult);

                bool isLastPhase = phaseIndex == totalPhases;
                bool chainCancelled = !success;
                bool chainCompleted = success && isLastPhase;

                EmitPhaseOutcome(
                    phaseIndex - 1,
                    totalPhases,
                    success
                        ? (outcome.Kind == PhaseOutcomeKind.Perfect ? TimedHitJudgment.Perfect : TimedHitJudgment.Good)
                        : TimedHitJudgment.Miss,
                    chainCancelled,
                    chainCompleted,
                    success ? 1 : 0,
                    totalPhases,
                    outcome.Multiplier,
                    chainCancelled || chainCompleted);

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

                if (chainCancelled)
                {
                    break;
                }
            }

            int missCount = Mathf.Max(0, totalPhases - (perfectCount + goodCount));
            var finalResult = BuildResult(tier, perfectCount, goodCount, missCount, totalPhases - 1);
            CompleteSequence(finalResult);
        }

        private void CompleteSequence(TimedHitResult result)
        {
            sequenceActive = false;

            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
                runRoutine = null;
            }

            OnSequenceCompleted?.Invoke(result);
            pendingRun?.TrySetResult(result);
            pendingRun = null;
            currentRequest = default;
        }

        private void AbortSequence(bool cancelled)
        {
            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
                runRoutine = null;
            }

            var fallback = new TimedHitResult(0, 0, 0, 0f, cancelled: cancelled, successStreak: 0);
            pendingRun?.TrySetResult(fallback);
            pendingRun = null;
            currentRequest = default;
            sequenceActive = false;
        }

        private void EmitPhaseOutcome(
            int phaseIndex,
            int totalPhases,
            TimedHitJudgment judgment,
            bool chainCancelled,
            bool chainCompleted,
            int phaseHitsSucceeded,
            int overallTotalHits,
            float phaseMultiplier,
            bool isFinal)
        {
            var result = new TimedHitResult(
                judgment,
                phaseHitsSucceeded,
                overallTotalHits,
                phaseMultiplier,
                phaseIndex,
                totalPhases,
                isFinal);

            PhaseResolved?.Invoke(new Ks1PhaseOutcome(
                phaseIndex,
                totalPhases,
                judgment,
                chainCancelled,
                chainCompleted,
                result));
        }

        private static TimedHitResult BuildResult(
            Ks1TimedHitProfile.Tier tier,
            int perfectCount,
            int goodCount,
            int missCount,
            int finalPhaseIndex)
        {
            int totalHits = Mathf.Max(0, perfectCount + goodCount + missCount);
            int hitsSucceeded = Mathf.Max(0, perfectCount + goodCount);
            int refund = Mathf.Clamp(hitsSucceeded, 0, tier.RefundMax);
            float multiplier = CalculateCombinedMultiplier(tier, perfectCount, goodCount);

            return new TimedHitResult(
                TimedHitResult.InferJudgment(hitsSucceeded, totalHits),
                hitsSucceeded,
                totalHits,
                multiplier,
                finalPhaseIndex,
                totalHits <= 0 ? 1 : totalHits,
                true,
                refund,
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
