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

        public RectTransform Target => target;
        public float FocusScale
        {
            get => focusScale;
            set => focusScale = value;
        }

        public float TweenDuration
        {
            get => tweenDuration;
            set => tweenDuration = Mathf.Max(0f, value);
        }

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

        public void SetTarget(RectTransform newTarget)
        {
            if (newTarget == null)
            {
                return;
            }

            target = newTarget;
            defaultScale = target.localScale == Vector3.zero ? Vector3.one : target.localScale;
        }

        public void Configure(float scaleMultiplier, float durationSeconds, Ease easing)
        {
            focusScale = scaleMultiplier;
            tweenDuration = Mathf.Max(0f, durationSeconds);
            ease = easing;
        }

        public void ResetImmediate()
        {
            KillTween();
            if (target != null)
            {
                target.localScale = defaultScale;
            }
            isFocused = false;
        }

        public void ApplyState(bool focused, bool instant = false)
        {
            isFocused = focused;
            if (target == null)
            {
                return;
            }

            var targetScale = focused ? defaultScale * focusScale : defaultScale;

            if (instant)
            {
                KillTween();
                target.localScale = targetScale;
            }
            else
            {
                PlayTween(targetScale);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            ApplyState(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            ApplyState(false);
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
