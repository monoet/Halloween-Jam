using System.Collections.Generic;
using UnityEngine;

public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    [Header("Datos de personajes")]
    [SerializeField] private List<CharacterRuntime> allPartyMembers = new List<CharacterRuntime>(); // roster completo
    [SerializeField, Range(1, 8)] private int activeCount = 4;

    public List<CharacterRuntime> ActiveParty { get; private set; } = new List<CharacterRuntime>();

    [Header("UI Prefabs y contenedor")]
    [SerializeField] private GameObject characterSlotUIPrefab;
    [SerializeField] private GameObject separatorPrefab;
    [SerializeField] private Transform partyGridParent;
    private readonly List<CharacterSlotUI> uiSlots = new List<CharacterSlotUI>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        GeneratePartyGrid();
    }

    public void GeneratePartyGrid()
    {
        if (characterSlotUIPrefab == null || partyGridParent == null)
        {
            Debug.LogWarning("[PartyManager] Prefab o contenedor no asignado.");
            return;
        }

        foreach (Transform child in partyGridParent)
            Destroy(child.gameObject);
        uiSlots.Clear();
        ActiveParty.Clear();

        for (int i = 0; i < allPartyMembers.Count; i++)
        {
            CharacterRuntime member = allPartyMembers[i];
            if (member == null)
            {
                Debug.LogWarning("[PartyManager] Miembro " + i + " no asignado.");
                continue;
            }

            bool isActive = i < activeCount;
            if (isActive)
                ActiveParty.Add(member);

            GameObject go = Instantiate(characterSlotUIPrefab, partyGridParent);
            CharacterSlotUI slotUI = go.GetComponent<CharacterSlotUI>();
            slotUI.Init(member);
            slotUI.SetActiveVisual(isActive);
            uiSlots.Add(slotUI);

            if (separatorPrefab != null && i < allPartyMembers.Count - 1)
                Instantiate(separatorPrefab, partyGridParent);
        }

        Debug.Log("[PartyManager] Party inicializada con " + ActiveParty.Count + " miembros activos.");
    }

    public void RefreshAll()
    {
        foreach (var slot in uiSlots)
            slot.Refresh();
    }
}

