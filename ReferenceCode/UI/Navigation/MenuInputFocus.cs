// Assets/Scripts/UI/Navigation/MenuInputFocus.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Garantiza que siempre haya un botón seleccionado en el menú.
/// Si el EventSystem pierde el foco, reasigna el primero automáticamente.
/// </summary>
public class MenuInputFocus : MonoBehaviour
{
    [Tooltip("Botón que tendrá el foco inicial al abrir el menú.")]
    [SerializeField] private Button defaultButton;

    private void OnEnable()
    {
        if (defaultButton == null)
        {
            Debug.LogWarning($"⚠️ MenuInputFocus en {name}: no tiene un botón asignado.");
            return;
        }

        // 🔹 Forzar foco inicial cuando el panel se activa
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
    }

    private void Update()
    {
        // 🔄 Si se pierde el foco (por abrir otro panel, etc.), volver a asignar
        if (EventSystem.current.currentSelectedGameObject == null && defaultButton != null)
        {
            EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
        }
    }
}
