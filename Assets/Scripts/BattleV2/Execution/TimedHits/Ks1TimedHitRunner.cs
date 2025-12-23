using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Charge;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// KS1 timed-hit runner that consumes TimedHitResultEvent from the service and emits Ks1PhaseOutcome.
    /// </summary>
    public sealed class Ks1TimedHitRunner : MonoBehaviour, ITimedHitRunner
    {
        [SerializeField] private AnimationSystemInstaller installer;
        [SerializeField] private bool enableDebugLogs = true;

        public event Action OnSequenceStarted;
        public event Action<TimedHitPhaseInfo> OnPhaseStarted;
        public event Action<TimedHitPhaseResult> OnPhaseResolved;
        public event Action<TimedHitResult> OnSequenceCompleted;
        public event Action<Ks1PhaseOutcome> PhaseResolved;

        private readonly Queue<TimedHitResultEvent> pendingEvents = new();
        private readonly object eventGate = new();

        private TaskCompletionSource<TimedHitResult> pendingRun;
        private IDisposable eventSubscription;
        private TimedHitRequest currentRequest;
        private bool sequenceActive;
        private Coroutine runRoutine;
        private Coroutine windowRoutine;

        private void Awake()
        {
            installer ??= AnimationSystemInstaller.Current;
        }

        private void OnDisable()
        {
            if (sequenceActive)
            {
                AbortSequence(cancelled: true);
            }

            UnsubscribeFromTimedHitEvents();
            ClearQueue();

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

            if (request.Profile == null)
            {
                var fallback = new TimedHitResult(0, 0, 0, 1f, cancelled: false, successStreak: 0);
                OnSequenceStarted?.Invoke();
                OnSequenceCompleted?.Invoke(fallback);
                return Task.FromResult(fallback);
            }

            installer ??= AnimationSystemInstaller.Current;
            if (installer?.EventBus == null)
            {
                Debug.LogWarning("[Ks1TimedHitRunner] EventBus not available, falling back to instant runner.", this);
                return InstantTimedHitRunner.Shared.RunAsync(request);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[Ks1TimedHitRunner] Sequence started. Bus Hash: {installer.EventBus.GetHashCode()}.", this);
            }

            SubscribeToTimedHitEvents();

            currentRequest = request;
            pendingRun = new TaskCompletionSource<TimedHitResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            runRoutine = StartCoroutine(RunSequence());
            return pendingRun.Task;
        }

        private IEnumerator RunSequence()
        {
            sequenceActive = true;

            var request = currentRequest;
            var token = request.CancellationToken;
            var tier = request.Profile.GetTierForCharge(request.CpCharge);
            int expectedPhases = Mathf.Max(1, tier.Hits);
            int processedPhases = 0;
            int perfectCount = 0;
            int goodCount = 0;
            bool chainCancelled = false;

            ClearQueue();
            OnSequenceStarted?.Invoke();
            windowRoutine = StartCoroutine(EmitWindows(request, tier));

            while (!chainCancelled && processedPhases < expectedPhases)
            {
                if (token.IsCancellationRequested)
                {
                    AbortSequence(cancelled: true);
                    yield break;
                }

                // Wait for the event to be processed and enqueued
                float timeout = 2.0f; // Safety timeout
                float timer = 0f;
                while (pendingEvents.Count == 0 && timer < timeout)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }

                if (!TryDequeueEvent(request.Attacker, out var evt))
                {
                    Debug.LogError($"[Ks1TimedHitRunner] TIMEOUT waiting for Window Event! The Timeline for '{request.ActionData?.id}' might be missing 'AnimationWindowEvent' markers, or the EventBus is disconnected.");
                    // If we timed out, force a miss to prevent hanging
                    evt = new TimedHitResultEvent(request.Attacker, ResolveEventTag(request), TimedHitJudgment.Miss, 0, 0, processedPhases + 1, expectedPhases, false, 0, 0, TimedHitResultScope.RawWindow, "none", "neutral", false, 1);
                }

                processedPhases++;

                if (evt.WindowCount > 0)
                {
                    expectedPhases = Mathf.Max(expectedPhases, evt.WindowCount);
                }

                int currentPhaseIndex = evt.WindowIndex > 0 ? evt.WindowIndex : processedPhases;
                int totalPhases = evt.WindowCount > 0 ? evt.WindowCount : expectedPhases;

                OnPhaseStarted?.Invoke(new TimedHitPhaseInfo(
                    currentPhaseIndex,
                    totalPhases,
                    0f,
                    1f));

                TimedHitJudgment judgment = evt.Judgment;
                bool success = judgment != TimedHitJudgment.Miss;

                if (success)
                {
                    if (judgment == TimedHitJudgment.Perfect)
                    {
                        perfectCount++;
                    }
                    else
                    {
                        goodCount++;
                    }
                }
                else
                {
                    chainCancelled = true;
                }

                float phaseMultiplier = ResolvePhaseMultiplier(tier, judgment);
                var accuracy = ResolveAccuracy(judgment);
                var phaseResult = new TimedHitPhaseResult(
                    currentPhaseIndex,
                    success,
                    phaseMultiplier,
                    accuracy,
                    request.Attacker);
                OnPhaseResolved?.Invoke(phaseResult);

                EmitPhaseOutcome(
                    phaseIndex: currentPhaseIndex,
                    totalPhases: totalPhases,
                    judgment,
                    chainCancelled,
                    chainCompleted: success && processedPhases >= totalPhases,
                    phaseHitsSucceeded: success ? 1 : 0,
                    overallTotalHits: totalPhases,
                    phaseMultiplier,
                    accuracy,
                    success && processedPhases >= totalPhases,
                    actor: request.Attacker);

                if (chainCancelled)
                {
                    break;
                }
            }

            int missCount = Mathf.Max(0, expectedPhases - (perfectCount + goodCount));
            var finalResult = BuildResult(tier, perfectCount, goodCount, missCount, processedPhases, expectedPhases);
            PublishFinalResultEvent(finalResult, request);
            CompleteSequence(finalResult);
        }

        private void CompleteSequence(TimedHitResult result)
        {
            sequenceActive = false;

            StopWindowRoutine();
            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
                runRoutine = null;
            }

            UnsubscribeFromTimedHitEvents();
            ClearQueue();

            OnSequenceCompleted?.Invoke(result);
            pendingRun?.TrySetResult(result);
            pendingRun = null;
            currentRequest = default;
        }

        private void PublishFinalResultEvent(TimedHitResult result, TimedHitRequest request)
        {
            Debug.Log(
                $"[KS1-FINAL] ENTERED | installer={installer} | bus={installer?.EventBus} | attacker={request.Attacker?.name}",
                this);

            if (installer?.EventBus == null)
            {
                Debug.LogError("[KS1-FINAL] ERROR: installer.EventBus is NULL", this);
                return;
            }

            var bus = installer.EventBus;
            if (bus == null || request.Attacker == null)
            {
                if (bus == null)
                {
                    Debug.LogError("[KS1-FINAL] ERROR: bus is NULL", this);
                }
                if (request.Attacker == null)
                {
                    Debug.LogError("[KS1-FINAL] ERROR: request.Attacker is NULL", this);
                }
                return;
            }

            double timestamp = Time.timeAsDouble;
            string weaponKind = "none";
            string element = "neutral";
            int targetCount = request.Target != null ? 1 : 1;
            var evt = new TimedHitResultEvent(
                request.Attacker,
                string.Empty,
                result.Judgment,
                deltaMilliseconds: 0d,
                inputTimestamp: timestamp,
                windowIndex: Math.Max(1, result.PhaseIndex),
                windowCount: Math.Max(1, result.TotalPhases),
                consumedInput: result.HitsSucceeded > 0,
                windowOpenedAt: timestamp,
                windowClosedAt: timestamp,
                scope: TimedHitResultScope.Final,
                weaponKind: weaponKind,
                element: element,
                isCritical: false,
                targetCount: targetCount);

            Debug.Log(
                $"[KS1-FINAL] PUBLISH | BusHash={bus.GetHashCode()} | Actor={request.Attacker?.name} | Judgment={result.Judgment} | Scope={evt.Scope}",
                this);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevCpTrace)
            {
                BattleDiagnostics.Log(
                    "CPTRACE",
                    $"TH_RESULT_FINAL exec={request.ExecutionId} judgment={result.Judgment} phase={result.PhaseIndex}/{result.TotalPhases} hits={result.HitsSucceeded}/{result.TotalHits}",
                    request.Attacker);
            }
            if (BattleDiagnostics.DevFlowTrace)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"TH_RESULT_FINAL exec={request.ExecutionId} judgment={result.Judgment} phase={result.PhaseIndex}/{result.TotalPhases} hits={result.HitsSucceeded}/{result.TotalHits}",
                    request.Attacker);
            }
#endif
            bus.Publish(evt);
        }

        private void AbortSequence(bool cancelled)
        {
            StopWindowRoutine();
            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
                runRoutine = null;
            }

            UnsubscribeFromTimedHitEvents();
            ClearQueue();

            var fallback = new TimedHitResult(
                TimedHitJudgment.Miss,
                0,
                0,
                0f,
                phaseIndex: 1,
                totalPhases: 1,
                isFinal: true,
                cancelled: cancelled,
                successStreak: 0);

            pendingRun?.TrySetResult(fallback);
            pendingRun = null;
            currentRequest = default;
            sequenceActive = false;
        }

        private void StopWindowRoutine()
        {
            if (windowRoutine != null)
            {
                StopCoroutine(windowRoutine);
                windowRoutine = null;
            }
        }

        private void SubscribeToTimedHitEvents()
        {
            if (eventSubscription != null)
            {
                return;
            }

            var bus = installer?.EventBus;
            if (bus == null)
            {
                return;
            }

            eventSubscription = bus.Subscribe<TimedHitResultEvent>(OnTimedHitResultEvent);
        }

        private void UnsubscribeFromTimedHitEvents()
        {
            eventSubscription?.Dispose();
            eventSubscription = null;
        }

        private void OnTimedHitResultEvent(TimedHitResultEvent evt)
        {
            if (!sequenceActive)
            {
                return;
            }

            var request = currentRequest;
            if (request.Attacker == null || evt.Actor != request.Attacker)
            {
                return;
            }

            lock (eventGate)
            {
                pendingEvents.Enqueue(evt);
            }
        }

        private bool TryDequeueEvent(CombatantState actor, out TimedHitResultEvent evt)
        {
            lock (eventGate)
            {
                while (pendingEvents.Count > 0)
                {
                    var candidate = pendingEvents.Dequeue();
                    if (candidate.Actor == actor)
                    {
                        evt = candidate;
                        return true;
                    }
                }
            }

            evt = default;
            return false;
        }

        private void ClearQueue()
        {
            lock (eventGate)
            {
                pendingEvents.Clear();
            }
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
            float accuracy,
            bool requestedFinal,
            CombatantState actor)
        {
            int displayIndex = Mathf.Max(1, phaseIndex);
            int displayTotal = Mathf.Max(1, totalPhases);
            bool isFinalPhase = requestedFinal || chainCancelled || displayIndex >= displayTotal;

            var phaseResult = new TimedHitResult(
                judgment,
                phaseHitsSucceeded,
                overallTotalHits,
                phaseMultiplier,
                displayIndex,
                displayTotal,
                isFinalPhase);

            PublishPhaseEvent(
                actor,
                judgment,
                accuracy,
                displayIndex,
                displayTotal,
                chainCancelled,
                isFinalPhase);

            PhaseResolved?.Invoke(new Ks1PhaseOutcome(
                displayIndex - 1,
                displayTotal,
                judgment,
                chainCancelled,
                chainCompleted,
                phaseResult,
                actor));
        }

        private IEnumerator EmitWindows(TimedHitRequest request, Ks1TimedHitProfile.Tier tier)
        {
            var bus = installer?.EventBus;
            if (bus == null || request.Attacker == null)
            {
                yield break;
            }

            ResolveWindowBounds(tier, out float startNorm, out float endNorm, out float perfectNorm);
            int total = Mathf.Max(1, tier.Hits);
            float timelineDuration = Mathf.Max(0.05f, tier.TimelineDuration);
            float segmentDuration = timelineDuration / total;
            float timelineStart = Time.time;
            string payload = $"perfect={perfectNorm.ToString(CultureInfo.InvariantCulture)}";
            float windowNormDuration = Mathf.Max(0.01f, endNorm - startNorm);
            float windowSeconds = Mathf.Max(0.01f, windowNormDuration * segmentDuration);

            for (int i = 0; i < total; i++)
            {
                float segmentOffset = i * segmentDuration;
                float openAt = timelineStart + segmentOffset + (startNorm * segmentDuration);
                float closeAt = timelineStart + segmentOffset + (endNorm * segmentDuration);

                float waitToOpen = Mathf.Max(0f, openAt - Time.time);
                if (waitToOpen > 0f)
                {
                    yield return new WaitForSeconds(waitToOpen);
                }

                PublishWindow(bus, request, payload, startNorm, endNorm, true, i + 1, total, windowSeconds);

                float windowDuration = Mathf.Max(0.01f, closeAt - Time.time);
                yield return new WaitForSeconds(windowDuration);

                PublishWindow(bus, request, payload, startNorm, endNorm, false, i + 1, total, windowSeconds);
            }
        }

        private void PublishWindow(
            IAnimationEventBus bus,
            TimedHitRequest request,
            string payload,
            float startNorm,
            float endNorm,
            bool opening,
            int index,
            int count,
            float windowSeconds)
        {
            string tag = ResolveEventTag(request);
            bus.Publish(new AnimationWindowEvent(request.Attacker, tag, payload, startNorm, endNorm, opening, index, count));

            if (enableDebugLogs)
            {
                Debug.Log($"[KS1] Runner window {(opening ? "open" : "close")} dur={windowSeconds:0.###} tag={tag} idx={index}/{count}", this);
                Debug.Log($"PhasEvWindow | {(opening ? "OPEN" : "CLOSE")} | Tag={tag} | Actor={request.Attacker?.name ?? "(null)"} | idx={index}/{count} | Norm={startNorm:0.###}-{endNorm:0.###} | DurSec={windowSeconds:0.###}");
            }
        }

        private static void ResolveWindowBounds(Ks1TimedHitProfile.Tier tier, out float startNorm, out float endNorm, out float perfectNorm)
        {
            // Force full timeline window: start at 0, end at 1. Perfect center still used for payload.
            perfectNorm = (tier.PerfectWindowCenter <= 0f && tier.PerfectWindowRadius <= 0f)
                ? 0.5f
                : Mathf.Clamp01(tier.PerfectWindowCenter);
            startNorm = 0f;
            endNorm = 1f;
        }

        private static TimedHitResult BuildResult(
            Ks1TimedHitProfile.Tier tier,
            int perfectCount,
            int goodCount,
            int missCount,
            int finalPhaseIndex,
            int totalPhases)
        {
            int totalHits = Mathf.Max(0, perfectCount + goodCount + missCount);
            int hitsSucceeded = Mathf.Max(0, perfectCount + goodCount);
            int refund = Mathf.Clamp(hitsSucceeded, 0, tier.RefundMax);
            float multiplier = CalculateCombinedMultiplier(tier, perfectCount, goodCount);
            int finalPhase = Mathf.Max(1, finalPhaseIndex);
            int displayTotal = Mathf.Max(1, totalPhases);

            return new TimedHitResult(
                TimedHitResult.InferJudgment(hitsSucceeded, totalHits),
                hitsSucceeded,
                totalHits,
                multiplier,
                finalPhase,
                displayTotal,
                true,
                refund,
                cancelled: false,
                successStreak: hitsSucceeded);
        }

        private static float ResolvePhaseMultiplier(Ks1TimedHitProfile.Tier tier, TimedHitJudgment judgment)
        {
            return judgment switch
            {
                TimedHitJudgment.Perfect => tier.PerfectHitMultiplier > 0f ? tier.PerfectHitMultiplier : 1.5f,
                TimedHitJudgment.Good => tier.SuccessHitMultiplier > 0f ? tier.SuccessHitMultiplier : 1f,
                _ => tier.MissHitMultiplier > 0f ? tier.MissHitMultiplier : 0f
            };
        }

        private static float ResolveAccuracy(TimedHitJudgment judgment)
        {
            return judgment switch
            {
                TimedHitJudgment.Perfect => 1f,
                TimedHitJudgment.Good => 0.75f,
                _ => 0f
            };
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

        private void PublishPhaseEvent(
            CombatantState actor,
            TimedHitJudgment judgment,
            float accuracy,
            int phaseIndex,
            int totalPhases,
            bool chainCancelled,
            bool isFinal)
        {
            var bus = installer?.EventBus;
            if (bus == null || actor == null)
            {
                return;
            }

            bus.Publish(new TimedHitPhaseEvent(
                actor,
                judgment,
                accuracy,
                Mathf.Max(1, phaseIndex),
                Mathf.Max(1, totalPhases),
                chainCancelled,
                isFinal));
        }

        private static string ResolveEventTag(TimedHitRequest request)
        {
            // Ks1 profile doesn't carry an EventTag; use action id as a stable tag.
            return !string.IsNullOrWhiteSpace(request.ActionData?.id) ? request.ActionData.id : "ks1";
        }
    }
}
