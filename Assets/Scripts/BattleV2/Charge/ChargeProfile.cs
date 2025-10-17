using UnityEngine;

namespace BattleV2.Charge
{
    /// <summary>
    /// Parameter set that controls how CP charge behaves for a given action.
    /// Future iterations can expand this with timing windows, multipliers, etc.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Charge/Profile")]
    public class ChargeProfile : ScriptableObject
    {
        [Header("Basic Parameters")]
        [SerializeField, Min(0f)] private float maxChargeTime = 1.0f;
        [SerializeField, Range(0f, 5f)] private float maxCpSpendFactor = 1f;

        public float MaxChargeTime => maxChargeTime;
        public float MaxCpSpendFactor => maxCpSpendFactor;
    }
}
