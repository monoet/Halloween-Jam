using UnityEngine;

namespace BattleV2.Orchestration.Services.Animation
{
    [CreateAssetMenu(menuName = "Battle/Timing Config")]
    public class BattleTimingConfig : ScriptableObject
    {
        [Min(0.01f)] public float baseActionTime = 0.8f;
        [Min(0f)] public float perTargetDelay = 0.15f;
        [Range(0.1f, 10f)] public float speedScaleMin = 0.75f;
        [Range(0.1f, 10f)] public float speedScaleMax = 1.25f;

        public BattleTimingProfile ToProfile()
        {
            return new BattleTimingProfile(
                baseActionTime,
                perTargetDelay,
                speedScaleMin,
                speedScaleMax);
        }
    }

    public readonly struct BattleTimingProfile
    {
        public BattleTimingProfile(float baseActionTime, float perTargetDelay, float speedScaleMin, float speedScaleMax)
        {
            BaseActionTime = Mathf.Max(0.01f, baseActionTime);
            PerTargetDelay = Mathf.Max(0f, perTargetDelay);
            SpeedScaleMin = Mathf.Max(0.01f, speedScaleMin);
            SpeedScaleMax = Mathf.Max(SpeedScaleMin, speedScaleMax);
        }

        public float BaseActionTime { get; }
        public float PerTargetDelay { get; }
        public float SpeedScaleMin { get; }
        public float SpeedScaleMax { get; }

        public static BattleTimingProfile Default => new BattleTimingProfile(0.8f, 0.15f, 0.75f, 1.25f);
    }
}
