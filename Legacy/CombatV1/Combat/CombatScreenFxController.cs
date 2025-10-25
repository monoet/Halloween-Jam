using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides simple screen-wide effects such as scanline flashes for combat feedback.
/// </summary>
public class CombatScreenFxController : MonoBehaviour
{
    [Header("Scanlines")]
    [SerializeField] private float scanlineAlpha = 0.15f;
    [SerializeField] private float scanlineFlashDuration = 0.25f;
    [SerializeField] private int patternHeight = 4;

    private Canvas canvas;
    private Image overlay;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        EnsureOverlay();
    }

    public void PlayScanlinesFlash()
    {
        EnsureOverlay();
        canvasGroup.DOKill();
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(scanlineAlpha, scanlineFlashDuration * 0.5f)
                    .SetEase(Ease.OutSine)
                    .OnComplete(() => canvasGroup.DOFade(0f, scanlineFlashDuration).SetEase(Ease.InSine));
    }

    private void EnsureOverlay()
    {
        if (canvas == null)
        {
            var go = new GameObject("CombatScreenFX_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            canvasGroup = go.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
        }

        if (overlay == null)
        {
            var overlayGo = new GameObject("ScanlinesOverlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(canvas.transform, false);
            var rt = overlayGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            overlay = overlayGo.GetComponent<Image>();
            overlay.color = Color.white;
            overlay.type = Image.Type.Tiled;
            overlay.sprite = GenerateScanlineSprite();
        }
    }

    private Sprite GenerateScanlineSprite()
    {
        int width = 2;
        int height = Mathf.Max(2, patternHeight);
        var texture = new Texture2D(width, height, TextureFormat.ARGB32, false)
        {
            filterMode = FilterMode.Point
        };

        for (int y = 0; y < height; y++)
        {
            bool isLine = y == 0;
            Color color = isLine ? new Color(0f, 0f, 0f, 1f) : new Color(0f, 0f, 0f, 0f);
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}
