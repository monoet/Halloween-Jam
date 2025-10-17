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
    }
}
