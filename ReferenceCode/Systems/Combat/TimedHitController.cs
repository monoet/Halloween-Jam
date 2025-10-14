using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Controlador simple para Timed Hits. Muestra una barra con un indicador que se mueve y calcula el resultado.
/// No genera críticos: devuelve un multiplicador (Perfect/Good/Miss) indicado por ActionData.
/// </summary>
public class TimedHitController : MonoBehaviour
{
    [Header("UI (opcional)")]
    [SerializeField] private RectTransform uiRoot;
    [SerializeField] private RectTransform barContainer;
    [SerializeField] private Image barFill;
    [SerializeField] private Image targetZone;
    [SerializeField] private Image marker;

    [Header("Creación runtime si faltan refs")]
    [SerializeField] private bool buildRuntimeUIIfMissing = true;
    [SerializeField] private Vector2 runtimeBarSize = new Vector2(400, 18);

    private bool running;
    private float duration;
    private float targetPos;
    private float perfectHalf;
    private float goodHalf;
    private Action<float> onResolved; // devuelve multiplicador
    private float t;
    private bool pressed;
    private float curPerfectMult = 1f;
    private float curGoodMult = 1f;
    private float curMissMult = 0f;
    private CanvasGroup canvasGroup;

    private void EnsureUI()
    {
        if (!buildRuntimeUIIfMissing) return;
        if (uiRoot == null)
        {
            var canvasGO = new GameObject("TimedHitUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4000;
            uiRoot = canvasGO.AddComponent<RectTransform>();
            uiRoot.anchorMin = Vector2.zero; uiRoot.anchorMax = Vector2.one; uiRoot.offsetMin = Vector2.zero; uiRoot.offsetMax = Vector2.zero;
            canvasGroup = canvasGO.GetComponent<CanvasGroup>();
        }
        if (barContainer == null)
        {
            var go = new GameObject("BarContainer", typeof(RectTransform));
            go.transform.SetParent(uiRoot, false);
            barContainer = go.GetComponent<RectTransform>();
            barContainer.sizeDelta = runtimeBarSize;
            barContainer.anchorMin = new Vector2(0.5f, 0.15f);
            barContainer.anchorMax = new Vector2(0.5f, 0.15f);
            barContainer.anchoredPosition = Vector2.zero;
        }
        if (barFill == null)
        {
            var bg = new GameObject("BarBG", typeof(Image));
            bg.transform.SetParent(barContainer, false);
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            var bgrt = bg.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;

            var fill = new GameObject("Progress", typeof(Image));
            fill.transform.SetParent(barContainer, false);
            barFill = fill.GetComponent<Image>();
            barFill.color = new Color(0.3f, 0.9f, 1f, 1f);
            barFill.type = Image.Type.Filled; barFill.fillMethod = Image.FillMethod.Horizontal; barFill.fillOrigin = (int)Image.OriginHorizontal.Left; barFill.fillAmount = 0f;
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        }
        if (targetZone == null)
        {
            var tz = new GameObject("TargetZone", typeof(Image));
            tz.transform.SetParent(barContainer, false);
            targetZone = tz.GetComponent<Image>();
            targetZone.color = new Color(1f, 0.9f, 0.2f, 0.6f);
            var trt = tz.GetComponent<RectTransform>();
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(0f, 1f); trt.sizeDelta = new Vector2(8f, 0f);
        }
        if (marker == null)
        {
            var mk = new GameObject("Marker", typeof(Image));
            mk.transform.SetParent(barContainer, false);
            marker = mk.GetComponent<Image>();
            marker.color = new Color(1f, 1f, 1f, 1f);
            var mrt = mk.GetComponent<RectTransform>();
            mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.anchorMin = new Vector2(0f, 0f); mrt.anchorMax = new Vector2(0f, 1f); mrt.sizeDelta = new Vector2(4f, 0f);
        }
    }

    public void StartTimedHit(ActionData data, Action<float> onResult)
    {
        if (running) return;
        EnsureUI();
        onResolved = onResult;
        duration = Mathf.Max(0.1f, data.TimedDuration);
        targetPos = Mathf.Clamp01(data.TimedTarget);
        perfectHalf = Mathf.Clamp01(data.TimedPerfectWindow * 0.5f);
        goodHalf = Mathf.Clamp01(data.TimedGoodWindow * 0.5f);
        curPerfectMult = data.PerfectMultiplier;
        curGoodMult = data.GoodMultiplier;
        curMissMult = data.MissMultiplier;
        t = 0f; pressed = false; running = true;

        if (barFill != null) barFill.fillAmount = 0f;
        if (targetZone != null)
        {
            var trt = targetZone.rectTransform;
            float xNorm = targetPos;
            trt.anchoredPosition = new Vector2(xNorm * barContainer.rect.width, 0f);
            trt.sizeDelta = new Vector2(Mathf.Max(8f, barContainer.rect.width * data.TimedGoodWindow), 0f);
        }
        if (marker != null)
            marker.rectTransform.anchoredPosition = Vector2.zero;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!running) return;
        t += Time.deltaTime;
        float norm = Mathf.Clamp01(t / duration);
        if (barFill != null) barFill.fillAmount = norm;
        if (marker != null)
            marker.rectTransform.anchoredPosition = new Vector2(norm * barContainer.rect.width, 0f);

        if (!pressed && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
            pressed = true;
            Resolve(norm);
        }

        if (t >= duration && !pressed)
        {
            pressed = true; // auto-resolve as miss if no input
            Resolve(norm);
        }
    }

    private void Resolve(float pos)
    {
        running = false;
        float delta = Mathf.Abs(pos - targetPos);
        float mult;
        if (delta <= perfectHalf) mult = curPerfectMult;
        else if (delta <= goodHalf) mult = curGoodMult;
        else mult = curMissMult;

        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(0f, 0.12f).SetEase(Ease.InSine).OnComplete(() => onResolved?.Invoke(mult));
        }
        else
        {
            onResolved?.Invoke(mult);
        }
    }
}
