using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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

        [Header("Formatting")]
        [SerializeField] private string hpFormat = "{0}/{1}";
        [SerializeField] private string spFormat = "{0}/{1}";
        [SerializeField] private string cpFormat = "{0}/{1}";

        private UnityAction vitalsListener;
        private bool pendingRefresh;
        private WorldSpaceHudAnchor worldAnchor;

        private void Awake()
        {
            vitalsListener = ScheduleRefresh;
            EnsurePortraitReference();
            worldAnchor = GetComponent<WorldSpaceHudAnchor>();
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
            ScheduleRefresh();
            SyncAnchorTarget();
        }

        private void OnDisable()
        {
            Unsubscribe(source);
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
            EnsurePortraitReference();
            ScheduleRefresh();
            SyncAnchorTarget();
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

        private void LateUpdate()
        {
            if (!pendingRefresh)
            {
                return;
            }

            if (!CanApplyVisuals())
            {
                return;
            }

            pendingRefresh = false;
            ApplyVisuals();
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
    }
}
