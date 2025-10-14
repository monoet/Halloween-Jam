using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controla el menu principal (abrir/cerrar con ESC o Start).
/// Gestiona el estado actual de IMenuState y la animacion de entrada/salida.
/// </summary>
public class MenuManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private OverviewMenu overviewMenu;
    [SerializeField] private MenuSlideAnimator slideAnimator;

    private IMenuState currentMenu;
    private bool menuOpen;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            ToggleMenu();

        currentMenu?.Update();
    }

    public void ToggleMenu()
    {
        if (menuOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    private void OpenMenu()
    {
        menuOpen = true;

        if (overviewMenu == null)
            return;

        overviewMenu.gameObject.SetActive(true);
        slideAnimator?.SlideIn();
        SetMenu(overviewMenu);

        if (overviewMenu.DefaultButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(overviewMenu.DefaultButton.gameObject);
        }
    }

    private void CloseMenu()
    {
        menuOpen = false;

        float disableDelay = 0f;
        if (slideAnimator != null)
        {
            slideAnimator.SlideOut();
            disableDelay = slideAnimator.HideDuration;
        }

        if (disableDelay > 0f)
            Invoke(nameof(DisableMenu), disableDelay);
        else
            DisableMenu();
    }

    private void DisableMenu()
    {
        if (overviewMenu != null)
            overviewMenu.gameObject.SetActive(false);
    }

    private void SetMenu(IMenuState newMenu)
    {
        currentMenu?.Exit();

        currentMenu = newMenu;
        currentMenu?.Enter();
    }
}

