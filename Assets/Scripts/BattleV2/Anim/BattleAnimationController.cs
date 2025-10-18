using UnityEngine;
using DG.Tweening;

namespace BattleV2.Anim
{
    /// <summary>
    /// Tween-based helper that performs the classic attack loop (windup -> lunge -> freeze -> recover)
    /// and keeps DOTween sequences encapsulated for pause/resume/reset requests.
    /// </summary>
    public class BattleAnimationController : MonoBehaviour
    {
        [SerializeField] private AttackAnimProfile profile;
        [SerializeField] private Transform root;

        private Vector3 startLocalPosition;
        private Sequence activeSequence;
        private Tween hitShakeTween;

        private void Awake()
        {
            if (root == null)
            {
                root = transform;
            }

            startLocalPosition = root.localPosition;
        }

        private void OnEnable()
        {
            ResetToIdle();
        }

        private void OnDisable()
        {
            StopAllTweens();
        }

        private void OnDestroy()
        {
            StopAllTweens();
        }

        public AttackAnimProfile GetProfile() => profile;

        public void PlayAttackAnimation()
        {
            if (profile == null || root == null)
            {
                return;
            }

            StopAllTweens();

            activeSequence = DOTween.Sequence();

            activeSequence.Append(
                root.DOLocalMoveX(startLocalPosition.x - profile.windupBackDist, profile.windupTime)
                    .SetEase(profile.windupEase));

            activeSequence.Append(
                root.DOLocalMoveX(startLocalPosition.x + profile.lungeForwardDist, profile.lungeTime)
                    .SetEase(profile.lungeEase));

            activeSequence.AppendInterval(profile.hitFreeze);

            activeSequence.Append(
                root.DOLocalMoveX(startLocalPosition.x, profile.recoverTime)
                    .SetEase(profile.recoverEase));
        }

        public void PlayHitFeedback()
        {
            if (profile == null || root == null)
            {
                return;
            }

            StopHitShake();
            hitShakeTween = BattleVFX.EnemyHitShake(root, profile);
        }

        public void ResetToIdle()
        {
            StopAllTweens();
            if (root != null)
            {
                root.localPosition = startLocalPosition;
            }
        }

        public void PauseAnim()
        {
            activeSequence?.Pause();
            hitShakeTween?.Pause();
        }

        public void ResumeAnim()
        {
            activeSequence?.Play();
            hitShakeTween?.Play();
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

            StopHitShake();
        }

        private void StopHitShake()
        {
            if (hitShakeTween != null)
            {
                hitShakeTween.Kill();
                hitShakeTween = null;
            }
        }
    }
}
