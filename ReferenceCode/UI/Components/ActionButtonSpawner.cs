using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ActionButtonSpawner : MonoBehaviour
{
    [Header("Prefabs y Contenedor")]
    [SerializeField] private GameObject actionButtonPrefab;
    [SerializeField] private Transform buttonContainer;

    public UnityEvent<CharacterRuntime> OnCharacterButtonPressed = new UnityEvent<CharacterRuntime>();
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    public void SpawnButtons(OverviewMenu.MenuMode mode)
    {
        ClearButtons();

        if (PartyManager.Instance == null)
        {
            Debug.LogWarning("[ActionButtonSpawner] No hay PartyManager activo, no se pueden generar botones.");
            return;
        }

        if (actionButtonPrefab == null || buttonContainer == null)
        {
            Debug.LogError("[ActionButtonSpawner] Spawner sin prefab o contenedor asignado.");
            return;
        }

        foreach (var character in PartyManager.Instance.ActiveParty)
        {
            var buttonObj = Instantiate(actionButtonPrefab, buttonContainer);
            var ui = buttonObj.GetComponent<ActionButtonUI>();

            if (ui == null)
            {
                Debug.LogError("[ActionButtonSpawner] El prefab no tiene ActionButtonUI.");
                Destroy(buttonObj);
                continue;
            }

            ui.Setup(character, mode, () => OnCharacterButtonPressed.Invoke(character));
            spawnedButtons.Add(buttonObj);
        }

        Debug.Log($"[ActionButtonSpawner] Generados {spawnedButtons.Count} botones para modo {mode}");
    }

    public void ClearButtons()
    {
        foreach (var btn in spawnedButtons)
        {
            if (btn != null) Destroy(btn);
        }
        spawnedButtons.Clear();
    }
}

