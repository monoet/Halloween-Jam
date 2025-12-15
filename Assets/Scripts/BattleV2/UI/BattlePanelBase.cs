using UnityEngine;
using DG.Tweening;

namespace BattleV2.UI
{
    /// <summary>
    /// Base class for all Battle UI panels.
    /// Handles Show/Hide animations using CanvasGroup and DOTween.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BattlePanelBase : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] protected float fadeDuration = 0.25f;
        [SerializeField] protected Ease showEase = Ease.OutQuad;
        [SerializeField] protected Ease hideEase = Ease.InQuad;

        protected CanvasGroup canvasGroup;
        protected Tweener currentTween;

        protected virtual void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        public virtual void Show(bool instant = false)
        {
            EnsureInitialized();
            currentTween?.Kill();
            
            gameObject.SetActive(true);
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            if (instant)
            {
                canvasGroup.alpha = 1f;
            }
            else
            {
                canvasGroup.alpha = 0f;
                currentTween = canvasGroup.DOFade(1f, fadeDuration).SetEase(showEase).SetUpdate(true);
            }
        }

        public virtual void Hide(bool instant = false)
        {
            EnsureInitialized();
            currentTween?.Kill();

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (instant)
            {
                canvasGroup.alpha = 0f;
                gameObject.SetActive(false);
            }
            else
            {
                currentTween = canvasGroup.DOFade(0f, fadeDuration)
                    .SetEase(hideEase)
                    .SetUpdate(true)
                    .OnComplete(() => gameObject.SetActive(false));
            }
        }

        /// <summary>
        /// Override this to define default focus logic (e.g. select first button).
        /// </summary>
        public abstract void FocusFirst();
    }
}
