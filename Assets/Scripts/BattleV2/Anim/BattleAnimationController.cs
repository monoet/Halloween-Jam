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

        private bool useRectTransform;
        private RectTransform rectRoot;
        private Vector3 startLocalPosition;
        private Vector3 startAnchoredPosition;
        private Sequence activeSequence;
        private Tween hitTween;
        private bool hasCachedStart;
        private bool hasStarted;

        public Vector3 WorldPosition => root != null ? root.position : transform.position;

        private void Awake()
        {
            if (root == null)
            {
                root = transform;
            }

            rectRoot = root as RectTransform;
            useRectTransform = rectRoot != null;
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

            activeSequence = DOTween.Sequence();
            float dir = Mathf.Approximately(directionMultiplier, 0f) ? 1f : Mathf.Sign(directionMultiplier);

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
                activeSequence.AppendInterval(profile.hitFreeze);

                activeSequence.Append(
                    rectRoot.DOAnchorPosX(baseX, profile.recoverTime)
                        .SetEase(profile.recoverEase));
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
                activeSequence.AppendInterval(profile.hitFreeze);

                activeSequence.Append(
                    root.DOLocalMoveX(baseX, profile.recoverTime)
                        .SetEase(profile.recoverEase));
            }

            if (activeSequence != null)
            {
                activeSequence.OnComplete(() => onComplete?.Invoke());
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
    }
}
