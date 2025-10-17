using System.Collections.Generic;
using System.Linq;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Actions
{
    [CreateAssetMenu(menuName = "Battle/Action Catalog")]
    public class ActionCatalog : ScriptableObject
    {
        [SerializeField] private List<BattleActionData> basic = new();
        [SerializeField] private List<BattleActionData> magic = new();
        [SerializeField] private List<BattleActionData> items = new();

        public IReadOnlyList<BattleActionData> BuildAvailableFor(CombatantState actor, CombatContext context)
        {
            var all = new List<BattleActionData>();
            all.AddRange(basic);
            all.AddRange(magic);
            all.AddRange(items);

            var filtered = new List<BattleActionData>();

            foreach (var data in all)
            {
                if (data == null)
                {
                    continue;
                }

                try
                {
                    var impl = Resolve(data);
                    if (impl == null)
                    {
                        BattleLogger.Warn("Catalog", $"Action '{data.id}' has no implementation.");
                        continue;
                    }

                    if (impl.CanExecute(actor, context, 0))
                    {
                        filtered.Add(data);
                    }
                }
                catch (System.Exception ex)
                {
                    BattleLogger.Warn("Catalog", $"Error evaluating action '{data.id}': {ex.Message}");
                }
            }

            return filtered;
        }

        public IAction Resolve(BattleActionData data)
        {
            if (data == null || data.actionImpl == null)
            {
                return null;
            }

            if (data.actionImpl is IActionProvider provider)
            {
                return provider.Get();
            }

            BattleLogger.Warn("Catalog", $"Action implementation on '{data.id}' does not implement IActionProvider.");
            return null;
        }

        public BattleActionData Fallback(CombatantState actor, CombatContext context)
        {
            var available = BuildAvailableFor(actor, context);
            if (available.Count > 0)
            {
                return available[0];
            }

            BattleLogger.Error("Catalog", "No fallback action available.");
            throw new System.InvalidOperationException("ActionCatalog has no fallback action configured.");
        }
    }
}
