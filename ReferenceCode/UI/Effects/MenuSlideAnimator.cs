using UnityEngine;
using DG.Tweening;

public class MenuSlideAnimator : MonoBehaviour
{
    [Header("Panels")]
    public RectTransform rootPanel;
    public RectTransform partyPanel;

    [Header("Canvas Fade (opcional)")]
    public CanvasGroup canvasGroup;
    public float fadeDuration = 0.35f;

    [Header("Anim Settings")]
    public float slideDuration = 0.5f;
    public Ease slideEase = Ease.OutQuart;
    public float offscreenOffset = 1600f;

    private Vector2 rootStartPos;
    private Vector2 partyStartPos;
    private bool initialized;

    public float ShowDuration => slideDuration;
    public float HideDuration => Mathf.Max(slideDuration * 0.8f, fadeDuration * 0.8f);

    private void Awake()
    {
        if (!ValidatePanels())
            return;

        CacheStartPositions();
        MovePanelsOffscreen();
        SetupCanvas();
    }

    private bool ValidatePanels()
    {
        if (rootPanel == null || partyPanel == null)
        {
            Debug.LogError("[MenuSlideAnimator] Asigna rootPanel y partyPanel en el inspector.", this);
            enabled = false;
            return false;
        }

        initialized = true;
        return true;
    }

    private void CacheStartPositions()
    {
        rootStartPos = rootPanel.anchoredPosition;
        partyStartPos = partyPanel.anchoredPosition;
    }

    private void MovePanelsOffscreen()
    {
        rootPanel.anchoredPosition = rootStartPos + Vector2.left * offscreenOffset;
        partyPanel.anchoredPosition = partyStartPos + Vector2.right * offscreenOffset;
    }

    private void SetupCanvas()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void SlideIn()
    {
        if (!initialized && !ValidatePanels())
            return;

        if (canvasGroup != null)
        {
            canvasGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutSine);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        rootPanel.DOAnchorPos(rootStartPos, slideDuration).SetEase(slideEase);
        partyPanel.DOAnchorPos(partyStartPos, slideDuration)
                  .SetEase(slideEase)
                  .SetDelay(0.05f);

        Debug.Log("[MenuSlideAnimator] SlideIn ejecutado");
    }

    public void SlideOut()
    {
        if (!initialized && !ValidatePanels())
            return;

        if (canvasGroup != null)
        {
            float fadeOut = fadeDuration * 0.8f;
            canvasGroup.DOFade(0f, fadeOut).SetEase(Ease.InSine);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        float slideOut = slideDuration * 0.8f;
        rootPanel.DOAnchorPos(rootStartPos + Vector2.left * offscreenOffset, slideOut)
                 .SetEase(Ease.InQuart);
        partyPanel.DOAnchorPos(partyStartPos + Vector2.right * offscreenOffset, slideOut)
                  .SetEase(Ease.InQuart);
    }
}

