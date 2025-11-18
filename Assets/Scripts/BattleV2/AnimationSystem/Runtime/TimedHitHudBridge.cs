using System;
using TMPro;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Listens to timed-hit results and shows quick feedback on a HUD label.
    /// </summary>
    public sealed class TimedHitHudBridge : MonoBehaviour
    {
        [SerializeField] private AnimationSystemInstaller installer;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField, Min(0f)] private float holdSeconds = 1f;
        [SerializeField] private Color perfectColor = new(0.1f, 0.9f, 0.2f);
        [SerializeField] private Color goodColor = new(0.9f, 0.8f, 0.1f);
        [SerializeField] private Color missColor = new(0.9f, 0.2f, 0.2f);

        private IDisposable subscription;
        private float timer;

        private void Awake()
        {
            installer ??= AnimationSystemInstaller.Current;
        }

        private void OnEnable()
        {
            ClearLabel();
            Invoke(nameof(Subscribe), 0f);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
            ClearLabel();
        }

        private void Update()
        {
            if (feedbackLabel == null || holdSeconds <= 0f || timer <= 0f)
            {
                return;
            }

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                ClearLabel();
            }
        }

        private void Subscribe()
        {
            if (installer?.EventBus == null)
            {
                return;
            }

            subscription = installer.EventBus.Subscribe<TimedHitResultEvent>(HandleTimedHitResult);
        }

        private void HandleTimedHitResult(TimedHitResultEvent evt)
        {
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.gameObject.SetActive(true);
            feedbackLabel.text = $"{evt.Judgment} ({evt.DeltaMilliseconds:0.#} ms)";
            feedbackLabel.color = evt.Judgment switch
            {
                TimedHitJudgment.Perfect => perfectColor,
                TimedHitJudgment.Good => goodColor,
                _ => missColor
            };

            timer = holdSeconds;
        }

        private void ClearLabel()
        {
            if (feedbackLabel == null)
            {
                return;
            }

            feedbackLabel.text = string.Empty;
            timer = 0f;
        }
    }
}
