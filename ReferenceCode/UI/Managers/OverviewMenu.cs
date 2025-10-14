using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class OverviewMenu : MonoBehaviour, IMenuState
{
    [Header("UI References")]
    [SerializeField] private ActionButtonSpawner actionButtonSpawner;
    [SerializeField] private GameObject rootMenuContainer;
    [SerializeField] private PartyPanelSelector partyPanelSelector;

    public enum MenuMode
    {
        None,
        Overview,
        Equipment,
        Skills,
        Stats
    }

    [Header("Panels")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private GameObject partyPanel;

    [Header("Sub Menus")]
    [SerializeField] private GameObject itemsPanel;
    [SerializeField] private GameObject questsPanel;
    [SerializeField] private GameObject systemPanel;

    [Header("Botones raiz (Root Panel)")]
    [SerializeField] private Button btnEquipment;
    [SerializeField] private Button btnFormation;
    [SerializeField] private Button btnItems;
    [SerializeField] private Button btnSpells;
    [SerializeField] private Button btnStatus;
    [SerializeField] private Button btnQuests;
    [SerializeField] private Button btnSystem;

    public Button DefaultButton => btnEquipment;

    private Button currentButton;
    public MenuMode currentMode = MenuMode.None;

    private void Start()
    {
        if (btnEquipment == null)
        {
            Debug.LogWarning("[OverviewMenu] Botones raiz no asignados en " + name);
            return;
        }

        btnEquipment.onClick.AddListener(() =>
        {
            currentMode = MenuMode.Equipment;
            OpenPartySelection();
        });

        btnSpells.onClick.AddListener(() =>
        {
            currentMode = MenuMode.Skills;
            OpenPartySelection();
        });

        btnStatus.onClick.AddListener(() =>
        {
            currentMode = MenuMode.Stats;
            OpenPartySelection();
        });

        btnItems.onClick.AddListener(() => OpenSubMenu(itemsPanel, null));
        btnFormation.onClick.AddListener(() => OpenSubMenu(null, partyPanel));
        btnQuests.onClick.AddListener(() => OpenSubMenu(questsPanel, null));
        btnSystem.onClick.AddListener(() => OpenSubMenu(systemPanel, null));

        CloseAllSubMenus();
        Debug.Log("[OverviewMenu] Start completado.", this);
    }

    public void Enter()
    {
        currentMode = MenuMode.Overview;

        if (rootPanel != null)
            rootPanel.SetActive(true);
        if (partyPanel != null)
            partyPanel.SetActive(true);

        CloseAllSubMenus();

        currentButton = btnEquipment;
        if (EventSystem.current != null && currentButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(currentButton.gameObject);
        }

        if (rootMenuContainer != null)
            rootMenuContainer.SetActive(true);

        if (partyPanelSelector != null)
        {
            partyPanelSelector.gameObject.SetActive(true);
            partyPanelSelector.SetLocked(true, currentMode);
        }

        Debug.Log("[OverviewMenu] Root menu abierto.");
    }

    public void Exit()
    {
        if (actionButtonSpawner != null)
            actionButtonSpawner.OnCharacterButtonPressed.RemoveListener(HandleCharacterAction);

        if (rootPanel != null)
            rootPanel.SetActive(false);
        if (partyPanel != null)
            partyPanel.SetActive(false);

        CloseAllSubMenus();
        currentMode = MenuMode.None;
        if (partyPanelSelector != null)
            partyPanelSelector.SetLocked(true, MenuMode.None);
    }

    public void Update()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && currentButton != null)
        {
            EventSystem.current.SetSelectedGameObject(currentButton.gameObject);
        }

        if (Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape))
        {
            if (AnySubMenuOpen())
            {
                CloseAllSubMenus();
                if (rootPanel != null)
                    rootPanel.SetActive(true);
                if (partyPanel != null)
                    partyPanel.SetActive(true);
                currentMode = MenuMode.Overview;
                if (EventSystem.current != null && btnEquipment != null)
                    EventSystem.current.SetSelectedGameObject(btnEquipment.gameObject);
            }
        }
    }

    private void OpenPartySelection()
    {
        if (actionButtonSpawner == null)
        {
            actionButtonSpawner = FindObjectOfType<ActionButtonSpawner>(true);
            if (actionButtonSpawner != null)
            {
                Debug.Log("[OverviewMenu] ActionButtonSpawner asignado dinamicamente.");
            }
            else
            {
                Debug.LogWarning("[OverviewMenu] No se encontro ActionButtonSpawner en escena.");
                return;
            }
        }

        actionButtonSpawner.ClearButtons();
        actionButtonSpawner.SpawnButtons(currentMode);

        actionButtonSpawner.OnCharacterButtonPressed.RemoveListener(HandleCharacterAction);
        actionButtonSpawner.OnCharacterButtonPressed.AddListener(HandleCharacterAction);

        if (rootPanel != null)
            rootPanel.SetActive(false);
        if (partyPanel != null)
            partyPanel.SetActive(true);

        if (partyPanelSelector != null)
        {
            partyPanelSelector.SetLocked(false, currentMode);
            Debug.Log("[OverviewMenu] PartyPanel desbloqueado para seleccionar personaje (" + currentMode + ").");
        }
        else
        {
            Debug.LogWarning("[OverviewMenu] PartyPanelSelector no asignado.");
        }

        CloseAllSubMenus();
    }

    private void HandleCharacterAction(CharacterRuntime character)
    {
        if (character == null)
        {
            Debug.LogWarning("[OverviewMenu] HandleCharacterAction recibio un personaje nulo.");
            return;
        }

        Debug.Log("[OverviewMenu] Accion sobre " + character.Archetype.characterName + " en modo " + currentMode);

        switch (currentMode)
        {
            case MenuMode.Equipment:
                Debug.Log("[OverviewMenu] Abrir menu de equipo para " + character.Archetype.characterName);
                break;
            case MenuMode.Skills:
                Debug.Log("[OverviewMenu] Abrir menu de habilidades para " + character.Archetype.characterName);
                break;
            case MenuMode.Stats:
                Debug.Log("[OverviewMenu] Abrir menu de stats/clase para " + character.Archetype.characterName);
                break;
        }

        if (actionButtonSpawner != null)
            actionButtonSpawner.ClearButtons();

        if (partyPanelSelector != null)
            partyPanelSelector.SetLocked(true, MenuMode.None);
    }

    private void OpenSubMenu(GameObject subPanel, GameObject optionalPartyPanel)
    {
        CloseAllSubMenus();

        if (rootPanel != null)
            rootPanel.SetActive(false);

        if (optionalPartyPanel != null)
            optionalPartyPanel.SetActive(true);
        else if (partyPanel != null)
            partyPanel.SetActive(false);

        if (subPanel != null)
            subPanel.SetActive(true);

        if (partyPanelSelector != null)
            partyPanelSelector.SetLocked(true, MenuMode.Overview);
    }

    private void CloseAllSubMenus()
    {
        if (itemsPanel != null)
            itemsPanel.SetActive(false);
        if (questsPanel != null)
            questsPanel.SetActive(false);
        if (systemPanel != null)
            systemPanel.SetActive(false);
    }

    private bool AnySubMenuOpen()
    {
        bool itemsOpen = itemsPanel != null && itemsPanel.activeSelf;
        bool questsOpen = questsPanel != null && questsPanel.activeSelf;
        bool systemOpen = systemPanel != null && systemPanel.activeSelf;
        return itemsOpen || questsOpen || systemOpen;
    }
}

