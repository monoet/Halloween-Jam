using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Punto central del combate: coordina turnos, UI y resolución de acciones.
/// </summary>
public class BattleManager : MonoBehaviour
{
    [Header("Referencias principales")]
    [SerializeField] private PartyManager partyManager;
    [SerializeField] private TurnController turnController;
    [SerializeField] private BattleUIManager battleUI;
    [SerializeField] private BattleHUDManager battleHUD;
    [SerializeField] private ActionResolver actionResolver;
    [SerializeField] private BattleEndManager battleEndManager;

    [Header("Dato de encuentro (opcional para pruebas)")]
    [SerializeField] private EncounterData debugEncounter;

    [Header("Estado runtime")]
    [SerializeField] private List<CharacterRuntime> activePartyMembers = new List<CharacterRuntime>();
    [SerializeField] private List<CharacterRuntime> activeEnemies = new List<CharacterRuntime>();

    [System.Serializable]
    public class BattleEvent : UnityEvent { }

    [System.Serializable]
    public class BattleResultEvent : UnityEvent<bool> { }

    [Header("Eventos")]
    [SerializeField] private BattleEvent onBattleStarted;
    [SerializeField] private BattleResultEvent onBattleEnded;

    private bool battleActive;
    private CharacterRuntime currentActor;
    private bool waitingForAction;
    [SerializeField] private TimedHitController timedHitController;

    private void Awake()
    {
        if (turnController == null)
            turnController = GetComponentInChildren<TurnController>(true);

        if (battleUI == null)
            battleUI = GetComponentInChildren<BattleUIManager>(true);

        if (actionResolver == null)
            actionResolver = GetComponentInChildren<ActionResolver>(true);

        if (battleEndManager == null)
            battleEndManager = GetComponentInChildren<BattleEndManager>(true);

        if (battleHUD == null)
            battleHUD = GetComponentInChildren<BattleHUDManager>(true);

        if (timedHitController == null)
            timedHitController = GetComponentInChildren<TimedHitController>(true);
        if (timedHitController == null)
            timedHitController = FindObjectOfType<TimedHitController>(true);
    }

    private void OnEnable()
    {
        if (battleUI != null)
            battleUI.OnActionSelected.AddListener(HandleActionSelected);

        if (battleEndManager != null)
            battleEndManager.OnBattleFinished.AddListener(HandleBattleFinished);

        if (actionResolver != null)
            actionResolver.OnActionComplete.AddListener(HandleActionComplete);
    }

    private void OnDisable()
    {
        if (battleUI != null)
            battleUI.OnActionSelected.RemoveListener(HandleActionSelected);

        if (battleEndManager != null)
            battleEndManager.OnBattleFinished.RemoveListener(HandleBattleFinished);

        if (actionResolver != null)
            actionResolver.OnActionComplete.RemoveListener(HandleActionComplete);
    }

    private void Start()
    {
        if (debugEncounter != null)
        {
            Debug.Log("[BattleManager] Iniciando combate de prueba con EncounterData asignado en inspector.", this);
            StartBattle(debugEncounter);
        }
    }

    public void StartBattle(EncounterData encounterData)
    {
        if (battleActive)
        {
            Debug.LogWarning("[BattleManager] Ya hay un combate en curso.");
            return;
        }

        if (partyManager == null)
        {
            partyManager = FindObjectOfType<PartyManager>();
            if (partyManager == null)
            {
                Debug.LogError("[BattleManager] No se encontró PartyManager en escena.");
                return;
            }
        }

        PreparePartyMembers();
        SpawnEnemies(encounterData);

        if (activePartyMembers.Count == 0 || activeEnemies.Count == 0)
        {
            Debug.LogWarning("[BattleManager] No se pudo iniciar combate: faltan unidades activas.");
            CleanupAfterBattle();
            return;
        }

        battleActive = true;

        var allCombatants = new List<CharacterRuntime>();
        allCombatants.AddRange(activePartyMembers);
        allCombatants.AddRange(activeEnemies);

        turnController.CalculateTurnOrder(allCombatants);

        onBattleStarted?.Invoke();
        Debug.Log("[BattleManager] Combate iniciado con " + allCombatants.Count + " participantes.");

        if (battleHUD != null)
            battleHUD.Build(activePartyMembers, activeEnemies);

        AdvanceTurn();
    }

    public void EndBattle(bool victory)
    {
        if (!battleActive)
            return;

        battleActive = false;
        onBattleEnded?.Invoke(victory);
        Debug.Log("[BattleManager] Combate finalizado. Victoria: " + victory);

        CleanupAfterBattle();
    }

    private void AdvanceTurn()
    {
        if (!battleActive)
            return;

        currentActor = turnController.NextTurn();
        if (currentActor == null)
        {
            Debug.LogWarning("[BattleManager] No se encontró siguiente actor. Recalculando turnos.");
            turnController.RebuildQueue();
            currentActor = turnController.NextTurn();
        }

        if (currentActor == null)
        {
            Debug.LogError("[BattleManager] La cola de turnos sigue vacía. Abortando combate.");
            EndBattle(false);
            return;
        }

        Debug.Log("[BattleManager] Turno de " + currentActor.Archetype.characterName);

        if (battleUI != null)
        {
            var actions = GatherAvailableActions(currentActor);
            var targets = GetPossibleTargets(currentActor);
            // Si hay UI interactiva configurada, la usamos; si no, fallback al placeholder auto.
            if (!battleUI.ShowInteractivePanels(currentActor, actions, targets))
            {
                battleUI.ShowCommandPanel(currentActor, actions);
            }
        }
    }

    private List<ActionData> GatherAvailableActions(CharacterRuntime actor)
    {
        // TODO: Integrar Keepsakes, Skills y acciones sincronizadas.
        var actions = new List<ActionData>();

        if (actor is EnemyRuntime enemy && enemy.DefaultAction != null)
        {
            actions.Add(enemy.DefaultAction);
            return actions;
        }

        if (battleUI != null)
            actions.AddRange(battleUI.DefaultActions);

        return actions;
    }

    private void HandleActionSelected(CharacterRuntime actor, ActionData action, CharacterRuntime target)
    {
        if (!battleActive)
        {
            Debug.LogWarning("[BattleManager] Acción recibida pero el combate no está activo.");
            return;
        }
        if (actionResolver == null)
        {
            Debug.LogError("[BattleManager] No hay ActionResolver asignado.");
            return;
        }

        target = target ?? GetDefaultTarget(actor);
        if (target == null)
        {
            Debug.LogWarning("[BattleManager] No se halló objetivo válido. Saltando turno.");
            AdvanceTurn();
            return;
        }

        if (action.IsTimedHit && timedHitController != null)
        {
            waitingForAction = true;
            timedHitController.StartTimedHit(action, (mult) =>
            {
                actionResolver.ExecuteAction(actor, action, target, mult);
            });
        }
        else
        {
            waitingForAction = true;
            actionResolver.ExecuteAction(actor, action, target);
        }
    }

    private void HandleBattleFinished(bool playerWon)
    {
        EndBattle(playerWon);
    }

    private void PreparePartyMembers()
    {
        activePartyMembers.Clear();

        if (partyManager != null)
        {
            activePartyMembers.AddRange(partyManager.ActiveParty);
            Debug.Log("[BattleManager] Party cargada con " + activePartyMembers.Count + " miembros.");
            // Asegurar estados de combate
            foreach (var member in activePartyMembers)
            {
                if (member == null) continue;
                var state = member.GetComponent<CombatantState>();
                if (state == null) state = member.gameObject.AddComponent<CombatantState>();
                state.InitializeFrom(member);
                EnsureCombatantVisual(member);
            }
        }
    }

    private void SpawnEnemies(EncounterData encounterData)
    {
        activeEnemies.Clear();

        if (encounterData == null)
        {
            Debug.LogWarning("[BattleManager] EncounterData nulo, no se spawnearán enemigos.");
            return;
        }

        Debug.Log("[BattleManager] Spawneando enemigos para encounter " + encounterData.EncounterName);

        for (int i = 0; i < encounterData.EnemiesToSpawn.Count; i++)
        {
            var enemyPrefab = encounterData.EnemiesToSpawn[i];
            if (enemyPrefab == null)
            {
                Debug.LogWarning("[BattleManager] Enemy prefab en índice " + i + " es nulo.");
                continue;
            }

            Vector3 spawnPoint = encounterData.GetSpawnPoint(i);
            EnemyRuntime enemyInstance = Instantiate(enemyPrefab, spawnPoint, Quaternion.identity);
            enemyInstance.name = enemyPrefab.name + "_Runtime";
            // Asegurar estado de combate
            var state = enemyInstance.GetComponent<CombatantState>();
            if (state == null) state = enemyInstance.gameObject.AddComponent<CombatantState>();
            state.InitializeFrom(enemyInstance);
            EnsureCombatantVisual(enemyInstance);
            activeEnemies.Add(enemyInstance);
        }

        Debug.Log("[BattleManager] Enemigos activos: " + activeEnemies.Count);
    }

    private void CleanupAfterBattle()
    {
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
                Destroy(enemy.gameObject);
        }

        activeEnemies.Clear();
        activePartyMembers.Clear();
        turnController.Clear();
        currentActor = null;
        waitingForAction = false;
    }

    private void EnsureCombatantVisual(CharacterRuntime runtime)
    {
        if (runtime == null) return;
        if (runtime.GetComponentInChildren<Renderer>() != null) return;

        var archetype = runtime.Archetype;
        if (archetype != null)
        {
            if (archetype.battlePrefab != null)
            {
                var battleVis = Instantiate(archetype.battlePrefab, runtime.transform);
                battleVis.transform.localPosition = Vector3.zero;
                battleVis.transform.localRotation = Quaternion.identity;
                return;
            }

            if (archetype.portrait != null)
            {
                var go = new GameObject("SpriteVisual", typeof(SpriteRenderer), typeof(BillboardSprite));
                go.transform.SetParent(runtime.transform, false);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = archetype.portrait;
                sr.sortingOrder = 10;
                var bounds = sr.sprite.bounds;
                go.transform.localPosition = new Vector3(0f, Mathf.Max(0f, bounds.extents.y), 0f);
                return;
            }
        }

        var fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fallback.name = "FallbackCylinder";
        fallback.transform.SetParent(runtime.transform, false);
        fallback.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        var collider = fallback.GetComponent<Collider>();
        if (collider) Destroy(collider);
    }

    private CharacterRuntime GetDefaultTarget(CharacterRuntime actor)
    {
        bool actorIsParty = activePartyMembers.Contains(actor);
        List<CharacterRuntime> targetPool = actorIsParty ? activeEnemies : activePartyMembers;

        foreach (var candidate in targetPool)
        {
            if (candidate != null)
            {
                Debug.Log("[BattleManager] Objetivo seleccionado automáticamente: " + candidate.Archetype.characterName);
                return candidate;
            }
        }

        return null;
    }

    private List<CharacterRuntime> GetPossibleTargets(CharacterRuntime actor)
    {
        var list = new List<CharacterRuntime>();
        bool actorIsParty = activePartyMembers.Contains(actor);
        List<CharacterRuntime> pool = actorIsParty ? activeEnemies : activePartyMembers;
        foreach (var c in pool)
        {
            if (c == null) continue;
            var st = c.GetComponent<CombatantState>();
            if (st == null || st.IsAlive)
                list.Add(c);
        }
        return list;
    }

    private void HandleActionComplete()
    {
        if (!battleActive) return;
        waitingForAction = false;
        if (battleEndManager != null)
        {
            bool finished = battleEndManager.CheckBattleState(activePartyMembers, activeEnemies);
            if (finished)
                return;
        }
        AdvanceTurn();
    }
}
