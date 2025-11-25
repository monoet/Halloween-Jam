using System;
using UnityEngine;

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
            Locked
        }

        private UiState state = UiState.Hidden;

        private void Awake()
        {
            WirePanels();
            HideAll();
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
                targetPanel.OnCancel += () =>
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
            state = UiState.Root;
            ShowOnly(rootMenu);
        }

        public void EnterAtk()
        {
            state = UiState.Atk;
            ShowOnly(atkMenu);
            cpPanel?.Show();
        }

        public void EnterMag()
        {
            state = UiState.Mag;
            ShowOnly(magMenu);
            cpPanel?.ShowIfAllowed();
        }

        public void EnterItem()
        {
            state = UiState.Item;
            ShowOnly(itemMenu);
            cpPanel?.Hide();
        }

        public void EnterTarget()
        {
            state = UiState.Target;
            ShowOnly(targetPanel);
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

        private void ShowOnly(MonoBehaviour panel)
        {
            if (rootMenu != null) rootMenu.gameObject.SetActive(panel == rootMenu);
            if (atkMenu != null) atkMenu.gameObject.SetActive(panel == atkMenu);
            if (magMenu != null) magMenu.gameObject.SetActive(panel == magMenu);
            if (itemMenu != null) itemMenu.gameObject.SetActive(panel == itemMenu);
            if (targetPanel != null) targetPanel.gameObject.SetActive(panel == targetPanel);
            if (cpPanel != null && panel != cpPanel) cpPanel.Hide();
        }

        public void ShowTimedHitPanel()
        {
            timedHitPanel?.Show();
        }

        public void HideTimedHitPanel()
        {
            timedHitPanel?.Hide();
        }
    }
}
