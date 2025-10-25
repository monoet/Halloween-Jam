using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace BattleV2.UI.ActionMenu
{
    public class KeepsakeCardAttackStrategy : MonoBehaviour, IAttackSubmenuStrategy
    {
        [SerializeField] private RectTransform cardsRoot;
        [SerializeField] private KeepsakeCardSlot[] cardSlots;
        [SerializeField] private float slideOffset = 120f;
        [SerializeField] private float slideDuration = 0.2f;
        [SerializeField] private Ease slideEase = Ease.OutQuad;

        private readonly List<ActionMenuOption> optionCache = new();
        private ActionMenuContext context;
        private int currentIndex;
        private Tween currentTween;

        public void Initialise(ActionMenuContext ctx)
        {
            context = ctx;
            Hide();
        }

        public void Show(IReadOnlyList<ActionMenuOption> options)
        {
            optionCache.Clear();
            optionCache.AddRange(options);
            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, optionCache.Count - 1));

            if (cardsRoot != null)
            {
                cardsRoot.gameObject.SetActive(true);
                cardsRoot.anchoredPosition = Vector2.zero;
            }

            if (cardSlots == null || cardSlots.Length == 0)
            {
                return;
            }

            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] == null)
                {
                    continue;
                }

                if (i < optionCache.Count)
                {
                    var option = optionCache[i];
                    cardSlots[i].SetContent(option.DisplayName, option.Description);
                    cardSlots[i].SetActive(true);
                }
                else
                {
                    cardSlots[i].SetActive(false);
                }
            }

            PlaySlideAnimation();
            HighlightCurrent();
        }

        public void Hide()
        {
            optionCache.Clear();
            if (cardsRoot != null)
            {
                cardsRoot.gameObject.SetActive(false);
            }

            if (cardSlots != null)
            {
                for (int i = 0; i < cardSlots.Length; i++)
                {
                    if (cardSlots[i] != null)
                    {
                        cardSlots[i].SetActive(false);
                    }
                }
            }
        }

        public bool HandleInput(ActionMenuInput input)
        {
            if (optionCache.Count == 0)
            {
                return false;
            }

            bool consumed = false;

            if (input.Horizontal != 0)
            {
                currentIndex = Mathf.Clamp(currentIndex + input.Horizontal, 0, optionCache.Count - 1);
                HighlightCurrent();
                PlaySlideAnimation();
                consumed = true;
            }

            if (input.ConfirmPressed)
            {
                context?.OnOptionSelected?.Invoke(optionCache[currentIndex]);
                consumed = true;
            }

            if (input.CancelPressed)
            {
                context?.OnBackRequested?.Invoke();
                consumed = true;
            }

            if (input.ChargeHeld)
            {
                context?.OnChargeRequested?.Invoke();
                consumed = true;
            }

            return consumed;
        }

        private void HighlightCurrent()
        {
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] == null)
                {
                    continue;
                }

                float targetAlpha = (i == currentIndex) ? 1f : 0.4f;
                if (cardSlots[i].Group != null)
                {
                    cardSlots[i].Group.alpha = targetAlpha;
                }
            }
        }

        private void PlaySlideAnimation()
        {
            if (cardsRoot == null)
            {
                return;
            }

            if (currentTween != null && currentTween.IsActive())
            {
                currentTween.Kill();
            }

            Vector2 targetPos = Vector2.zero;
            if (currentIndex > 0)
            {
                targetPos = new Vector2(-slideOffset * currentIndex, 0f);
            }

            currentTween = cardsRoot.DOAnchorPos(targetPos, slideDuration).SetEase(slideEase);
        }
    }
}
