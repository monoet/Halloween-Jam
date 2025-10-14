// Scripts/UI/EquipmentMenu.cs
using UnityEngine;
using TMPro;

public class EquipmentMenu : MonoBehaviour, IMenuState
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;

    public void Enter()
    {
        gameObject.SetActive(true);
        if (titleText != null) titleText.text = "Equipment Menu (Placeholder)";
    }

    public void Exit()
    {
        gameObject.SetActive(false);
    }

    public void Update()
    {
        // Aquí luego vas a manejar la lógica de equipar
    }
}
