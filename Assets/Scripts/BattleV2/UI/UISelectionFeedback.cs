using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace BattleV2.UI
{
    /// <summary>
    /// Visual feedback for UI selection. Deterministic: never accumulates scale.
    /// Works with keyboard/gamepad navigation via ISelect/IDeselect.
    /// </summary>
    public sealed class UISelectionFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [Header("Target")]
        [Tooltip("What actually scales. If null, scales this RectTransform.")]
        [SerializeField] private RectTransform scaleTarget;

        [Header("Scale")]
        [SerializeField] private float selectedScaleMultiplier = 1.08f;
        [SerializeField] private float duration = 0.10f;
        [SerializeField] private Ease ease = Ease.OutQuad;

        [Header("Debug")]
        [SerializeField] private bool logEvents = false;

        private Vector3 baseScale;
        private bool baseCaptured;
        private bool isSelected;
        private Tween activeTween;

        private RectTransform Target
        {
            get
            {
                if (scaleTarget != null) return scaleTarget;
                return transform as RectTransform;
            }
        }

        private void CaptureBaseIfNeeded()
        {
            if (baseCaptured) return;
            var t = Target;
            if (t == null) return;
            baseScale = t.localScale;
            baseCaptured = true;
        }

        private void KillTween()
        {
            if (activeTween != null && activeTween.IsActive())
            {
                activeTween.Kill(false);
            }
            activeTween = null;
        }

        public void ResetInstant()
        {
            CaptureBaseIfNeeded();
            KillTween();
            var t = Target;
            if (t != null && baseCaptured)
                t.localScale = baseScale;
            isSelected = false;
        }

        private void AnimateTo(Vector3 targetScale)
        {
            var t = Target;
            if (t == null) return;

            KillTween();
            activeTween = t.DOScale(targetScale, duration).SetEase(ease).SetUpdate(true);
        }

        public void OnSelect(BaseEventData eventData)
        {
            BattleV2.UI.Diagnostics.MagMenuDebug.Log("04", $"OnSelect name={name}", this);
            CaptureBaseIfNeeded();
            if (!baseCaptured) return;

            if (logEvents) Debug.Log($"[UISelectionFeedback] OnSelect {name}");

            if (isSelected)
                return;

            isSelected = true;

            var t = Target;
            if (t == null) return;

            t.localScale = baseScale;
            AnimateTo(baseScale * selectedScaleMultiplier);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            BattleV2.UI.Diagnostics.MagMenuDebug.Log("05", $"OnDeselect name={name}", this);
            CaptureBaseIfNeeded();
            if (!baseCaptured) return;

            if (logEvents) Debug.Log($"[UISelectionFeedback] OnDeselect {name}");

            isSelected = false;
            AnimateTo(baseScale);
        }

        private void OnDisable()
        {
            BattleV2.UI.Diagnostics.MagMenuDebug.Log("06", $"OnDisable name={name}", this);
            if (logEvents) Debug.Log($"[UISelectionFeedback] OnDisable {name}");
            ResetInstant();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (selectedScaleMultiplier < 1.01f) selectedScaleMultiplier = 1.01f;
            if (duration < 0.01f) duration = 0.01f;
        }
#endif
    }
}

