using System;
using BattleV2.Core;
using BattleV2.Providers;
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
        [Header("Root Menu Container")]
        [SerializeField] private GameObject rootMenuContainer;
        [SerializeField] private GameObject rootDefaultSelectable;
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
        public event Action OnMenuCancel;
        public event Action OnTargetCancel;

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
        private CombatantState currentActor;
        private CombatContext combatContext;
        private BattleActionContext pendingActionContext;
        private CombatContext ActiveContext => combatContext != null ? combatContext : pendingActionContext?.Context;

        // FMOD/UI mode: do NOT conflate player-hidden with system-hidden.
        public enum BattleUIMode
        {
            CombatUI = 0,
            Cinematic = 1,
            SystemHidden = 2
        }

        public event Action<BattleUIMode> OnUIModeChanged;

        [SerializeField] private BattleUIMode debugInitialMode = BattleUIMode.SystemHidden;
        private BattleUIMode uiMode;
        public BattleUIMode CurrentUIMode => uiMode;

        private enum RootMenuVisibility
        {
            Visible,
            Hidden
        }

        private RootMenuVisibility rootVisibility = RootMenuVisibility.Visible;
        public bool IsRootHidden => rootVisibility == RootMenuVisibility.Hidden;

        private void Update()
        {
            // Enforce focus if we are in a menu state and nothing is selected (unless root is intentionally hidden)
            if (state != UiState.Hidden && state != UiState.Locked && state != UiState.TimedHit && !IsRootHidden)
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
            var es = EventSystem.current;
            var cur = es != null ? es.currentSelectedGameObject : null;
            BattleV2.UI.Diagnostics.MagMenuDebug.Log("07", $"RestoreFocus state={state} cur={(cur ? cur.name : "null")} last={(lastSelected ? lastSelected.name : "null")}", this);

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

        public void SetActionContext(BattleActionContext context)
        {
            pendingActionContext = context;
            currentActor = context != null ? context.Player : null;
            combatContext = context != null ? context.Context : null;
            if (cpPanel != null && context != null)
            {
                cpPanel.ConfigureMax(context.MaxCpCharge);
            }
        }

        private void Awake()
        {
            // Initialize mode early for audio bridges.
            uiMode = debugInitialMode;
            WirePanels();
            if (rootMenuContainer == null && rootMenu != null)
            {
                rootMenuContainer = rootMenu.gameObject;
            }
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
                rootMenu.OnClose += HideRootToHidden; // Presentation-only close -> cinematic
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
                targetPanel.OnCancelRequested += () => RequestCancelTarget(goBack: true);
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
            EnsureRootVisible();
            PushState(UiState.Root);

            if (!IsRootHidden)
            {
                SetUIMode(BattleUIMode.CombatUI);
            }
        }

        public void EnterAtk()
        {
            PushState(UiState.Atk);
        }

        public void EnterMag()
        {
            magMenu?.ShowFor(currentActor, ActiveContext);
            PushState(UiState.Mag);
        }

        public void EnterItem()
        {
            itemMenu?.ShowFor(currentActor, ActiveContext);
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
            rootVisibility = RootMenuVisibility.Hidden;
            ShowOnly(null);
            menuStack.Clear(); // Reset stack on full hide? Or just hide visuals?
            SetRootContainerActive(false);
            ClearSelection();

            SetUIMode(BattleUIMode.SystemHidden);
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
                    EnsureRootVisible();
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
        public void CancelTarget() => OnTargetCancel?.Invoke();
        public void CancelMenu() => OnMenuCancel?.Invoke();

        /// <summary>
        /// Cancela la selección de objetivo. Opcionalmente hace GoBack antes de emitir el evento.
        /// </summary>
        public void RequestCancelTarget(bool goBack)
        {
            if (goBack)
            {
                GoBack();
            }

            CancelTarget();
        }

        private void SetRootContainerActive(bool active)
        {
            if (rootMenuContainer != null)
            {
                rootMenuContainer.SetActive(active);
            }
            else if (rootMenu != null)
            {
                rootMenu.gameObject.SetActive(active);
            }
        }

        private void ClearSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            lastSelected = null;
        }

        public void HideRootToHidden()
        {
            if (rootVisibility == RootMenuVisibility.Hidden)
            {
                return;
            }

            rootVisibility = RootMenuVisibility.Hidden;
            SetRootContainerActive(false);
            ClearSelection();

            SetUIMode(BattleUIMode.Cinematic);
        }

        public void ShowRootFromHidden()
        {
            if (rootVisibility == RootMenuVisibility.Visible)
            {
                return;
            }

            SetRootContainerActive(true);
            rootVisibility = RootMenuVisibility.Visible;
            RestoreRootSelection();

            SetUIMode(BattleUIMode.CombatUI);
        }

        public void ToggleRootVisibility()
        {
            if (IsRootHidden)
            {
                ShowRootFromHidden();
            }
            else
            {
                HideRootToHidden();
            }
        }

        public void EnsureRootVisible()
        {
            if (IsRootHidden)
            {
                ShowRootFromHidden();
            }
        }

        private void RestoreRootSelection()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            if (rootDefaultSelectable != null)
            {
                EventSystem.current.SetSelectedGameObject(rootDefaultSelectable);
            }
            else
            {
                rootMenu?.FocusFirst();
            }
        }

        private void SetUIMode(BattleUIMode mode)
        {
            if (uiMode == mode)
            {
                return;
            }

            uiMode = mode;
            OnUIModeChanged?.Invoke(uiMode);
        }
    }
}

