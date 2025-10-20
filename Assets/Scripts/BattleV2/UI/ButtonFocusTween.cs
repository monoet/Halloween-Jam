using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleV2.UI
{
    /// <summary>
    /// Plays a simple scale tween whenever the selectable receives focus.
    /// Works with keyboard/gamepad navigation via EventSystem.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ButtonFocusTween : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private float focusScale = 1.1f;
        [SerializeField] private float tweenDuration = 0.18f;
        [SerializeField] private Ease ease = Ease.OutQuad;
        [SerializeField] private bool ignoreTimeScale = true;

        private Vector3 defaultScale;
        private Tween activeTween;
        private bool isFocused;

        private void Awake()
        {
            if (target == null)
            {
                target = transform as RectTransform;
            }

            defaultScale = target != null ? target.localScale : Vector3.one;
        }

        private void OnEnable()
        {
            // Ensure the scale matches the current focus state when re-enabled.
            if (target != null)
            {
                target.localScale = isFocused ? defaultScale * focusScale : defaultScale;
            }
        }

        private void OnDisable()
        {
            KillTween();
            if (target != null)
            {
                target.localScale = defaultScale;
            }
            isFocused = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            isFocused = true;
            PlayTween(defaultScale * focusScale);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isFocused = false;
            PlayTween(defaultScale);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnSelect(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isFocused)
            {
                PlayTween(defaultScale);
            }
        }

        private void PlayTween(Vector3 targetScale)
        {
            if (target == null)
            {
                return;
            }

            KillTween();
            activeTween = target.DOScale(targetScale, tweenDuration)
                .SetEase(ease)
                .SetUpdate(ignoreTimeScale)
                .OnComplete(() => activeTween = null);
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
