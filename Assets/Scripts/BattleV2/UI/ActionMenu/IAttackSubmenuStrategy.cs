using System.Collections.Generic;

namespace BattleV2.UI.ActionMenu
{
    /// <summary>
    /// Strategy contract for rendering and handling user interaction with the Attack submenu.
    /// </summary>
    public interface IAttackSubmenuStrategy
    {
        void Initialise(ActionMenuContext context);
        void Show(IReadOnlyList<ActionMenuOption> options);
        void Hide();
        bool HandleInput(ActionMenuInput input);
    }
}
