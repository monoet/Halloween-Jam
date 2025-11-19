using System;
using System.Collections;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using BattleV2.AnimationSystem.Runtime;
using UnityEngine;

namespace BattleV2.Audio
{
    /// <summary>
    /// Listens to TimedHitResultEvent and emits combat flags for audio (miss/good/perfect).
    /// </summary>
    public sealed class TimedHitAudioBridge : MonoBehaviour
    {
        [SerializeField] private AnimationSystemInstaller installer;

        private IDisposable subscription;
        private IDisposable phaseSubscription;
        private CombatEventDispatcher dispatcher;
        private Coroutine subscribeRoutine;

        private void Awake()
        {
            installer ??= AnimationSystemInstaller.Current;
            dispatcher = installer != null ? installer.CombatEvents : null;
        }

        private void Start()
        {
            subscribeRoutine = StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            if (subscribeRoutine != null)
            {
                StopCoroutine(subscribeRoutine);
                subscribeRoutine = null;
            }

            subscription?.Dispose();
            subscription = null;

            phaseSubscription?.Dispose();
            phaseSubscription = null;
        }

        private IEnumerator SubscribeWhenReady()
        {
            // Espera indefinidamente hasta que EventBus y Dispatcher estén listos
            while (installer == null || installer.EventBus == null || dispatcher == null)
            {
                installer ??= AnimationSystemInstaller.Current;
                dispatcher ??= installer?.CombatEvents;
                yield return null;
            }

            subscription = installer.EventBus.Subscribe<TimedHitResultEvent>(OnTimedHitResult);
            phaseSubscription = installer.EventBus.Subscribe<TimedHitPhaseEvent>(OnTimedHitPhase);
        }

        private void OnTimedHitResult(TimedHitResultEvent evt)
        {
            string quality = evt.Judgment.ToString().ToUpper();
            int window = evt.WindowIndex;
            double offset = evt.DeltaMilliseconds;

            Debug.Log($"PhasEv01 | RESULT={quality} | Window={window} | OffsetMs={offset:F1}", this);
        }

        private void OnTimedHitPhase(TimedHitPhaseEvent evt)
        {
            string quality = evt.Judgment.ToString().ToUpper();
            int index = evt.PhaseIndex;
            int total = evt.TotalPhases;
            float accuracy = evt.AccuracyNormalized;

            Debug.Log(
                $"PhasEv02 | PHASE={index}/{total} | JUDGMENT={quality} | Accuracy={accuracy:F2} | Cancelled={evt.Cancelled} | Final={evt.IsFinal}",
                this);
        }
    }
}
