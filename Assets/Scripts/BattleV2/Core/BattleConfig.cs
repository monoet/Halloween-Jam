using BattleV2.Actions;
using BattleV2.Charge;
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
    }
}
