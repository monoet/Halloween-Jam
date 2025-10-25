using UnityEngine;
using DG.Tweening;

namespace BattleV2.Anim
{
    /// <summary>
    /// Encapsula las animaciones tween básicas para un combatiente (windup -> lunge -> freeze -> recover)
    /// y conserva la posición inicial correctamente tanto para objetos world-space como UI (RectTransform).
    /// </summary>
    public class BattleAnimationController : MonoBehaviour
    {
        [SerializeField] private AttackAnimProfile profile;
        [SerializeField] private Transform root;
        [SerializeField] private float directionMultiplier = 1f;

        [Header("Flash (optional)")]
        [SerializeField] private SpriteRenderer spriteFlashRenderer;
        [SerializeField] private Renderer meshFlashRenderer;
        [SerializeField] private string flashMaterialProperty = "_Color";
        [SerializeField] private Color flashColor = new Color(0.3f, 0.8f, 2f, 1f); // Azul con brillo HDR

        private bool useRectTransform;
        private RectTransform rectRoot;
        private Vector3 startLocalPosition;
        private Vector3 startAnchoredPosition;
        private Sequence activeSequence;
        private Tween hitTween;
        private bool hasCachedStart;
        private bool hasStarted;
        private Material flashMaterialInstance;
        private Color spriteBaseColor;
        private Color materialBaseColor;

        public Vector3 WorldPosition => root != null ? root.position : transform.position;

        private void Awake()
        {
            if (root == null)
            {
                root = transform;
            }

            rectRoot = root as RectTransform;
            useRectTransform = rectRoot != null;

            if (spriteFlashRenderer != null)
            {
                spriteBaseColor = spriteFlashRenderer.color;
            }

            if (meshFlashRenderer != null)
            {
                flashMaterialInstance = meshFlashRenderer.material;
                if (flashMaterialInstance != null && flashMaterialInstance.HasProperty(flashMaterialProperty))
                {
                    materialBaseColor = flashMaterialProperty == "_Color"
                        ? flashMaterialInstance.GetColor("_Color")
                        : Color.white;
                }
            }
        }

        private void OnEnable()
        {
            if (hasStarted)
            {
                ResetToIdle();
            }
        }

        private void OnDisable()
        {
            StopAllTweens();
        }

        private void OnDestroy()
        {
            StopAllTweens();
            if (flashMaterialInstance != null && meshFlashRenderer != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(flashMaterialInstance);
                }
                else
#endif
                {
                    Destroy(flashMaterialInstance);
                }
            }
        }

        private void Start()
        {
            CacheStartPosition(force: true);
            hasStarted = true;
            ResetToIdle(immediate: true);
        }

        public AttackAnimProfile GetProfile() => profile;

        public void SetDirection(float direction)
        {
            directionMultiplier = Mathf.Approximately(direction, 0f) ? 1f : Mathf.Sign(direction);
        }

        public void PlayAttackAnimation(System.Action onStrike = null, System.Action onComplete = null)
        {
            if (profile == null || root == null)
            {
                return;
            }

            CacheStartPosition();
            StopAllTweens();
            ResetFlashImmediate();

            activeSequence = DOTween.Sequence();
            float dir = Mathf.Approximately(directionMultiplier, 0f) ? 1f : Mathf.Sign(directionMultiplier);

            AppendPreDelay(activeSequence);

            if (useRectTransform)
            {
                float baseX = startAnchoredPosition.x;
                activeSequence.Append(
                    rectRoot.DOAnchorPosX(baseX - dir * profile.windupBackDist, profile.windupTime)
                        .SetEase(profile.windupEase));

                activeSequence.Append(
                    rectRoot.DOAnchorPosX(baseX + dir * profile.lungeForwardDist, profile.lungeTime)
                        .SetEase(profile.lungeEase));

                activeSequence.AppendCallback(() => onStrike?.Invoke());
                AppendFreeze(activeSequence);
                AppendPushAndRecoverRect(activeSequence, baseX, dir);
            }
            else
            {
                float baseX = startLocalPosition.x;
                activeSequence.Append(
                    root.DOLocalMoveX(baseX - dir * profile.windupBackDist, profile.windupTime)
                        .SetEase(profile.windupEase));

                activeSequence.Append(
                    root.DOLocalMoveX(baseX + dir * profile.lungeForwardDist, profile.lungeTime)
                        .SetEase(profile.lungeEase));

                activeSequence.AppendCallback(() => onStrike?.Invoke());
                AppendFreeze(activeSequence);
                AppendPushAndRecoverLocal(activeSequence, baseX, dir);
            }

            if (activeSequence != null)
            {
                activeSequence.OnComplete(() =>
                {
                    ResetFlashImmediate();
                    onComplete?.Invoke();
                });
            }
        }

        public void PlayHitFeedback(float direction = 0f)
        {
            if (profile == null || root == null)
            {
                return;
            }

            StopHitTween();
            CacheStartPosition();

            float dir = Mathf.Approximately(direction, 0f) ? 1f : Mathf.Sign(direction);

            if (useRectTransform)
            {
                float baseX = startAnchoredPosition.x;
                hitTween = DOTween.Sequence()
                    .Append(rectRoot.DOAnchorPosX(baseX + dir * profile.hitPushDistance, profile.hitPushDuration)
                        .SetEase(profile.hitPushEase))
                    .Append(rectRoot.DOAnchorPosX(baseX, profile.hitReturnDuration)
                        .SetEase(profile.hitReturnEase));
            }
            else
            {
                float baseX = startLocalPosition.x;
                hitTween = DOTween.Sequence()
                    .Append(root.DOLocalMoveX(baseX + dir * profile.hitPushDistance, profile.hitPushDuration)
                        .SetEase(profile.hitPushEase))
                    .Append(root.DOLocalMoveX(baseX, profile.hitReturnDuration)
                        .SetEase(profile.hitReturnEase));
            }
        }

        public void ResetToIdle(bool immediate = false)
        {
            if (root == null)
            {
                return;
            }

            CacheStartPosition();
            StopAllTweens();

            if (useRectTransform)
            {
                if (immediate)
                {
                    rectRoot.anchoredPosition3D = startAnchoredPosition;
                }
                else
                {
                    rectRoot.DOAnchorPos(startAnchoredPosition, profile != null ? profile.recoverTime : 0.15f)
                        .SetEase(profile != null ? profile.recoverEase : Ease.OutSine);
                }
            }
            else
            {
                if (immediate)
                {
                    root.localPosition = startLocalPosition;
                }
                else
                {
                    var target = startLocalPosition;
                    root.DOLocalMove(target, profile != null ? profile.recoverTime : 0.15f)
                        .SetEase(profile != null ? profile.recoverEase : Ease.OutSine);
                }
            }
        }

        public void PauseAnim()
        {
            activeSequence?.Pause();
            hitTween?.Pause();
        }

        public void ResumeAnim()
        {
            activeSequence?.Play();
            hitTween?.Play();
        }

        private void StopAllTweens()
        {
            if (activeSequence != null)
            {
                activeSequence.Kill();
                activeSequence = null;
            }

            if (root != null)
            {
                DOTween.Kill(root, complete: false);
            }

            StopHitTween();
            ResetFlashImmediate();
        }

        private void StopHitTween()
        {
            if (hitTween != null)
            {
                hitTween.Kill();
                hitTween = null;
            }
        }

        private void CacheStartPosition(bool force = false)
        {
            if (root == null)
            {
                return;
            }

            if (!force && hasCachedStart)
            {
                return;
            }

            if (useRectTransform)
            {
                startAnchoredPosition = rectRoot.anchoredPosition3D;
            }
            else
            {
                startLocalPosition = root.localPosition;
            }

            hasCachedStart = true;
        }

        private void AppendPreDelay(Sequence sequence)
        {
            if (sequence == null || profile == null)
            {
                return;
            }

            float total = Mathf.Max(0f, profile.preDelayTotal);
            int flashes = Mathf.Max(0, profile.preFlashes);

            if (flashes <= 0 || total <= 0f)
            {
                if (total > 0f)
                {
                    sequence.AppendInterval(total);
                }
                return;
            }

            float flashHalf = total / (flashes * 2f);
            for (int i = 0; i < flashes; i++)
            {
                sequence.Append(FlashTo(1f, flashHalf));
                sequence.Append(FlashTo(0f, flashHalf));
            }
        }

        private void AppendFreeze(Sequence sequence)
        {
            if (sequence == null || profile == null)
            {
                return;
            }

            if (profile.hitFreeze > 0f)
            {
                sequence.AppendInterval(profile.hitFreeze);
            }
        }

        private void AppendPushAndRecoverRect(Sequence sequence, float baseX, float dir)
        {
            if (sequence == null || profile == null)
            {
                return;
            }

            float lungeX = baseX + dir * profile.lungeForwardDist;
            float pushDistance = Mathf.Max(0f, profile.hitPushDistance);
            float pushDuration = Mathf.Max(0f, profile.hitPushDuration);
            float returnDuration = Mathf.Max(0f, profile.hitReturnDuration);

            if (pushDistance > 0f && pushDuration > 0f)
            {
                float pushX = lungeX + dir * pushDistance;
                sequence.Append(
                    rectRoot.DOAnchorPosX(pushX, pushDuration)
                        .SetEase(profile.hitPushEase));

                if (returnDuration > 0f)
                {
                    sequence.Append(
                        rectRoot.DOAnchorPosX(lungeX, returnDuration)
                            .SetEase(profile.hitReturnEase));
                }
            }

            sequence.Append(
                rectRoot.DOAnchorPosX(baseX, profile.recoverTime)
                    .SetEase(profile.recoverEase));
        }

        private void AppendPushAndRecoverLocal(Sequence sequence, float baseX, float dir)
        {
            if (sequence == null || profile == null)
            {
                return;
            }

            float lungeX = baseX + dir * profile.lungeForwardDist;
            float pushDistance = Mathf.Max(0f, profile.hitPushDistance);
            float pushDuration = Mathf.Max(0f, profile.hitPushDuration);
            float returnDuration = Mathf.Max(0f, profile.hitReturnDuration);

            if (pushDistance > 0f && pushDuration > 0f)
            {
                float pushX = lungeX + dir * pushDistance;
                sequence.Append(
                    root.DOLocalMoveX(pushX, pushDuration)
                        .SetEase(profile.hitPushEase));

                if (returnDuration > 0f)
                {
                    sequence.Append(
                        root.DOLocalMoveX(lungeX, returnDuration)
                            .SetEase(profile.hitReturnEase));
                }
            }

            sequence.Append(
                root.DOLocalMoveX(baseX, profile.recoverTime)
                    .SetEase(profile.recoverEase));
        }

        private Tween FlashTo(float intensity, float duration)
        {
            float clamped = Mathf.Clamp01(intensity);
            float flashAmount = Mathf.Clamp01(clamped * profile.preFlashIntensity);

            if (spriteFlashRenderer != null)
            {
                Color target = Color.Lerp(spriteBaseColor, flashColor, flashAmount);
                return spriteFlashRenderer
                    .DOColor(target, duration)
                    .SetEase(profile.preFlashEase);
            }

            if (flashMaterialInstance != null && flashMaterialInstance.HasProperty(flashMaterialProperty))
            {
                if (flashMaterialProperty == "_Color")
                {
                    Color target = Color.Lerp(materialBaseColor, flashColor, flashAmount);

                    return DOTween
                        .To(() => flashMaterialInstance.GetColor("_Color"),
                            c => flashMaterialInstance.SetColor("_Color", c),
                            target,
                            duration)
                        .SetEase(profile.preFlashEase);
                }

                float value = flashAmount;
                return DOTween
                        .To(() => flashMaterialInstance.GetFloat(flashMaterialProperty),
                            v => flashMaterialInstance.SetFloat(flashMaterialProperty, v),
                            value,
                            duration)
                        .SetEase(profile.preFlashEase);
            }

            return DOVirtual.DelayedCall(duration, () => { });
        }

        private void ResetFlashImmediate()
        {
            if (spriteFlashRenderer != null)
            {
                spriteFlashRenderer.color = spriteBaseColor;
            }

            if (flashMaterialInstance != null && flashMaterialInstance.HasProperty(flashMaterialProperty))
            {
                if (flashMaterialProperty == "_Color")
                {
                    flashMaterialInstance.SetColor("_Color", materialBaseColor);
                }
                else
                {
                    flashMaterialInstance.SetFloat(flashMaterialProperty, 0f);
                }
            }
        }
    }
}
