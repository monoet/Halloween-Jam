using UnityEngine;

namespace BattleV2.Charge
{
    [CreateAssetMenu(menuName = "Battle/Charge/KS1 Timed Hit Profile")]
    public class Ks1TimedHitProfile : ScriptableObject
    {
        [System.Serializable]
        public struct Tier
        {
            public int RequiredCp;
            public int Hits;
            public int RefundMax;
            public float DamageMultiplier;
            [Header("Timing")]
            [Min(0.05f)] public float TimelineDuration;
            [Range(0f, 1f)] public float PerfectWindowCenter;
            [Range(0f, 0.5f)] public float PerfectWindowRadius;
            [Range(0f, 0.5f)] public float SuccessWindowRadius;
            [Header("Per-Hit Multipliers")]
            public float PerfectHitMultiplier;
            public float SuccessHitMultiplier;
            public float MissHitMultiplier;
            [Header("Phase Display")]
            [Min(0f)] public float ResultHoldDuration;
        }

        [SerializeField] private Tier[] tiers = new Tier[0];

        public Tier[] Tiers => tiers;

        public Tier GetTierForCharge(int cpCharge)
        {
            Tier best = default;
            foreach (var tier in tiers)
            {
                if (cpCharge >= tier.RequiredCp)
                {
                    best = tier;
                }
            }
            return best;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (tiers == null)
            {
                return;
            }

            for (int i = 0; i < tiers.Length; i++)
            {
                var tier = tiers[i];
                if (tier.TimelineDuration <= 0f)
                {
                    tier.TimelineDuration = 1f;
                }

                if (tier.PerfectWindowCenter < 0f || tier.PerfectWindowCenter > 1f)
                {
                    tier.PerfectWindowCenter = Mathf.Clamp(tier.PerfectWindowCenter, 0f, 1f);
                }

                if (tier.PerfectWindowCenter == 0f && tier.PerfectWindowRadius == 0f)
                {
                    tier.PerfectWindowCenter = 0.5f;
                }

                if (tier.PerfectWindowRadius < 0f)
                {
                    tier.PerfectWindowRadius = 0f;
                }

                if (tier.SuccessWindowRadius < tier.PerfectWindowRadius)
                {
                    tier.SuccessWindowRadius = tier.PerfectWindowRadius;
                }

                if (tier.PerfectHitMultiplier <= 0f)
                {
                    tier.PerfectHitMultiplier = 1.5f;
                }

                if (tier.SuccessHitMultiplier <= 0f)
                {
                    tier.SuccessHitMultiplier = 1f;
                }

                if (tier.MissHitMultiplier <= 0f)
                {
                    tier.MissHitMultiplier = 0.5f;
                }

                if (tier.ResultHoldDuration < 0f)
                {
                    tier.ResultHoldDuration = 0.35f;
                }

                tiers[i] = tier;
            }
        }
#endif
    }
}
