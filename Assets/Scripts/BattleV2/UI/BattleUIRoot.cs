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
                atkMenu.OnBack += EnterRoot;
            }

            if (magMenu != null)
            {
                magMenu.OnSpellChosen += id => OnSpellChosen?.Invoke(id);
                magMenu.OnBack += EnterRoot;
            }

            if (itemMenu != null)
            {
                itemMenu.OnItemChosen += id => OnItemChosen?.Invoke(id);
                itemMenu.OnBack += EnterRoot;
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
                    EnterRoot();
                };
            }

            if (timedHitPanel != null)
            {
                timedHitPanel.OnTimedHitPressed += () => OnTimedHitPressed?.Invoke();
            }
        }

        public void EnterRoot()
        {
            Debug.Log("BattleUIRoot: EnterRoot called");
            state = UiState.Root;
            ShowOnly(rootMenu);
            if (rootMenu != null)
            {
                rootMenu.FocusFirst();
                Debug.Log("BattleUIRoot: Requested FocusFirst on RootMenu");
            }
            else
            {
                Debug.LogError("BattleUIRoot: RootMenu reference is missing!");
            }
        }

        public void EnterAtk()
        {
            state = UiState.Atk;
            ShowOnly(atkMenu);
            cpPanel?.Show();
            atkMenu?.FocusFirst();
        }

        public void EnterMag()
        {
            state = UiState.Mag;
            ShowOnly(magMenu);
            cpPanel?.ShowIfAllowed();
            magMenu?.FocusFirst();
        }

        public void EnterItem()
        {
            state = UiState.Item;
            ShowOnly(itemMenu);
            cpPanel?.Hide();
            itemMenu?.FocusFirst();
        }

        public void EnterTarget()
        {
            state = UiState.Target;
            ShowOnly(targetPanel);
            targetPanel?.FocusFirst();
        }

        public void EnterLocked()
        {
            state = UiState.Locked;
            HideAll();
        }

        public void HideAll()
        {
            state = UiState.Hidden;
            ShowOnly(null);
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

        public void ShowTimedHitPanel()
        {
            timedHitPanel?.Show();
        }

        public void HideTimedHitPanel()
        {
            timedHitPanel?.Hide();
        }


        public void GoBack()
        {
            switch (state)
            {
                case UiState.Atk:
                case UiState.Mag:
                case UiState.Item:
                    EnterRoot();
                    break;
                case UiState.Target:
                    // Cancel targeting
                    if (targetPanel != null)
                    {
                        // This triggers the OnCancel event in TargetPanel which calls our lambda in WirePanels
                        // which calls OnCancel?.Invoke() and EnterRoot()
                        // So we just need to simulate the cancel action on the panel or call the logic directly.
                        // Ideally, we trigger the panel's cancel to keep flow consistent.
                        // But since TargetPanel.OnCancel is an event, we can't invoke it from outside.
                        // We rely on the input driver to click the cancel button OR we expose a Cancel method on TargetPanel.
                        // For now, let's assume we can just call EnterRoot, but we also need to notify the listener (BattleUITargetInteractor) that we cancelled.
                        // The lambda in WirePanels handles this: targetPanel.OnCancel += ...
                        // So we need a way to trigger that.
                        // Let's just call the OnCancel logic directly if possible, or force EnterRoot and assume Interactor handles cleanup?
                        // Actually, BattleUITargetInteractor waits for OnCancel. If we just switch UI, it hangs.
                        // We need to fire the event.
                        // Let's add a public Cancel() method to TargetSelectionPanel or just invoke the callback if we stored it.
                        // Simpler: Just call the lambda logic we defined in WirePanels:
                         OnCancel?.Invoke();
                         EnterRoot();
                    }
                    else
                    {
                        EnterRoot();
                    }
                    break;
                case UiState.Root:
                    // Maybe show pause menu? Or nothing.
                    break;
            }
        }
    }
}
