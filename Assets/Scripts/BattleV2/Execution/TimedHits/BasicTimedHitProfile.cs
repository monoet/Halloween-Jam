using UnityEngine;

namespace BattleV2.Execution.TimedHits
{
    [CreateAssetMenu(menuName = "Battle/Timed Hits/Basic Profile")]
    public sealed class BasicTimedHitProfile : ScriptableObject
    {
        [SerializeField, Tooltip("Milliseconds from window start considered a Perfect input.")]
        private float perfectThresholdMs = 40f;

        [SerializeField, Tooltip("Milliseconds from window start considered a Good input.")]
        private float goodThresholdMs = 120f;

        [SerializeField, Tooltip("Milliseconds before the window closes automatically and counts as a Miss.")]
        private float windowTimeoutMs = 150f;

        [Header("Multipliers")]
        [SerializeField] private float perfectMultiplier = 1.5f;
        [SerializeField] private float goodMultiplier = 1f;
        [SerializeField] private float missMultiplier = 0f;

        [Header("Metadata")]
        [SerializeField] private string eventTag = "basic_attack";
        [SerializeField, Min(0)] private int comboPointReward;

        public float PerfectThresholdMs => perfectThresholdMs;
        public float GoodThresholdMs => goodThresholdMs;
        public float WindowTimeoutMs => windowTimeoutMs;
        public float PerfectMultiplier => perfectMultiplier;
        public float GoodMultiplier => goodMultiplier;
        public float MissMultiplier => missMultiplier;
        public string EventTag => string.IsNullOrWhiteSpace(eventTag) ? "basic_attack" : eventTag;
        public int ComboPointReward => comboPointReward;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (perfectThresholdMs < 0f)
            {
                perfectThresholdMs = 0f;
            }

            if (goodThresholdMs < perfectThresholdMs)
            {
                goodThresholdMs = perfectThresholdMs;
            }

            if (windowTimeoutMs < goodThresholdMs)
            {
                windowTimeoutMs = goodThresholdMs;
            }

            if (perfectMultiplier <= 0f)
            {
                perfectMultiplier = 1.5f;
            }

            if (goodMultiplier <= 0f)
            {
                goodMultiplier = 1f;
            }

            if (missMultiplier < 0f)
            {
                missMultiplier = 0f;
            }
        }
#endif
    }
}
