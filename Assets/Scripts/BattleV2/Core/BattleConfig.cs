using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Orchestration.Services.Animation;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Core
{
    [CreateAssetMenu(menuName = "Battle/Battle Config")]
    public class BattleConfig : ScriptableObject
    {
        [Header("Catalog & Providers")]
        public ActionCatalog actionCatalog;
        public Object inputProvider;

        [Header("Services & Scaling")]
        public BattleServices services = new();
        public ComboPointScalingProfile comboPointScaling;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Tooltip("Dev-only: enable CP trace logs (CPTRACE). Avoid enabling in release builds.")]
        public bool enableCpTraceLogs = false;
#endif

        [Header("Timing")]
        public BattleTimingConfig timingConfig;
    }
}
