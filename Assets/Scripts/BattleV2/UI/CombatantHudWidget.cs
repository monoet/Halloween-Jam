using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using BattleV2.Marks;
using BattleV2.Orchestration;

namespace BattleV2.UI
{
    /// <summary>
    /// Binds a CombatantState to HUD labels, sliders, and optional CP pips.
    /// </summary>
    public class CombatantHudWidget : MonoBehaviour
    {
        [SerializeField] private CombatantState source;

        [Header("Optional Labels")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text spText;
        [SerializeField] private TMP_Text cpText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private bool autoFindPortraitImage = true;
        [SerializeField] private string portraitChildPath = "Portrait";

        [Header("Optional Sliders")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Slider spSlider;
        [SerializeField] private Slider cpSlider;

        [Header("Optional CP Pips")]
        [SerializeField] private GameObject[] cpPips;

        [Header("Marks")]
        [SerializeField] private Image markIcon;
        // MarkService is injected at runtime; not serializable.
        private MarkService markService;
        [SerializeField] private bool markFxEnabled = true;
        [SerializeField, Min(0f)] private float markFlashDuration = 0.3f;
        [SerializeField] private Color detonateFlashColor = Color.white;
        [SerializeField] private Vector3 markApplyPunch = new Vector3(0.05f, 0.05f, 0f);

        [Header("Highlight")]
        [SerializeField] private GameObject highlightRoot;
        [SerializeField] private Color highlightNameColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private bool pulseHighlight = true;
        [SerializeField, Min(0f)] private float pulseSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float pulseScaleAmount = 0.05f;

        [Header("Formatting")]
        [SerializeField] private string hpFormat = "{0}/{1}";
        [SerializeField] private string spFormat = "{0}/{1}";
        [SerializeField] private string cpFormat = "{0}/{1}";

        private UnityAction vitalsListener;
        private bool pendingRefresh;
        private WorldSpaceHudAnchor worldAnchor;
        private Color originalNameColor = Color.white;
        private bool originalColorCaptured;
        private bool isHighlighted;
        private bool marksSubscribed;
        private bool marksInitialized;
        private bool loggedMissingMarksWiring;
        private Vector3 originalHighlightScale = Vector3.one;
        private Coroutine markScaleRoutine;
        private Coroutine markFlashRoutine;
        private Coroutine markAnimRoutine;
        private Vector3 markIconOriginalScale = Vector3.one;

        private void Awake()
        {
            vitalsListener = ScheduleRefresh;
            EnsurePortraitReference();
            worldAnchor = GetComponent<WorldSpaceHudAnchor>();
            CaptureOriginalNameColor();
            ScheduleRefresh();
        }

        private void OnValidate()
        {
            if (cpPips == null)
            {
                return;
            }

            for (int i = 0; i < cpPips.Length; i++)
            {
                var pip = cpPips[i];
                if (pip != null && pip.Equals(null))
                {
                    cpPips[i] = null;
                }
            }

            EnsurePortraitReference();
        }

        private void OnEnable()
        {
            EnsurePortraitReference();
            CacheHighlightScale();
            Subscribe(source);
            if (marksInitialized && markService != null)
            {
                SubscribeMarks();
            }
            ScheduleRefresh();
            SyncAnchorTarget();
            SyncHighlightState();
            SyncMarks();
        }

        private void Start()
        {
            if ((markService == null || !marksInitialized) && !loggedMissingMarksWiring)
            {
                loggedMissingMarksWiring = true;
                Debug.LogError($"[CombatantHudWidget] MarkService not injected on '{name}'. Ensure HUDManager calls InitializeMarks(markService) before enabling the widget. Marks UI disabled.", this);
            }
        }

        private void OnDisable()
        {
            Unsubscribe(source);
            UnsubscribeMarks();
            if (worldAnchor != null)
            {
                worldAnchor.Target = null;
            }
        }

        /// <summary>
        /// Must be called by HUDManager immediately after Instantiate to provide MarkService.
        /// </summary>
        public void InitializeMarks(MarkService service)
        {
            if (marksSubscribed)
            {
                UnsubscribeMarks();
            }

            markService = service;
            marksInitialized = true;

            if (isActiveAndEnabled)
            {
                SubscribeMarks();
                SyncMarks();
            }
        }

        /// <summary>
        /// Binds this widget to the provided combatant state.
        /// </summary>
        public void Bind(CombatantState newSource)
        {
            SetSourceInternal(newSource);

            if (isActiveAndEnabled)
            {
                ApplyVisuals();
                SyncHighlightState();
            }
            else
            {
                ScheduleRefresh();
            }
        }

        /// <summary>
        /// Clears the current binding and detaches event listeners.
        /// </summary>
        public void Unbind()
        {
            if (source == null)
            {
                return;
            }

            Unsubscribe(source);
            source = null;
            ScheduleRefresh();
            SetHighlighted(false);
        }

        private void SetSourceInternal(CombatantState newSource)
        {
            if (source == newSource)
            {
                return;
            }

            Unsubscribe(source);
            source = newSource;
            Subscribe(source);
            SyncMarks();
            EnsurePortraitReference();
            CaptureOriginalNameColor();
            ScheduleRefresh();
            SyncAnchorTarget();
            SyncHighlightState();
        }

        private void Subscribe(CombatantState target)
        {
            if (target != null && target.OnVitalsChanged != null)
            {
                target.OnVitalsChanged.AddListener(vitalsListener);
            }
        }

        private void Unsubscribe(CombatantState target)
        {
            if (target != null && target.OnVitalsChanged != null)
            {
                target.OnVitalsChanged.RemoveListener(vitalsListener);
            }
        }

        private void SubscribeMarks()
        {
            if (!marksInitialized || markService == null || marksSubscribed)
            {
                return;
            }

            markService.OnMarkChanged += HandleMarkChanged;
            marksSubscribed = true;
        }

        private void UnsubscribeMarks()
        {
            if (!marksSubscribed)
            {
                return;
            }

            if (markService != null)
            {
                markService.OnMarkChanged -= HandleMarkChanged;
            }
            marksSubscribed = false;
        }

        private void LateUpdate()
        {
            if (!pendingRefresh)
            {
                // Still allow highlight pulse to run
                if (isHighlighted)
                {
                    UpdateHighlightPulse();
                }
                return;
            }

            if (!CanApplyVisuals())
            {
                return;
            }

            pendingRefresh = false;
            ApplyVisuals();

            if (isHighlighted)
            {
                UpdateHighlightPulse();
            }
        }

        private void ScheduleRefresh()
        {
            pendingRefresh = true;
        }

        private void UpdateVisuals()
        {
            ScheduleRefresh();
        }

        private bool CanApplyVisuals()
        {
            return isActiveAndEnabled && gameObject.scene.IsValid();
        }

        private void ApplyVisuals()
        {
            if (source == null)
            {
                SetLabels("--", "--", "--", "--");
                SetSliders(0f, 0f, 0f);
                UpdatePips(0, 0);
                UpdatePortrait(null);
                return;
            }

            SetLabels(
                source.DisplayName,
                string.Format(hpFormat, source.CurrentHP, source.MaxHP),
                string.Format(spFormat, source.CurrentSP, source.MaxSP),
                string.Format(cpFormat, source.CurrentCP, source.MaxCP));

            SetSliders(
                SafeRatio(source.CurrentHP, source.MaxHP),
                SafeRatio(source.CurrentSP, source.MaxSP),
                SafeRatio(source.CurrentCP, source.MaxCP));

            UpdatePips(source.CurrentCP, source.MaxCP);
            UpdatePortrait(source.Portrait);
        }

        private void SyncAnchorTarget()
        {
            if (worldAnchor == null)
            {
                return;
            }

            worldAnchor.Target = source != null ? source.transform : null;
        }

        private void SetLabels(string displayName, string hpValue, string spValue, string cpValue)
        {
            if (CanWrite(nameText))
            {
                nameText.text = displayName;
            }

            if (CanWrite(hpText))
            {
                hpText.text = hpValue;
            }

            if (CanWrite(spText))
            {
                spText.text = spValue;
            }

            if (CanWrite(cpText))
            {
                cpText.text = cpValue;
            }
        }

        private void SetSliders(float hpPct, float spPct, float cpPct)
        {
            if (hpSlider != null)
            {
                hpSlider.value = hpPct;
            }

            if (spSlider != null)
            {
                spSlider.value = spPct;
            }

            if (cpSlider != null)
            {
                cpSlider.value = cpPct;
            }
        }

        private void UpdatePips(int currentCp, int maxCp)
        {
            if (cpPips == null || cpPips.Length == 0)
            {
                return;
            }

            for (int i = 0; i < cpPips.Length; i++)
            {
                var pip = cpPips[i];
                if (pip == null)
                {
                    continue;
                }

                bool withinCapacity = i < maxCp;
                bool filled = i < currentCp;

                if (pip.activeSelf != withinCapacity)
                {
                    pip.SetActive(withinCapacity);
                }

                var image = pip.GetComponent<Image>();
                if (image != null)
                {
                    image.enabled = withinCapacity;
                    image.color = filled ? Color.white : new Color(1f, 1f, 1f, 0.15f);
                }
            }
        }

        private static float SafeRatio(int current, int max)
        {
            if (max <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01(current / (float)max);
        }

        private static bool CanWrite(TMP_Text label)
        {
            return label != null && label.gameObject.scene.IsValid();
        }

        private void UpdatePortrait(Sprite sprite)
        {
            if (portraitImage == null)
            {
                return;
            }

            portraitImage.sprite = sprite;
            portraitImage.enabled = sprite != null;
        }

        private void EnsurePortraitReference()
        {
            if (portraitImage != null || !autoFindPortraitImage)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(portraitChildPath))
            {
                var target = transform.Find(portraitChildPath);
                if (target != null)
                {
                    portraitImage = target.GetComponent<Image>();
                    if (portraitImage != null)
                    {
                        return;
                    }
                }
            }

            if (portraitImage == null)
            {
                portraitImage = GetComponentInChildren<Image>(includeInactive: true);
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            if (isHighlighted == highlighted)
            {
                return;
            }

            isHighlighted = highlighted;
            if (highlightRoot != null)
            {
                highlightRoot.SetActive(highlighted);
            }

            if (nameText != null && originalColorCaptured)
            {
                nameText.color = highlighted ? highlightNameColor : originalNameColor;
            }

            if (!highlighted && highlightRoot != null)
            {
                highlightRoot.transform.localScale = originalHighlightScale;
            }
        }

        private void CaptureOriginalNameColor()
        {
            if (nameText == null)
            {
                return;
            }

            originalNameColor = nameText.color;
            originalColorCaptured = true;
        }

        private void SyncHighlightState()
        {
            SetHighlighted(isHighlighted);
        }

        private void CacheHighlightScale()
        {
            if (highlightRoot == null)
            {
                originalHighlightScale = Vector3.one;
                return;
            }

            originalHighlightScale = highlightRoot.transform.localScale;
            if (markIcon != null)
            {
                markIconOriginalScale = markIcon.transform.localScale;
            }
        }

        private void UpdateHighlightPulse()
        {
            if (!pulseHighlight || highlightRoot == null)
            {
                return;
            }

            float t = Mathf.PingPong(Time.unscaledTime * pulseSpeed, 1f);
            float scale = 1f + Mathf.Lerp(-pulseScaleAmount, pulseScaleAmount, t);
            highlightRoot.transform.localScale = originalHighlightScale * scale;
        }

        private void HandleMarkChanged(MarkEvent evt)
        {
            if (evt.Target != source)
            {
                return;
            }

            if (markIcon == null)
            {
                return;
            }

            switch (evt.Reason)
            {
                case MarkChangeReason.Applied:
                case MarkChangeReason.Refreshed:
                    ApplyMarkVisual(evt.Definition);
                    break;
                case MarkChangeReason.Detonated:
                case MarkChangeReason.Cleared:
                case MarkChangeReason.Expired:
                    if (markFxEnabled)
                    {
                        if (markFlashRoutine != null)
                        {
                            StopCoroutine(markFlashRoutine);
                        }
                        markFlashRoutine = StartCoroutine(FlashAndClearMark());
                    }
                    else
                    {
                        ClearMarkIcon();
                    }
                    break;
                default:
                    SyncMarks();
                    break;
            }
        }

        private void SyncMarks()
        {
            if (markIcon == null)
            {
                return;
            }

            if (source == null || !source.ActiveMark.HasValue)
            {
                ClearMarkIcon();
                return;
            }

            ApplyMarkVisual(source.ActiveMark.Definition);
        }

        private void ApplyMarkVisual(MarkDefinition def)
        {
            if (markIcon == null)
            {
                return;
            }

            if (markAnimRoutine != null)
            {
                StopCoroutine(markAnimRoutine);
                markAnimRoutine = null;
            }

            var hasAnim = def != null && def.animatedFrames != null && def.animatedFrames.Length > 0;
            if (hasAnim)
            {
                markIcon.color = def.tint;
                markAnimRoutine = StartCoroutine(AnimateMarkIcon(def.animatedFrames, def.animatedFrameRate, def.animatedLoop));
            }
            else
            {
                markIcon.sprite = def != null ? def.icon : null;
                markIcon.color = def != null ? def.tint : Color.white;
                markIcon.enabled = markIcon.sprite != null;
            }

            // Pequeño punch para feedback al aplicar/refresh.
            if (markFxEnabled && markIcon.enabled)
            {
                if (markScaleRoutine != null)
                {
                    StopCoroutine(markScaleRoutine);
                }
                markIcon.transform.localScale = markIconOriginalScale;
                markIcon.transform.localScale += markApplyPunch;
                markScaleRoutine = StartCoroutine(ResetMarkScale());
            }
        }

        private void ClearMarkIcon()
        {
            if (markIcon == null)
            {
                return;
            }

            if (markAnimRoutine != null)
            {
                StopCoroutine(markAnimRoutine);
                markAnimRoutine = null;
            }
            if (markScaleRoutine != null)
            {
                StopCoroutine(markScaleRoutine);
                markScaleRoutine = null;
            }
            if (markFlashRoutine != null)
            {
                StopCoroutine(markFlashRoutine);
                markFlashRoutine = null;
            }

            markIcon.sprite = null;
            markIcon.enabled = false;
            markIcon.color = Color.white;
            markIcon.transform.localScale = markIconOriginalScale;
        }

        private System.Collections.IEnumerator ResetMarkScale()
        {
            yield return null;
            if (markIcon != null)
            {
                markIcon.transform.localScale = markIconOriginalScale;
            }
            markScaleRoutine = null;
        }

        private System.Collections.IEnumerator AnimateMarkIcon(System.Collections.Generic.IReadOnlyList<Sprite> frames, float frameRate, bool loop)
        {
            if (markIcon == null || frames == null || frames.Count == 0)
            {
                markIcon.enabled = false;
                yield break;
            }

            markIcon.enabled = true;
            var wait = new WaitForSeconds(1f / Mathf.Max(1f, frameRate));

            do
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    var frame = frames[i];
                    if (frame != null)
                    {
                        markIcon.sprite = frame;
                    }
                    yield return wait;
                }
            }
            while (loop);

            markAnimRoutine = null;
        }

        private System.Collections.IEnumerator FlashAndClearMark()
        {
            if (markIcon == null)
            {
                yield break;
            }

            var originalColor = markIcon.color;
            float t = 0f;
            while (t < markFlashDuration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / markFlashDuration);
                markIcon.color = Color.Lerp(originalColor, detonateFlashColor, lerp);
                yield return null;
            }

            ClearMarkIcon();
            markFlashRoutine = null;
        }

        /// <summary>
        /// Inyección explícita del servicio de marks. Debe llamarse desde el orquestador/HUD manager.
        /// </summary>
        public void SetMarkService(MarkService service)
        {
            if (marksSubscribed)
            {
                UnsubscribeMarks();
            }

            markService = service;

            if (isActiveAndEnabled)
            {
                SubscribeMarks();
                SyncMarks();
            }
        }
    }
}
