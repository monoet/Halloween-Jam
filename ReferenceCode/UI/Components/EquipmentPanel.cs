using System.Collections;
using UnityEngine;
using TMPro;

public class EquipmentPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField, Min(0f)] private float initializationTimeout = 2f;

    private void OnEnable()
    {
        StartCoroutine(InitializeAsync());
    }

    private IEnumerator InitializeAsync()
    {
        float elapsed = 0f;
        while ((PartyManager.Instance == null || PartyManager.Instance.ActiveParty.Count == 0) && elapsed < initializationTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (PartyManager.Instance == null)
        {
            Debug.LogWarning("[EquipmentPanel] PartyManager no esta disponible.");
            yield break;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (PartyManager.Instance == null)
        {
            Debug.LogWarning("[EquipmentPanel] Refresh llamado sin PartyManager.");
            return;
        }

        var active = PartyManager.Instance.ActiveParty;

        foreach (var member in active)
        {
            Debug.Log("[EquipmentPanel] Actualizando UI de " + member.Archetype.characterName);
        }

        if (debugText != null)
            debugText.text = "Miembros activos: " + active.Count;
    }
}

