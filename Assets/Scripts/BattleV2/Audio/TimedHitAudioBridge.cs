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
        }

        private void OnTimedHitResult(TimedHitResultEvent evt)
        {
            // Temporal: bridge deshabilitado, no emite flags ni logs.
        }
    }
}
