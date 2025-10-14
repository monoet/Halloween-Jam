using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawnea UI de HP/SP muy básica para party y enemigos usando CharacterSlotUI como placeholder.
/// </summary>
public class BattleHUDManager : MonoBehaviour
{
    [Header("Prefabs y contenedores")]
    [SerializeField] private CharacterSlotUI slotPrefab;
    [SerializeField] private Transform partyRoot;
    [SerializeField] private Transform enemyRoot;

    private readonly Dictionary<CharacterRuntime, CharacterSlotUI> slots = new Dictionary<CharacterRuntime, CharacterSlotUI>();

    public void Build(List<CharacterRuntime> party, List<CharacterRuntime> enemies)
    {
        Clear();
        if (slotPrefab == null)
        {
            Debug.LogWarning("[BattleHUDManager] slotPrefab no asignado.");
            return;
        }

        if (party != null && partyRoot != null)
        {
            foreach (var c in party)
            {
                if (c == null) continue;
                var ui = Instantiate(slotPrefab, partyRoot);
                ui.Init(c);
                slots[c] = ui;
                var st = c.GetComponent<CombatantState>();
                if (st != null) st.OnVitalsChanged.AddListener(ui.Refresh);
            }
        }

        if (enemies != null && enemyRoot != null)
        {
            foreach (var c in enemies)
            {
                if (c == null) continue;
                var ui = Instantiate(slotPrefab, enemyRoot);
                ui.Init(c);
                slots[c] = ui;
                var st = c.GetComponent<CombatantState>();
                if (st != null) st.OnVitalsChanged.AddListener(ui.Refresh);
            }
        }
    }

    public void Clear()
    {
        foreach (var kv in slots)
        {
            var c = kv.Key;
            var st = c != null ? c.GetComponent<CombatantState>() : null;
            if (st != null)
                st.OnVitalsChanged.RemoveListener(kv.Value.Refresh);
        }
        slots.Clear();

        if (partyRoot != null)
        {
            for (int i = partyRoot.childCount - 1; i >= 0; i--)
                Destroy(partyRoot.GetChild(i).gameObject);
        }
        if (enemyRoot != null)
        {
            for (int i = enemyRoot.childCount - 1; i >= 0; i--)
                Destroy(enemyRoot.GetChild(i).gameObject);
        }
    }
}

