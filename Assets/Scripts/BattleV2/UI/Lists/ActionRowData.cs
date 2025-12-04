using UnityEngine;

namespace BattleV2.UI.Lists
{
    public interface IActionRowData
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        bool IsEnabled { get; }
        string DisabledReason { get; }
    }

    public interface ISpellRowData : IActionRowData
    {
        int SpCost { get; }
        Sprite ElementIcon { get; }
    }

    public interface IItemRowData : IActionRowData
    {
        int Quantity { get; }
    }

    public sealed class SpellRowData : ISpellRowData
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public bool IsEnabled { get; }
        public string DisabledReason { get; }
        public int SpCost { get; }
        public Sprite ElementIcon { get; }

        public SpellRowData(
            string id,
            string name,
            string description,
            bool isEnabled,
            string disabledReason,
            int spCost,
            Sprite elementIcon)
        {
            Id = id;
            Name = name;
            Description = description;
            IsEnabled = isEnabled;
            DisabledReason = disabledReason;
            SpCost = Mathf.Max(0, spCost);
            ElementIcon = elementIcon;
        }
    }

    public sealed class ItemRowData : IItemRowData
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public bool IsEnabled { get; }
        public string DisabledReason { get; }
        public int Quantity { get; }

        public ItemRowData(
            string id,
            string name,
            string description,
            bool isEnabled,
            string disabledReason,
            int quantity)
        {
            Id = id;
            Name = name;
            Description = description;
            IsEnabled = isEnabled;
            DisabledReason = disabledReason;
            Quantity = Mathf.Max(0, quantity);
        }
    }
}
