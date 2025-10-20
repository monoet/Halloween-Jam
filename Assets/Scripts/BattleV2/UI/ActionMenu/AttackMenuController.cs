using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.UI.ActionMenu
{
    public class AttackMenuController : MonoBehaviour
    {
        private const string DebugTag = "[AttackMenu]";

        [SerializeField] private ActionMenuContext context;
        public ActionMenuContext Context => context;
        [SerializeField] private MonoBehaviour defaultStrategyBehaviour;
        [SerializeField] private MonoBehaviour alternateStrategyBehaviour;
        [SerializeField] private bool useAlternateStrategy;

        private IAttackSubmenuStrategy defaultStrategy;
        private IAttackSubmenuStrategy alternateStrategy;
        private IAttackSubmenuStrategy activeStrategy;
        private readonly List<ActionMenuOption> optionBuffer = new();

        private void Awake()
        {
            defaultStrategy = defaultStrategyBehaviour as IAttackSubmenuStrategy;
            alternateStrategy = alternateStrategyBehaviour as IAttackSubmenuStrategy;

            if (defaultStrategyBehaviour != null && defaultStrategy == null)
            {
                Debug.LogError("Default strategy does not implement IAttackSubmenuStrategy.");
            }

            if (alternateStrategyBehaviour != null && alternateStrategy == null)
            {
                Debug.LogError("Alternate strategy does not implement IAttackSubmenuStrategy.");
            }

            InitialiseStrategies();
        }

        private void OnEnable()
        {
            RefreshActiveStrategy();
        }

        public void ToggleStrategy(bool useAlternate)
        {
            useAlternateStrategy = useAlternate;
            Debug.Log($"{DebugTag} ToggleStrategy -> {(useAlternate ? "Alternate" : "Default")}");
            RefreshActiveStrategy();
        }

        public void ShowOptions(IReadOnlyList<ActionMenuOption> options)
        {
            optionBuffer.Clear();
            optionBuffer.AddRange(options);

            if (activeStrategy != null)
            {
                activeStrategy.Show(optionBuffer);
                Debug.Log($"{DebugTag} ShowOptions count={optionBuffer.Count}");
            }
        }

        public void Hide()
        {
            activeStrategy?.Hide();
            Debug.Log($"{DebugTag} Hide");
        }

        public bool HandleInput(ActionMenuInput input)
        {
            if (activeStrategy == null)
            {
                return false;
            }

            bool consumed = activeStrategy.HandleInput(input);
            if (consumed)
            {
                Debug.Log($"{DebugTag} Consumed input: V={input.Vertical} H={input.Horizontal} Confirm={input.ConfirmPressed} Cancel={input.CancelPressed} Charge={input.ChargeHeld}");
            }

            return consumed;
        }

        private void InitialiseStrategies()
        {
            if (context == null)
            {
                Debug.LogWarning("AttackMenuController missing context.");
                return;
            }

            defaultStrategy?.Initialise(context);
            alternateStrategy?.Initialise(context);
        }

        private void RefreshActiveStrategy()
        {
            activeStrategy?.Hide();
            activeStrategy = useAlternateStrategy ? alternateStrategy : defaultStrategy;

            if (activeStrategy != null)
            {
                activeStrategy.Show(optionBuffer);
                Debug.Log($"{DebugTag} RefreshActiveStrategy -> {activeStrategy.GetType().Name} (options={optionBuffer.Count})");
            }
        }
    }
}
