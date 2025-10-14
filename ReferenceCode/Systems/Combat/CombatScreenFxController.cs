using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Controla overlays de pantalla como scanlines retro.
/// Genera un Canvas e Image si no existen.
/// </summary>
public class CombatScreenFxController : MonoBehaviour
{
    [Header("Scanlines")]
    [SerializeField] private float scanlineAlpha = 0.15f;
    [SerializeField] private float scanlineFlashDuration = 0.25f;
    [SerializeField] private int patternHeight = 4; // altura del patrón (línea + hueco)

    private Canvas canvas;
    private Image overlay;
    private CanvasGroup group;

    private void Awake()
    {
        EnsureOverlay();
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
            group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
        }

        if (overlay == null)
        {
            var imgGo = new GameObject("ScanlinesOverlay", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(canvas.transform, false);
            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            overlay = imgGo.GetComponent<Image>();
            overlay.color = Color.white;
            overlay.type = Image.Type.Tiled;
            overlay.sprite = GenerateScanlineSprite();
        }
    }

    private Sprite GenerateScanlineSprite()
    {
        int w = 2; int h = Mathf.Max(2, patternHeight);
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Point;
        for (int y = 0; y < h; y++)
        {
            bool isLine = (y % h) == 0; // primera fila sólida
            Color c = isLine ? new Color(0f, 0f, 0f, 1f) : new Color(0f, 0f, 0f, 0f);
            for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        var spr = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return spr;
    }

    public void PlayScanlinesFlash()
    {
        EnsureOverlay();
        group.DOKill();
        group.alpha = 0f;
        group.DOFade(scanlineAlpha, scanlineFlashDuration * 0.5f)
             .SetEase(Ease.OutSine)
             .OnComplete(() => group.DOFade(0f, scanlineFlashDuration).SetEase(Ease.InSine));
    }
}

