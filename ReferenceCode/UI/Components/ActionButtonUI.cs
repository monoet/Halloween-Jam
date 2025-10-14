using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActionButtonUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Button button;

    private CharacterRuntime characterRef;
    private OverviewMenu.MenuMode mode;
    private System.Action onPressed;

    public void Setup(CharacterRuntime character, OverviewMenu.MenuMode currentMode, System.Action callback)
    {
        characterRef = character;
        mode = currentMode;
        onPressed = callback;

        if (label != null)
            label.text = character.Archetype.characterName;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                Debug.Log("[ActionButtonUI] Click en boton de " + character.Archetype.characterName + " (" + mode + ")");
                onPressed?.Invoke();
            });
        }
        else
        {
            Debug.LogWarning("[ActionButtonUI] Sin componente Button en " + name);
        }
    }
}

