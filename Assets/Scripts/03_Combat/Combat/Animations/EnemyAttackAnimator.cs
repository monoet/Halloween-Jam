using System;
using DG.Tweening;
using UnityEngine;

namespace HalloweenJam.Combat.Animations
{
    public class EnemyAttackAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform modelRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform targetRoot;
        [SerializeField] private SpriteRenderer targetSprite;
        [SerializeField] private AudioSource audioSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip windupSfx;
        [SerializeField] private AudioClip impactSfx;
        [SerializeField] private AudioClip settleSfx;

        [Header("Movement Settings")]
        [SerializeField] private float windupDistance = 0.3f;
        [SerializeField] private float windupDuration = 0.18f;
        [SerializeField] private float lungeDistance = 0.9f;
        [SerializeField] private float lungeDuration = 0.22f;
        [SerializeField] private float recoverDuration = 0.35f;

        [Header("Target Feedback")]
        [SerializeField] private float targetKnockbackDistance = 0.35f;
        [SerializeField] private float targetKnockbackDuration = 0.12f;
        [SerializeField] private Color targetHitColor = Color.white;

        [Header("Camera Feedback")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float cameraShakeDuration = 0.1f;
        [SerializeField] private float cameraShakeStrength = 0.18f;

        [Header("Debug")]
        [SerializeField] private bool autoPlayOnStart = false;
        [SerializeField] private bool enableDebugLogs = false;

        private Vector3 modelStartPosition;
        private Vector3 targetStartPosition;
        private Color targetBaseColor = Color.white;
        private Sequence currentSequence;

        private void Awake()
        {
            if (modelRoot == null)
            {
                modelRoot = transform;
            }

            modelStartPosition = modelRoot.localPosition;

            if (targetRoot != null)
            {
                targetStartPosition = targetRoot.localPosition;
            }

            if (targetSprite != null)
            {
                targetBaseColor = targetSprite.color;
            }
        }

        private void Start()
        {
            if (autoPlayOnStart)
            {
                PlayAttack();
            }
        }

        public void PlayAttack(Action onImpact = null, Action onComplete = null)
        {
            currentSequence?.Kill();
            modelRoot?.DOKill();
            targetRoot?.DOKill();
            if (targetSprite != null)
            {
                DOTween.Kill(targetSprite);
            }
            cameraTransform?.DOKill();

            modelStartPosition = modelRoot != null ? modelRoot.localPosition : Vector3.zero;
            if (targetRoot != null)
            {
                targetStartPosition = targetRoot.localPosition;
            }
            ResetVisuals();

            var direction = CalculateHorizontalDirection();
            var impactCallback = onImpact;
            var completeCallback = onComplete;

            currentSequence = DOTween.Sequence();

            // Windup phase (move slightly away from target)
            currentSequence.AppendCallback(() =>
            {
                PlaySfx(windupSfx);
                DebugLog("Enemy Attack: Windup.");
            });

            currentSequence.Append(modelRoot.DOLocalMoveX(modelStartPosition.x - direction * windupDistance, windupDuration)
                .SetEase(Ease.OutQuad));

            // Lunge and impact
            currentSequence.AppendCallback(() =>
            {
                PlaySfx(impactSfx);
                DebugLog("Enemy Attack: Lunge.");
            });

            var lungeTween = modelRoot.DOLocalMoveX(modelStartPosition.x + direction * lungeDistance, lungeDuration)
                .SetEase(Ease.InExpo)
                .OnComplete(() =>
                {
                    TriggerTargetFeedback(direction);
                    impactCallback?.Invoke();
                });

            currentSequence.Append(lungeTween);

            // Recover
            currentSequence.Append(modelRoot.DOLocalMoveX(modelStartPosition.x, recoverDuration)
                .SetEase(Ease.OutCubic)
                .OnStart(() => PlaySfx(settleSfx)));

            currentSequence.OnComplete(() =>
            {
                DebugLog("Enemy Attack: Completed.");
                completeCallback?.Invoke();
                ResetVisuals();
            });

            currentSequence.Play();
        }

        private float CalculateHorizontalDirection()
        {
            if (targetRoot == null)
            {
                return 1f;
            }

            var delta = targetRoot.position - modelRoot.position;
            var sign = Mathf.Sign(delta.x);

            if (Mathf.Approximately(sign, 0f))
            {
                sign = 1f;
            }

            return sign;
        }

        private void TriggerTargetFeedback(float direction)
        {
            DebugLog("Enemy Attack: Triggering target feedback.");

            if (targetRoot != null)
            {
                var targetSequence = DOTween.Sequence();
                targetSequence.Append(targetRoot.DOLocalMove(targetRoot.localPosition + new Vector3(direction * targetKnockbackDistance, 0f, 0f), targetKnockbackDuration)
                    .SetEase(Ease.OutQuad));
                targetSequence.Append(targetRoot.DOLocalMove(targetStartPosition, targetKnockbackDuration)
                    .SetEase(Ease.InQuad));
            }

            if (targetSprite != null)
            {
                targetSprite.DOColor(targetHitColor, targetKnockbackDuration * 0.5f)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.Linear);
            }

            if (cameraTransform != null)
            {
                cameraTransform.DOShakePosition(cameraShakeDuration, cameraShakeStrength, vibrato: 10, randomness: 90f, snapping: false, fadeOut: true);
            }
        }

        private void ResetVisuals()
        {
            modelRoot.localPosition = modelStartPosition;

            if (targetRoot != null)
            {
                targetRoot.localPosition = targetStartPosition;
            }

            if (targetSprite != null)
            {
                targetSprite.color = targetBaseColor;
            }
        }

        private void PlaySfx(AudioClip clip)
        {
            if (audioSource == null || clip == null)
            {
                return;
            }

            audioSource.PlayOneShot(clip);
        }

        private void DebugLog(string message)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            Debug.Log($"[EnemyAttackAnimator] {message}", this);
        }
    }
}
