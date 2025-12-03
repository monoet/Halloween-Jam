using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

namespace BattleV2.UI
{
    /// <summary>
    /// Adds a simple scale/color effect when selected. Visual only; audio handled elsewhere.
    /// Requires DOTween.
    /// </summary>
    public class UISelectionFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [Header("Scale Settings")]
        [SerializeField] private bool useScale = true;
        [SerializeField] private float scaleAmount = 1.1f;

        [Header("Color Settings")]
        [SerializeField] private bool useColor = true;
        [SerializeField] private Color selectedColor = Color.yellow;
        [SerializeField] private Image targetImage;

        [Header("Common")]
        [SerializeField] private float duration = 0.2f;

        private Vector3 originalScale;
        private Color originalColor;
        private Tweener scaleTween;
        private Tweener colorTween;

        private void Awake()
        {
            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }
            CacheOriginals();
        }

        private void OnEnable()
        {
            CacheOriginals();
        }

        private void CacheOriginals()
        {
            originalScale = transform.localScale;
            if (targetImage != null)
            {
                originalColor = targetImage.color;
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (useScale)
            {
                scaleTween?.Kill();
                scaleTween = transform.DOScale(originalScale * scaleAmount, duration).SetUpdate(true);
            }

            if (useColor && targetImage != null)
            {
                colorTween?.Kill();
                colorTween = targetImage.DOColor(selectedColor, duration).SetUpdate(true);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (useScale)
            {
                scaleTween?.Kill();
                scaleTween = transform.DOScale(originalScale, duration).SetUpdate(true);
            }

            if (useColor && targetImage != null)
            {
                colorTween?.Kill();
                colorTween = targetImage.DOColor(originalColor, duration).SetUpdate(true);
            }
        }

        private void OnDisable()
        {
            scaleTween?.Kill();
            colorTween?.Kill();
            scaleTween = null;
            colorTween = null;
            transform.localScale = originalScale;
            if (targetImage != null)
            {
                targetImage.color = originalColor;
            }
        }
    }
}
