using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.Charge
{
    /// <summary>
    /// Scriptable profile that defines how combo point charge modifies damage.
    /// Supports explicit multipliers, procedural growth, and optional soft caps.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Charge/Combo Point Profile")]
    public class ComboPointScalingProfile : ScriptableObject
    {
        [Header("Explicit Multipliers")]
        [Tooltip("Damage multipliers (1.0 = no bonus) for CP 1, 2, 3, etc.")]
        [SerializeField] private List<float> explicitMultipliers = new() { 1.15f, 1.40f, 1.90f, 2.90f };

        [Header("Extrapolation")]
        [SerializeField, Tooltip("If true, CP beyond the explicit list uses the procedural growth parameters.")]
        private bool extendWithGrowth = true;
        [SerializeField, Tooltip("Initial bonus (in percent) used when extrapolating the first CP tier or when the list is empty.")]
        private float growthInitialBonusPercent = 15f;
        [SerializeField, Tooltip("Multiplier applied to the previous bonus when extrapolating CP tiers.")]
        private float growthFactor = 2f;
        [SerializeField, Tooltip("Flat percent added after the growth factor when extrapolating CP tiers.")]
        private float growthOffset = 10f;

        [Header("Curve Override")]
        [SerializeField, Tooltip("Optional multiplier curve evaluated at CP charge. X = cpCharge, Y = multiplier.")]
        private AnimationCurve multiplierCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 1.15f),
            new Keyframe(2f, 1.40f),
            new Keyframe(3f, 1.90f),
            new Keyframe(4f, 2.90f));
        [SerializeField, Tooltip("If true, values beyond the last keyframe use that last keyframe's multiplier. If false, the curve is evaluated freely.")]
        private bool clampCurveToLastKey = true;

        [Header("Soft Cap")]
        [SerializeField, Tooltip("Clamp high multipliers to prevent runaway scaling.")]
        private bool useSoftCap;
        [SerializeField, Min(1f), Tooltip("Absolute multiplier cap applied when soft caps are enabled.")]
        private float softCapMultiplier = 4f;
        [SerializeField, Tooltip("Curve that controls how quickly values blend towards the soft cap. X = normalized excess (0..1). Y = blend amount (0..1).")]
        private AnimationCurve softCapBlend = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private float EvaluateCurve(int cpCharge)
        {
            if (multiplierCurve == null || multiplierCurve.length == 0)
            {
                return -1f;
            }

            float time = cpCharge;
            if (clampCurveToLastKey && multiplierCurve.length > 0)
            {
                var lastKey = multiplierCurve.keys[multiplierCurve.length - 1];
                time = Mathf.Min(time, lastKey.time);
            }

            float value = multiplierCurve.Evaluate(time);
            return value > 0f ? value : -1f;
        }

        private float EvaluateExplicit(int cpCharge)
        {
            if (explicitMultipliers == null || explicitMultipliers.Count == 0)
            {
                return -1f;
            }

            int index = cpCharge - 1;
            if (index < explicitMultipliers.Count)
            {
                float value = explicitMultipliers[index];
                return value > 0f ? value : -1f;
            }

            if (!extendWithGrowth)
            {
                // Clamp to last multiplier if growth is disabled.
                float last = explicitMultipliers[explicitMultipliers.Count - 1];
                return Mathf.Max(0f, last);
            }

            // Continue the sequence using growth parameters.
            float bonus = explicitMultipliers.Count > 0
                ? (explicitMultipliers[explicitMultipliers.Count - 1] - 1f) * 100f
                : growthInitialBonusPercent;

            if (bonus <= 0f)
            {
                bonus = growthInitialBonusPercent;
            }

            for (int tier = explicitMultipliers.Count + 1; tier <= cpCharge; tier++)
            {
                bonus = bonus * growthFactor + growthOffset;
            }

            return 1f + Mathf.Max(0f, bonus) / 100f;
        }

        private float ApplySoftCap(float multiplier)
        {
            if (!useSoftCap || softCapMultiplier <= 0f)
            {
                return multiplier;
            }

            if (multiplier <= softCapMultiplier)
            {
                return multiplier;
            }

            float excessRatio = Mathf.Clamp01((multiplier - softCapMultiplier) / Mathf.Max(softCapMultiplier, 0.0001f));
            float blend = softCapBlend != null && softCapBlend.length > 0
                ? Mathf.Clamp01(softCapBlend.Evaluate(excessRatio))
                : 1f;

            return Mathf.Lerp(multiplier, softCapMultiplier, blend);
        }

        /// <summary>
        /// Returns the configured multiplier for the provided CP charge value.
        /// </summary>
        public float GetMultiplier(int cpCharge)
        {
            if (cpCharge <= 0)
            {
                return 1f;
            }

            float multiplier = EvaluateCurve(cpCharge);
            if (multiplier <= 0f)
            {
                multiplier = EvaluateExplicit(cpCharge);
            }

            if (multiplier <= 0f)
            {
                // As a final fallback, replicate the original procedural formula.
                multiplier = ComboPointScaling.DefaultProceduralMultiplier(cpCharge);
            }

            multiplier = ApplySoftCap(multiplier);
            return Mathf.Max(0.01f, multiplier);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (explicitMultipliers != null)
            {
                for (int i = 0; i < explicitMultipliers.Count; i++)
                {
                    explicitMultipliers[i] = Mathf.Max(0f, explicitMultipliers[i]);
                }
            }
        }
#endif
    }
}

