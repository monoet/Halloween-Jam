using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace HalloweenJam.Combat.Animations
{
    /// <summary>
    /// Controla la animación de ataque de Lilia en combate:
    /// - Efectos de glow, retroceso y sacudida de cámara.
    /// - Sincroniza sonido con el impacto real.
    /// - Incluye modo de depuración de fases.
    /// </summary>
    public class LiliaAttackAnimator : MonoBehaviour, IAttackAnimator
    {
        [Header("References")]
        [SerializeField] private Transform modelRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform enemyRoot;
        [SerializeField] private SpriteRenderer enemySprite;
        [SerializeField] private AudioSource audioSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip chargeSfx;
        [SerializeField] private AudioClip slashSfx;
        [SerializeField] private AudioClip settleSfx;

        [Header("Movement Settings")]
        [SerializeField] private float backStepDistance = 0.5f;
        [SerializeField] private float backStepDuration = 0.3f;
        [SerializeField] private float chargeHoldDuration = 0.4f;
        [SerializeField] private float lungeDistance = 1.2f;
        [SerializeField] private float lungeDuration = 0.25f;
        [SerializeField] private float recoverDuration = 0.4f;

        [Header("Enemy Feedback")]
        [SerializeField] private float enemyKnockbackDistance = 0.3f;
        [SerializeField] private float enemyKnockbackDuration = 0.1f;
        [SerializeField] private Color enemyHitColor = Color.white;

        [Header("Glow Settings")]
        [SerializeField] private float glowLoopDuration = 0.25f;
        [SerializeField] private int glowLoops = 4;
        [SerializeField] private float glowRecoverDuration = 0.3f;
        [SerializeField] private float glowChargeIntensity = 1.5f;

        [Header("Camera Feedback")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float cameraShakeDuration = 0.1f;
        [SerializeField] private float cameraShakeStrength = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool autoPlayOnStart = false;
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool showTimingOverlay = false;

        private Sequence attackSequence;
        private Vector3 modelStartPosition;
        private Color baseColor = Color.white;
        private Color enemyBaseColor = Color.white;
        private Material spriteMaterialInstance;
        private Text debugOverlayText;

        private void Awake()
        {
            if (modelRoot == null)
                modelRoot = transform;

            modelStartPosition = modelRoot.localPosition;

            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
                spriteMaterialInstance = spriteRenderer.material;
                spriteMaterialInstance.SetFloat("_GlowIntensity", 0f);
            }

            if (enemySprite != null)
                enemyBaseColor = enemySprite.color;

            if (showTimingOverlay)
            {
                var canvasGO = new GameObject("DebugCanvas");
                canvasGO.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var textGO = new GameObject("DebugText");
                textGO.transform.SetParent(canvasGO.transform);
                debugOverlayText = textGO.AddComponent<Text>();
                debugOverlayText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                debugOverlayText.fontSize = 16;
                debugOverlayText.color = Color.yellow;
                debugOverlayText.alignment = TextAnchor.UpperLeft;
                debugOverlayText.rectTransform.anchorMin = new Vector2(0, 1);
                debugOverlayText.rectTransform.anchorMax = new Vector2(0, 1);
                debugOverlayText.rectTransform.anchoredPosition = new Vector2(10, -10);
            }
        }

        private void Start()
        {
            if (autoPlayOnStart)
                PlayAttack();
        }

        public void PlayAttack(Action onImpact = null, Action onComplete = null)
        {
            attackSequence?.Kill();
            modelRoot.DOKill();
            spriteRenderer?.DOKill();
            spriteMaterialInstance?.DOKill();
            enemyRoot?.DOKill();
            enemySprite?.DOKill();
            ResetVisualState();

            var startX = modelStartPosition.x;
            Action impactCallback = onImpact;
            Action completeCallback = onComplete;

            attackSequence = DOTween.Sequence();

            // ---------------- PHASE 1: CHARGE ----------------
            attackSequence.AppendCallback(() =>
            {
                PlaySfx(chargeSfx);
                DebugPhase("Phase 1: Charge started");
            });

            attackSequence.Append(modelRoot.DOLocalMoveX(startX + backStepDistance, backStepDuration)
                .SetEase(Ease.OutQuad));

            if (spriteMaterialInstance != null)
            {
                attackSequence.Join(spriteMaterialInstance
                    .DOFloat(glowChargeIntensity, "_GlowIntensity", glowLoopDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(glowLoops, LoopType.Yoyo)
                    .From(0f));
            }

            if (chargeHoldDuration > 0f)
                attackSequence.AppendInterval(chargeHoldDuration);

            // ---------------- PHASE 2: LUNGE + IMPACT ----------------
            attackSequence.AppendCallback(() =>
            {
                DebugPhase("Phase 2: Lunge started");
            });

            var lungeTween = modelRoot.DOLocalMoveX(startX - lungeDistance, lungeDuration)
                .SetEase(Ease.InExpo)
                .OnComplete(() =>
                {
                    TriggerEnemyHitFeedback(); // aquí suena el slash real
                    impactCallback?.Invoke();
                });

            attackSequence.Append(lungeTween);

            // ---------------- PHASE 3: RECOVER ----------------
            attackSequence.Append(modelRoot.DOLocalMoveX(startX, recoverDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    PlaySfx(settleSfx);
                    DebugPhase("Phase 3: Recover complete");
                    completeCallback?.Invoke();
                }));

            if (spriteMaterialInstance != null)
            {
                attackSequence.Join(spriteMaterialInstance
                    .DOFloat(0f, "_GlowIntensity", glowRecoverDuration)
                    .SetEase(Ease.InOutSine));
            }

            attackSequence.Play();
        }

        private void TriggerEnemyHitFeedback()
        {
            DebugPhase("TriggerEnemyHitFeedback invoked");

            // 🔊 sonido sincronizado con el impacto
            PlaySfx(slashSfx);

            // knockback enemigo
            if (enemyRoot != null)
            {
                enemyRoot.DOKill();
                var direction = Mathf.Sign(enemyRoot.position.x - modelRoot.position.x);
                if (Mathf.Approximately(direction, 0f)) direction = 1f;

                enemyRoot.DOLocalMoveX(
                        enemyRoot.localPosition.x + enemyKnockbackDistance * direction,
                        enemyKnockbackDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLoops(2, LoopType.Yoyo);
            }

            // flash color
            if (enemySprite != null)
            {
                enemySprite.DOKill();
                enemySprite.DOColor(enemyHitColor, 0.05f)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.Linear);
            }

            // shake cámara
            if (cameraTransform != null)
            {
                cameraTransform.DOKill();
                cameraTransform.DOShakePosition(
                    cameraShakeDuration,
                    cameraShakeStrength,
                    vibrato: 10,
                    randomness: 90f,
                    snapping: false,
                    fadeOut: true);
            }
        }

        private void PlaySfx(AudioClip clip)
        {
            if (audioSource == null || clip == null)
                return;

            audioSource.PlayOneShot(clip);
        }

        private void ResetVisualState()
        {
            modelRoot.localPosition = modelStartPosition;

            if (spriteRenderer != null)
                spriteRenderer.color = baseColor;

            if (spriteMaterialInstance != null)
                spriteMaterialInstance.SetFloat("_GlowIntensity", 0f);

            if (enemySprite != null)
                enemySprite.color = enemyBaseColor;
        }

        private void DebugPhase(string message)
        {
            if (!enableDebugLogs && !showTimingOverlay) return;

            string full = $"[LiliaAttackAnimator] {message}";
            if (enableDebugLogs) Debug.Log(full, this);

            if (showTimingOverlay && debugOverlayText != null)
            {
                debugOverlayText.text = $"{DateTime.Now:HH:mm:ss.fff} - {message}\n" + debugOverlayText.text;
                int maxLines = 10;
                var lines = debugOverlayText.text.Split('\n');
                if (lines.Length > maxLines)
                    debugOverlayText.text = string.Join("\n", lines, 0, maxLines);
            }
        }
    }
}
