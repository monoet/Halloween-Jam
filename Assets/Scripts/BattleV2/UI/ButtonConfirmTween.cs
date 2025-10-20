using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleV2.UI
{
    /// <summary>
    /// Plays a quick scale pulse on the button text when confirmed (Submit or click).
    /// </summary>
    public class ButtonConfirmTween : MonoBehaviour, ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private float scaleMultiplier = 1.15f;
        [SerializeField] private float duration = 0.15f;
        [SerializeField] private Ease ease = Ease.OutBack;
        [SerializeField] private bool ignoreTimeScale = true;

        private Vector3 defaultScale = Vector3.one;
        private Tween activeTween;

        private void Awake()
        {
            if (target == null)
            {
                if (TryGetComponent(out TMP_Text text))
                {
                    target = text.rectTransform;
                }
                else
                {
                    target = GetComponentInChildren<TMP_Text>()?.rectTransform;
                }

                if (target == null)
                {
                    target = transform as RectTransform;
                }
            }

            if (target != null && target.localScale != Vector3.zero)
            {
                defaultScale = target.localScale;
            }
        }

        private void OnDisable()
        {
            ResetScale();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            PlayPulse();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PlayPulse();
        }

        private void PlayPulse()
        {
            if (target == null)
            {
                return;
            }

            KillTween();
            target.localScale = defaultScale;

            activeTween = target.DOScale(defaultScale * scaleMultiplier, duration)
                .SetEase(ease)
                .SetUpdate(ignoreTimeScale)
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(ResetScale);
        }

        private void ResetScale()
        {
            KillTween();
            if (target != null)
            {
                target.localScale = defaultScale;
            }
        }

        private void KillTween()
        {
            if (activeTween != null && activeTween.IsActive())
            {
                activeTween.Kill();
            }

            activeTween = null;
        }
    }
}
