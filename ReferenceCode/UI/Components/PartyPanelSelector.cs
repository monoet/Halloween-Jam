using UnityEngine;

/// <summary>
/// Controla la seleccion de personajes dentro del Party Panel.
/// Permite activar o bloquear la interaccion segun el menu actual.
/// </summary>
public class PartyPanelSelector : MonoBehaviour
{
    private bool isLocked = true; // Por defecto bloqueado hasta que un menu lo habilite
    private OverviewMenu.MenuMode currentMode = OverviewMenu.MenuMode.None;

    /// <summary>
    /// Bloquea o desbloquea la interaccion del PartyPanel (mantiene visibilidad pero sin hover ni seleccion).
    /// </summary>
    public void SetLocked(bool state, OverviewMenu.MenuMode mode = OverviewMenu.MenuMode.None)
    {
        isLocked = state;
        currentMode = mode;

        // Solo afectar a los CharacterSlotUI, ignorar otros objetos como el ActionButtonSpawner
        var slots = GetComponentsInChildren<CharacterSlotUI>(includeInactive: true);
        foreach (var slot in slots)
        {
            slot.SetSelectable(!isLocked, currentMode);
        }

        // Si existe un ActionButtonSpawner dentro del panel, mantenerlo activo
        if (TryGetComponent<ActionButtonSpawner>(out var spawner))
        {
            spawner.gameObject.SetActive(true);
        }
        else
        {
            var spawnerInChildren = GetComponentInChildren<ActionButtonSpawner>(includeInactive: true);
            if (spawnerInChildren != null)
                spawnerInChildren.gameObject.SetActive(true);
        }

        Debug.Log(isLocked ? "[PartyPanel] Bloqueado." : "[PartyPanel] Desbloqueado.");
    }

    /// <summary>
    /// Verifica si el PartyPanel esta bloqueado.
    /// </summary>
    public bool IsLocked()
    {
        return isLocked;
    }

    /// <summary>
    /// Llamado por CharacterSlotUI cuando el jugador selecciona un personaje.
    /// </summary>
    public void OnSelectCharacter(CharacterRuntime character, OverviewMenu.MenuMode mode)
    {
        if (isLocked)
        {
            Debug.LogWarning("[PartyPanel] Intento de seleccion ignorado (panel bloqueado).");
            return;
        }

        switch (mode)
        {
            case OverviewMenu.MenuMode.Equipment:
                OpenEquipmentFor(character);
                break;

            case OverviewMenu.MenuMode.Skills:
                OpenSkillsFor(character);
                break;

            case OverviewMenu.MenuMode.Stats:
                OpenStatsFor(character);
                break;

            default:
                Debug.LogWarning("[PartyPanel] Modo de menu no soportado para seleccion.");
                break;
        }
    }

    private void OpenEquipmentFor(CharacterRuntime character)
    {
        Debug.Log("[PartyPanel] Abriendo Equipment para " + character.Archetype.characterName);
        // Aqui luego: mostrar panel de equipo del personaje
    }

    private void OpenSkillsFor(CharacterRuntime character)
    {
        Debug.Log("[PartyPanel] Abriendo Skills para " + character.Archetype.characterName);
    }

    private void OpenStatsFor(CharacterRuntime character)
    {
        Debug.Log("[PartyPanel] Abriendo Stats/Class para " + character.Archetype.characterName);
    }
}

