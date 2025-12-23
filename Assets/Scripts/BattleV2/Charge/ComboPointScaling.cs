namespace BattleV2.Charge
{
    /// <summary>
    /// Provides combo point charge multipliers for damage scaling.
    /// </summary>
    public static class ComboPointScaling
    {
        private static readonly System.Threading.AsyncLocal<int> traceExecutionId = new System.Threading.AsyncLocal<int>();
        private static readonly System.Threading.AsyncLocal<string> traceActionId = new System.Threading.AsyncLocal<string>();
        private static readonly System.Threading.AsyncLocal<bool> traceEnabled = new System.Threading.AsyncLocal<bool>();

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
            bool usedProfile = false;
            bool usedFallback = false;
            if (profile != null)
            {
                float value = profile.GetMultiplier(cpCharge);
                if (value > 0f)
                {
                    usedProfile = true;
                    return value;
                }
            }

            usedFallback = true;
            float fallback = DefaultProceduralMultiplier(cpCharge);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleV2.Core.BattleDiagnostics.DevCpTrace && cpCharge > 0 && traceEnabled.Value)
            {
                float baseline = profile != null ? UnityEngine.Mathf.Max(0.01f, profile.GetMultiplier(1)) : DefaultProceduralMultiplier(1);
                if (cpCharge >= 2 && fallback < baseline)
                {
                    UnityEngine.Debug.LogError($"[CPTRACE] FATAL CP SCALING REGRESSION exec={traceExecutionId.Value} action={traceActionId.Value ?? "(null)"} cp={cpCharge} baseline1={baseline:F3} final={fallback:F3} profile={(profile != null ? profile.name : "null")}");
                }

                UnityEngine.Debug.Log(
                    $"[CPTRACE] CPS exec={traceExecutionId.Value} action={traceActionId.Value ?? "(null)"} cp={cpCharge} profile={(profile != null ? profile.name : "null")} usedProfile={usedProfile} usedFallback={usedFallback} final={fallback:F3}");
            }
            if (BattleV2.Core.BattleDiagnostics.DevFlowTrace && cpCharge > 0 && traceEnabled.Value)
            {
                BattleV2.Core.BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"CPS exec={traceExecutionId.Value} action={traceActionId.Value ?? "(null)"} cp={cpCharge} usedProfile={usedProfile} usedFallback={usedFallback} final={fallback:F3}",
                    context: null);
            }
#endif

            return fallback;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private struct TraceScope : System.IDisposable
        {
            public void Dispose()
            {
                traceEnabled.Value = false;
                traceExecutionId.Value = 0;
                traceActionId.Value = null;
            }
        }

        /// <summary>
        /// Dev-only: associates trace metadata with the current thread for CP scaling logs.
        /// </summary>
        public static System.IDisposable BeginTrace(int executionId, string actionId)
        {
            traceExecutionId.Value = executionId;
            traceActionId.Value = actionId;
            traceEnabled.Value = true;
            return new TraceScope();
        }

        public static int CurrentTraceExecutionId => traceEnabled.Value ? traceExecutionId.Value : 0;
        public static string CurrentTraceActionId => traceEnabled.Value ? traceActionId.Value : null;
#endif
    }
}
