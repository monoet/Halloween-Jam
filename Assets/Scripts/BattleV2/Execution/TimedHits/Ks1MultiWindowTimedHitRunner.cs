using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Charge;
using BattleV2.Orchestration;
using UnityEngine;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Experimental KS1 runner that consumes TimedHitResultEvent from the service and emits Ks1PhaseOutcome.
    /// </summary>
    public sealed class Ks1MultiWindowTimedHitRunner : MonoBehaviour, ITimedHitRunner
    {
        [SerializeField] private BattleManagerV2 manager;
        [SerializeField] private AnimationSystemInstaller installer;
        [SerializeField] private bool enableExperimentalRunner = false;

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

            UnsubscribeFromTimedHitEvents();
            ClearQueue();

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
                Debug.LogWarning("[Ks1MultiWindowTimedHitRunner] EventBus not available, falling back to instant runner.", this);
                return InstantTimedHitRunner.Shared.RunAsync(request);
            }

            SubscribeToTimedHitEvents();

            currentRequest = request;
            pendingRun = new TaskCompletionSource<TimedHitResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            runRoutine = StartCoroutine(RunSequence());
            return pendingRun.Task;
        }

        private Coroutine runRoutine;

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

            while (!chainCancelled && processedPhases < expectedPhases)
            {
                if (token.IsCancellationRequested)
                {
                    AbortSequence(cancelled: true);
                    yield break;
                }

                if (!TryDequeueEvent(request.Attacker, out var evt))
                {
                    yield return null;
                    continue;
                }

                processedPhases++;

                if (evt.WindowCount > 0)
                {
                    expectedPhases = Mathf.Max(expectedPhases, evt.WindowCount);
                }

                OnPhaseStarted?.Invoke(new TimedHitPhaseInfo(
                    evt.WindowIndex > 0 ? evt.WindowIndex : processedPhases,
                    evt.WindowCount > 0 ? evt.WindowCount : expectedPhases,
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
                var phaseResult = new TimedHitPhaseResult(
                    processedPhases,
                    success,
                    phaseMultiplier,
                    ResolveAccuracy(judgment),
                    request.Attacker);
                OnPhaseResolved?.Invoke(phaseResult);

                EmitPhaseOutcome(
                    phaseIndex: processedPhases - 1,
                    totalPhases: expectedPhases,
                    judgment,
                    chainCancelled,
                    chainCompleted: success && processedPhases >= expectedPhases,
                    phaseHitsSucceeded: success ? 1 : 0,
                    overallTotalHits: expectedPhases,
                    phaseMultiplier,
                    isFinal: false,
                    actor: request.Attacker);

                if (chainCancelled)
                {
                    break;
                }
            }

            int missCount = Mathf.Max(0, expectedPhases - (perfectCount + goodCount));
            var finalResult = BuildResult(tier, perfectCount, goodCount, missCount, processedPhases - 1, expectedPhases);
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

            UnsubscribeFromTimedHitEvents();
            ClearQueue();

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

            UnsubscribeFromTimedHitEvents();
            ClearQueue();

            var fallback = new TimedHitResult(
                TimedHitJudgment.Miss,
                0,
                0,
                0f,
                phaseIndex: 0,
                totalPhases: 1,
                isFinal: true,
                cancelled: cancelled,
                successStreak: 0);

            pendingRun?.TrySetResult(fallback);
            pendingRun = null;
            currentRequest = default;
            sequenceActive = false;
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
            bool isFinal,
            CombatantState actor)
        {
            var phaseResult = new TimedHitResult(
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
                phaseResult,
                actor));
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

            return new TimedHitResult(
                TimedHitResult.InferJudgment(hitsSucceeded, totalHits),
                hitsSucceeded,
                totalHits,
                multiplier,
                Math.Max(0, finalPhaseIndex),
                Math.Max(1, totalPhases),
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
                TimedHitJudgment.Good => 0.65f,
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
    }
}
