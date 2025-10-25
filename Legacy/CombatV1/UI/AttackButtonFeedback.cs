using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HalloweenJam.UI.Combat
{
    /// <summary>
    /// Simple DOTween-driven feedback for the attack button:
    /// - Slight scale grow on hover.
    /// - Press animation with a quick squash/stretch.
    /// - Optional glow via CanvasGroup alpha.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AttackButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Scale")]
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float hoverDuration = 0.175f;
        [SerializeField] private float pressScale = 0.92f;
        [SerializeField] private float pressDuration = 0.1f;

        [Header("Glow")]
        [SerializeField] private CanvasGroup optionalGlow;
        [SerializeField, Range(0f, 1f)] private float hoverGlowAlpha = 0.35f;
        [SerializeField] private float glowDuration = 0.15f;

        [Header("Text Color")]
        [SerializeField] private TMP_Text optionalLabel;
        [SerializeField] private Color hoverTextColor = Color.white;
        [SerializeField] private Color normalTextColor = Color.white;
        [SerializeField] private float textColorDuration = 0.15f;

        private Button button;
        private Tween scaleTween;
        private Tween glowTween;
        private Tween textTween;
        private Vector3 baseScale;

        private void Awake()
        {
            button = GetComponent<Button>();
            baseScale = transform.localScale;

            if (optionalGlow != null)
            {
                optionalGlow.alpha = 0f;
            }

            if (optionalLabel == null)
            {
                optionalLabel = GetComponentInChildren<TMP_Text>();
            }

            if (optionalLabel != null)
            {
                normalTextColor = optionalLabel.color;
            }
        }

        private void OnDisable()
        {
            KillTweens();
            transform.localScale = baseScale;

            if (optionalGlow != null)
            {
                optionalGlow.alpha = 0f;
            }

            if (optionalLabel != null)
            {
                optionalLabel.color = normalTextColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!button.interactable)
            {
                return;
            }

            AnimateScale(hoverScale, hoverDuration);
            AnimateGlow(hoverGlowAlpha, glowDuration);
            AnimateTextColor(hoverTextColor, textColorDuration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateScale(baseScale.x, hoverDuration);
            AnimateGlow(0f, glowDuration);
            AnimateTextColor(normalTextColor, textColorDuration);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!button.interactable)
            {
                return;
            }

            AnimateScale(pressScale, pressDuration);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!button.interactable)
            {
                return;
            }

            AnimateScale(hoverScale, pressDuration);
        }

        private void AnimateScale(float targetScale, float duration)
        {
            KillTween(ref scaleTween);
            scaleTween = transform.DOScale(baseScale * targetScale, duration)
                .SetEase(Ease.OutQuad);
        }

        private void AnimateGlow(float alpha, float duration)
        {
            if (optionalGlow == null)
            {
                return;
            }

            KillTween(ref glowTween);
            glowTween = optionalGlow.DOFade(alpha, duration)
                .SetEase(Ease.OutQuad);
        }

        private void AnimateTextColor(Color color, float duration)
        {
            if (optionalLabel == null)
            {
                return;
            }

            KillTween(ref textTween);
            textTween = optionalLabel.DOColor(color, duration)
                .SetEase(Ease.OutQuad);
        }

        private void KillTweens()
        {
            KillTween(ref scaleTween);
            KillTween(ref glowTween);
            KillTween(ref textTween);
        }

        private static void KillTween(ref Tween tween)
        {
            tween?.Kill();
            tween = null;
        }
    }
}
