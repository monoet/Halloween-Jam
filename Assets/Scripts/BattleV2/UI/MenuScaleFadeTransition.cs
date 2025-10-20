using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Handles simple scale + fade tweens for battle menus.
    /// </summary>
    public class MenuScaleFadeTransition : MonoBehaviour
    {
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private float openDelay = 0.05f;
        [SerializeField] private float closeDelay = 0f;
        [SerializeField] private Ease openEase = Ease.OutBack;
        [SerializeField] private Ease closeEase = Ease.InBack;

        private readonly Dictionary<GameObject, Vector3> defaultScales = new();
        private readonly Dictionary<GameObject, Tween> activeTweens = new();

        public void PlayOpen(GameObject menu)
        {
            if (!TryPrepare(menu, out var rect, out var canvasGroup))
            {
                return;
            }

            var targetScale = GetDefaultScale(menu, rect);

            KillTween(menu);

            rect.localScale = Vector3.zero;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            var tween = DOTween.Sequence().SetUpdate(true);
            if (openDelay > 0f)
            {
                tween.AppendInterval(openDelay);
            }

            tween.Join(rect.DOScale(targetScale, duration).SetEase(openEase));

            if (canvasGroup != null)
            {
                tween.Join(canvasGroup.DOFade(1f, duration).SetEase(openEase));
            }

            tween.OnComplete(() =>
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }

                activeTweens.Remove(menu);
            });

            activeTweens[menu] = tween;
        }

        public void PlayClose(GameObject menu, Action onComplete = null)
        {
            if (!TryPrepare(menu, out var rect, out var canvasGroup))
            {
                onComplete?.Invoke();
                return;
            }

            var targetScale = GetDefaultScale(menu, rect);

            KillTween(menu);

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            var tween = DOTween.Sequence().SetUpdate(true);
            if (closeDelay > 0f)
            {
                tween.AppendInterval(closeDelay);
            }

            tween.Join(rect.DOScale(Vector3.zero, duration).SetEase(closeEase));

            if (canvasGroup != null)
            {
                tween.Join(canvasGroup.DOFade(0f, duration).SetEase(closeEase));
            }

            tween.OnComplete(() =>
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                }

                activeTweens.Remove(menu);
                rect.localScale = targetScale;
                onComplete?.Invoke();
            });

            activeTweens[menu] = tween;
        }

        private bool TryPrepare(GameObject menu, out RectTransform rect, out CanvasGroup canvasGroup)
        {
            rect = null;
            canvasGroup = null;

            if (menu == null)
            {
                return false;
            }

            rect = menu.transform as RectTransform;
            if (rect == null)
            {
                return false;
            }

            canvasGroup = menu.GetComponent<CanvasGroup>();
            return true;
        }

        private Vector3 GetDefaultScale(GameObject menu, RectTransform rect)
        {
            if (!defaultScales.TryGetValue(menu, out var scale))
            {
                scale = rect.localScale == Vector3.zero ? Vector3.one : rect.localScale;
                defaultScales[menu] = scale;
            }

            return scale;
        }

        private void KillTween(GameObject menu)
        {
            if (menu == null)
            {
                return;
            }

            if (activeTweens.TryGetValue(menu, out var tween) && tween.IsActive())
            {
                tween.Kill();
            }

            activeTweens.Remove(menu);
        }
    }
}
