// Assets/Scripts/Systems/EventSystemController.cs
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Encapsula la lógica del EventSystem y garantiza que solo exista uno.
/// También permite reasignar foco y reinicializarlo.
/// </summary>
[RequireComponent(typeof(EventSystem))]
public class EventSystemController : MonoBehaviour
{
    public static EventSystemController Instance { get; private set; }

    private EventSystem eventSystem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        eventSystem = GetComponent<EventSystem>();
    }

    public void SetSelected(GameObject go)
    {
        if (eventSystem == null || go == null) return;

        eventSystem.SetSelectedGameObject(null);
        eventSystem.SetSelectedGameObject(go);
    }

    public GameObject GetCurrentSelection()
    {
        return eventSystem?.currentSelectedGameObject;
    }

    public void ClearSelection()
    {
        if (eventSystem != null)
            eventSystem.SetSelectedGameObject(null);
    }
}
