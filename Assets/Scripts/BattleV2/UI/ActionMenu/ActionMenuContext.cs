using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.UI.ActionMenu
{
    /// <summary>
    /// Shared context passed to action menu strategies.
    /// </summary>
    [Serializable]
    public class ActionMenuContext
    {
        [SerializeField] private RectTransform container;
        [SerializeField] private BattleMenuManager menuManager;
        [SerializeField] private BattleUIOrchestrator uiOrchestrator;

        public RectTransform Container => container;
        public BattleMenuManager MenuManager => menuManager;
        public BattleUIOrchestrator UIOrchestrator => uiOrchestrator;
        public Action<ActionMenuOption> OnOptionSelected { get; set; }
        public Action OnBackRequested { get; set; }
        public Action OnChargeRequested { get; set; }
    }
}
