using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleV2.UI
{
    /// <summary>
    /// Punto de entrada de la UI de combate. Coordina paneles sin lógica de combate.
    /// Emite eventos hacia el orquestador/BattleManager.
    /// </summary>
    public sealed class BattleUIRoot : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private ActionMenuPanel rootMenu;
        [SerializeField] private AtkMenuPanel atkMenu;
        [SerializeField] private MagMenuPanel magMenu;
        [SerializeField] private ItemMenuPanel itemMenu;
        [SerializeField] private CPChargePanel cpPanel;
        [SerializeField] private TargetSelectionPanel targetPanel;
        [SerializeField] private TimedHitPanel timedHitPanel;
        [Header("Debug")]
        [SerializeField] private bool debugStartVisible = false;

        // Se dispara cuando se confirma la categoría (Attack/Magic/Item/Defend/Flee) o subacción elegida.
        public event Action<string> OnRootActionSelected;
        public event Action<string> OnAttackChosen;
        public event Action<string> OnSpellChosen;
        public event Action<string> OnItemChosen;
        public event Action<int> OnChargeCommitted;
        public event Action<int> OnTargetSelected;
        public event Action OnTargetConfirmed;
        public event Action OnTimedHitPressed;
        public event Action OnCancel;

        private enum UiState
        {
            Hidden,
            Root,
            Atk,
            Mag,
            Item,
            Target,
            TimedHit,
            Locked
        }

        private UiState state = UiState.Hidden;
        private GameObject lastSelected;

        private void Update()
        {
            // Enforce focus if we are in a menu state and nothing is selected
            if (state != UiState.Hidden && state != UiState.Locked && state != UiState.TimedHit)
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                {
                    RestoreFocus();
                }
                else if (EventSystem.current != null)
                {
                    lastSelected = EventSystem.current.currentSelectedGameObject;
                }
            }
        }

        private void RestoreFocus()
        {
            if (lastSelected != null && lastSelected.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(lastSelected);
            }
            else
            {
                // Fallback to focusing the current panel's default
                switch (state)
                {
                    case UiState.Root: rootMenu?.FocusFirst(); break;
                    case UiState.Atk: atkMenu?.FocusFirst(); break;
                    case UiState.Mag: magMenu?.FocusFirst(); break;
                    case UiState.Item: itemMenu?.FocusFirst(); break;
                    case UiState.Target: targetPanel?.FocusFirst(); break;
                }
            }
        }

        private System.Collections.Generic.Stack<UiState> menuStack = new System.Collections.Generic.Stack<UiState>();
        public int StackCount => menuStack.Count;

        private void Awake()
        {
            WirePanels();
            if (debugStartVisible)
            {
                EnterRoot();
            }
            else
            {
                HideAll();
            }
        }

        private void WirePanels()
        {
            if (rootMenu != null)
            {
                rootMenu.OnAttack += () => EnterAtk();
                rootMenu.OnMagic += () => EnterMag();
                rootMenu.OnItem += () => EnterItem();
                rootMenu.OnDefend += () => OnRootActionSelected?.Invoke("Defend");
                rootMenu.OnFlee += () => OnRootActionSelected?.Invoke("Flee");
                rootMenu.OnClose += HideAll;
            }

            if (atkMenu != null)
            {
                atkMenu.OnAttackChosen += id => OnAttackChosen?.Invoke(id);
                atkMenu.OnBack += GoBack;
            }

            if (magMenu != null)
            {
                magMenu.OnSpellChosen += id => OnSpellChosen?.Invoke(id);
                magMenu.OnBack += GoBack;
            }

            if (itemMenu != null)
            {
                itemMenu.OnItemChosen += id => OnItemChosen?.Invoke(id);
                itemMenu.OnBack += GoBack;
            }

            if (cpPanel != null)
            {
                cpPanel.OnChargeCommitted += amount => OnChargeCommitted?.Invoke(amount);
            }

            if (targetPanel != null)
            {
                targetPanel.OnTargetSelected += id => OnTargetSelected?.Invoke(id);
                targetPanel.OnCancelRequested += () =>
                {
                    OnCancel?.Invoke();
                    GoBack();
                };
            }

            if (timedHitPanel != null)
            {
                timedHitPanel.OnTimedHitPressed += () => OnTimedHitPressed?.Invoke();
            }
        }

        private void ShowOnly(BattlePanelBase panel)
        {
            void Toggle(BattlePanelBase p)
            {
                if (p == null) return;
                if (p == panel) p.Show();
                else p.Hide();
            }

            Toggle(rootMenu);
            Toggle(atkMenu);
            Toggle(magMenu);
            Toggle(itemMenu);
            Toggle(targetPanel);

            if (cpPanel != null) cpPanel.Hide();
        }

        public void EnterRoot()
        {
            Debug.Log("BattleUIRoot: EnterRoot called");
            PushState(UiState.Root);
        }

        public void EnterAtk()
        {
            PushState(UiState.Atk);
        }

        public void EnterMag()
        {
            PushState(UiState.Mag);
        }

        public void EnterItem()
        {
            PushState(UiState.Item);
        }

        public void EnterTarget()
        {
            PushState(UiState.Target);
        }

        public void EnterLocked()
        {
            state = UiState.Locked;
            HideAll();
            // Locked doesn't necessarily go on the stack, or it clears it? 
            // Usually Locked is temporary. Let's not mess with stack for Locked unless necessary.
        }

        public void HideAll()
        {
            state = UiState.Hidden;
            ShowOnly(null);
            menuStack.Clear(); // Reset stack on full hide? Or just hide visuals?
        }

        private void PushState(UiState newState)
        {
            // Avoid duplicate pushes of the same state if we are already there
            if (menuStack.Count > 0 && menuStack.Peek() == newState)
            {
                RestoreState(newState); // Just refresh
                return;
            }

            // If we are going to Root, maybe we should clear stack? 
            // Usually Root is the bottom.
            if (newState == UiState.Root)
            {
                menuStack.Clear();
            }

            menuStack.Push(newState);
            RestoreState(newState);
        }

        private void RestoreState(UiState uiState)
        {
            state = uiState;
            switch (uiState)
            {
                case UiState.Root:
                    ShowOnly(rootMenu);
                    rootMenu?.FocusFirst();
                    break;
                case UiState.Atk:
                    ShowOnly(atkMenu);
                    cpPanel?.Show();
                    atkMenu?.FocusFirst();
                    break;
                case UiState.Mag:
                    ShowOnly(magMenu);
                    cpPanel?.ShowIfAllowed();
                    magMenu?.FocusFirst();
                    break;
                case UiState.Item:
                    ShowOnly(itemMenu);
                    cpPanel?.Hide();
                    itemMenu?.FocusFirst();
                    break;
                case UiState.Target:
                    ShowOnly(targetPanel);
                    targetPanel?.FocusFirst();
                    break;
                case UiState.Hidden:
                    ShowOnly(null);
                    break;
            }
        }

        public void GoBack()
        {
            if (menuStack.Count <= 1)
            {
                // Already at root or empty, can't go back further
                // Maybe open Pause menu?
                return;
            }

            // Pop current
            menuStack.Pop();

            // Peek previous
            var previous = menuStack.Peek();
            
            // If previous was Target (weird), pop again? No, stack should be clean.
            
            RestoreState(previous);

            // If we just popped Target, we need to signal cancellation logic?
            // The caller (TargetSelectionState) calls GoBack().
            // But if we are in Atk menu and press Back, we go to Root.
        }

        // ... Confirm/Cancel Target ...
        public void ConfirmTarget() => OnTargetConfirmed?.Invoke();
        public void CancelTarget() => OnCancel?.Invoke();
    }
}
