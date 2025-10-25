namespace BattleV2.Charge
{
    /// <summary>
    /// Provides combo point charge multipliers for damage scaling.
    /// </summary>
    public static class ComboPointScaling
    {
        /// <summary>
        /// Active scaling profile injected at runtime via BattleConfig.
        /// </summary>
        public static ComboPointScalingProfile ActiveProfile { get; private set; }

        /// <summary>
        /// Allows the battle setup to configure a custom scaling profile.
        /// </summary>
        public static void Configure(ComboPointScalingProfile profile)
        {
            ActiveProfile = profile;
        }

        /// <summary>
        /// Returns the damage multiplier for the provided CP charge using the active profile (if any).
        /// </summary>
        public static float GetDamageMultiplier(int cpCharge)
        {
            if (cpCharge <= 0)
            {
                return 1f;
            }

            var profile = ActiveProfile;
            if (profile != null)
            {
                float value = profile.GetMultiplier(cpCharge);
                if (value > 0f)
                {
                    return value;
                }
            }

            return DefaultProceduralMultiplier(cpCharge);
        }

        /// <summary>
        /// Original procedural fallback: 1 CP → +15%, each extra doubles the previous bonus and adds +10%.
        /// Exposed for profiles that need to replicate or extend the default behaviour.
        /// </summary>
        internal static float DefaultProceduralMultiplier(int cpCharge)
        {
            if (cpCharge <= 0)
            {
                return 1f;
            }

            float bonusPercent = 15f;

            for (int i = 2; i <= cpCharge; i++)
            {
                bonusPercent = bonusPercent * 2f + 10f;
            }

            return 1f + (bonusPercent / 100f);
        }
    }
}
