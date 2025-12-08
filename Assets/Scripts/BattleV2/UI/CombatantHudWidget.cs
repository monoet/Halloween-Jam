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
        private MarkService markService;
        private bool marksSubscribed;
        private Vector3 originalHighlightScale = Vector3.one;
        private Coroutine markFxRoutine;
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
            Subscribe(source);
            SubscribeMarks();
            ScheduleRefresh();
            SyncAnchorTarget();
            SyncHighlightState();
            SyncMarks();
            CacheHighlightScale();
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
            if (marksSubscribed)
            {
                return;
            }

            markService ??= FindObjectOfType<BattleManagerV2>()?.MarkService;
            if (markService != null)
            {
                markService.OnMarkChanged += HandleMarkChanged;
                marksSubscribed = true;
            }
        }

        private void UnsubscribeMarks()
        {
            if (!marksSubscribed || markService == null)
            {
                return;
            }

            markService.OnMarkChanged -= HandleMarkChanged;
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
                        if (markFxRoutine != null)
                        {
                            StopCoroutine(markFxRoutine);
                        }
                        markFxRoutine = StartCoroutine(FlashAndClearMark());
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

            if (markService == null)
            {
                markIcon.enabled = false;
                return;
            }

            var marks = markService.GetMarks(source);
            if (marks == null || marks.Count == 0)
            {
                ClearMarkIcon();
                return;
            }

            var def = marks[0];
            ApplyMarkVisual(def);
        }

        private void ApplyMarkVisual(MarkDefinition def)
        {
            if (markIcon == null)
            {
                return;
            }

            markIcon.sprite = def != null ? def.icon : null;
            markIcon.color = def != null ? def.tint : Color.white;
            markIcon.enabled = markIcon.sprite != null;

            // Pequeño punch para feedback al aplicar/refresh.
            if (markFxEnabled && markIcon.enabled)
            {
                if (markFxRoutine != null)
                {
                    StopCoroutine(markFxRoutine);
                }
                markIcon.transform.localScale = markIconOriginalScale;
                markIcon.transform.localScale += markApplyPunch;
                markFxRoutine = StartCoroutine(ResetMarkScale());
            }
        }

        private void ClearMarkIcon()
        {
            if (markIcon == null)
            {
                return;
            }

            markIcon.sprite = null;
            markIcon.enabled = false;
            markIcon.color = Color.white;
        }

        private System.Collections.IEnumerator ResetMarkScale()
        {
            yield return null;
            if (markIcon != null)
            {
                markIcon.transform.localScale = markIconOriginalScale;
            }
            markFxRoutine = null;
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
            markFxRoutine = null;
        }
    }
}
