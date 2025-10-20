using BattleV2.Actions;

namespace BattleV2.UI.ActionMenu
{
    /// <summary>
    /// Lightweight data entry for each action option rendered by the menu.
    /// </summary>
    public readonly struct ActionMenuOption
    {
        public ActionMenuOption(string displayName, string description, BattleActionData actionData)
        {
            DisplayName = displayName;
            Description = description;
            ActionData = actionData;
        }

        public string DisplayName { get; }
        public string Description { get; }
        public BattleActionData ActionData { get; }
    }
}
