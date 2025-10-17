using BattleV2.Actions;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Core
{
    [CreateAssetMenu(menuName = "Battle/Battle Config")]
    public class BattleConfig : ScriptableObject
    {
        public ActionCatalog actionCatalog;
        public Object inputProvider;
        public BattleServices services = new();
    }
}
