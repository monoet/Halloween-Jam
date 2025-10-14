// Assets/Scripts/UI/Navigation/MenuNavigationHelper.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Configura automáticamente la navegación vertical entre los botones hijos de este objeto.
/// Ideal para menús tipo JRPG donde se usan flechas ↑↓ o D-Pad.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MenuNavigationHelper : MonoBehaviour
{
    [Tooltip("Si está activo, crea un loop (el último baja al primero y viceversa).")]
    public bool loopNavigation = true;

    private void Start()
    {
        SetupNavigation();
    }

    public void SetupNavigation()
    {
        // Obtiene todos los botones hijos activos en orden jerárquico
        Button[] buttons = GetComponentsInChildren<Button>(includeInactive: false);

        if (buttons.Length == 0)
        {
            Debug.LogWarning($"⚠️ MenuNavigationHelper en {name}: no encontró botones hijos.");
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Navigation nav = buttons[i].navigation;
            nav.mode = Navigation.Mode.Explicit;

            int upIndex = i - 1;
            int downIndex = i + 1;

            if (loopNavigation)
            {
                if (i == 0) upIndex = buttons.Length - 1;
                if (i == buttons.Length - 1) downIndex = 0;
            }

            if (upIndex >= 0 && upIndex < buttons.Length)
                nav.selectOnUp = buttons[upIndex];
            if (downIndex >= 0 && downIndex < buttons.Length)
                nav.selectOnDown = buttons[downIndex];

            buttons[i].navigation = nav;
        }

        Debug.Log($"✅ Configurada navegación para {buttons.Length} botones en {name}.");
    }
}
