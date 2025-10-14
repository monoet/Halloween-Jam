using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bootstrap de pruebas para el sistema de combate.
/// Se instancia manualmente en escena y expone campos en el inspector.
/// No ejecuta nada automáticamente: usa el botón de ContextMenu o StartTestBattle().
/// </summary>
public class BattleTestBootstrap : MonoBehaviour
{
    [Header("Referencias (opcionales)")]
    [SerializeField] private PartyManager partyManager;
    [SerializeField] private BattleManager battleManager;

    [Header("Configuración de prueba")]
    [SerializeField] private List<EnemyRuntime> enemyPrefabs = new List<EnemyRuntime>();
    [SerializeField] private EncounterData encounterDataOptional;
    [SerializeField] private ActionData testAction;
    [SerializeField] private List<Transform> enemySpawnAnchors = new List<Transform>();
    [SerializeField] private List<Transform> partySpawnAnchors = new List<Transform>();

    [Header("Posicionamiento Party (opcional)")]
    [SerializeField] private bool positionPartyOnStart = true;

    [Header("Isometric Layout (fallback si no hay anchors)")]
    [SerializeField] private bool useIsometricLayout = true;
    [SerializeField] private Vector3 isoEnemyOrigin = new Vector3(-3f, 0f, 3f);
    [SerializeField] private Vector3 isoPartyOrigin = new Vector3(3f, 0f, -3f);
    [SerializeField] private float isoSpacingX = 1.75f;
    [SerializeField] private float isoSpacingY = 1.00f;
    [SerializeField, Range(1, 4)] private int isoRows = 2;
    [SerializeField] private Camera layoutCamera; // si no se asigna, usa Camera.main
    [SerializeField] private bool alignToCamera = true;
    [SerializeField] private bool lockYToZero = true;

    [Header("Opciones")]
    [SerializeField] private bool setActionAsDefaultInUI = true;

    [Header("Testing (QA)")]
    [SerializeField] private bool bindForceNextCritToHotkey = false;
    [SerializeField] private KeyCode forceCritKey = KeyCode.C;

    [ContextMenu("Start Test Battle")]
    public void ContextStartTestBattle()
    {
        StartTestBattle();
    }

    [ContextMenu("Force Next Crit (Testing)")]
    public void ForceNextCrit()
    {
        // Busca un ActionResolver y marca el siguiente ataque como crítico
        ActionResolver resolver = null;
        if (battleManager != null)
            resolver = battleManager.GetComponentInChildren<ActionResolver>(true);
        if (resolver == null)
            resolver = FindObjectOfType<ActionResolver>(true);

        if (resolver != null)
        {
            resolver.ForceNextCrit();
        }
        else
        {
            Debug.LogWarning("[BattleTestBootstrap] No se encontró ActionResolver para forzar CRIT.");
        }
    }

    public void StartTestBattle()
    {
        ResolveReferences();

        if (battleManager == null)
        {
            Debug.LogError("[BattleTestBootstrap] No hay BattleManager en escena ni asignado.");
            return;
        }

        // Opcional: configurar la UI con la acción de prueba
        if (setActionAsDefaultInUI && testAction != null)
        {
            var ui = battleManager.GetComponentInChildren<BattleUIManager>(true);
            if (ui != null)
            {
                ui.DefaultActions.Clear();
                ui.DefaultActions.Add(testAction);
                Debug.Log("[BattleTestBootstrap] Configurada ActionData de prueba en BattleUIManager.DefaultActions: " + testAction.name);
            }
            else
            {
                Debug.LogWarning("[BattleTestBootstrap] No se encontró BattleUIManager en hijos de BattleManager.");
            }
        }

        // Preparar EncounterData
        var encounterToUse = BuildEncounterForTest();
        if (encounterToUse == null)
        {
            Debug.LogError("[BattleTestBootstrap] No se pudo crear/usar EncounterData. Asigna un asset o al menos un prefab de enemigo.");
            return;
        }

        int count = encounterToUse.EnemiesToSpawn != null ? encounterToUse.EnemiesToSpawn.Count : 0;
        // Posicionar party si corresponde
        if (positionPartyOnStart && partyManager != null && partyManager.ActiveParty != null)
        {
            ApplyPartyPositions(partyManager.ActiveParty);
        }

        Debug.Log($"[BattleTestBootstrap] Iniciando batalla de prueba con {count} enemigos.");

        battleManager.StartBattle(encounterToUse);
    }

    private void Update()
    {
        if (bindForceNextCritToHotkey && Input.GetKeyDown(forceCritKey))
        {
            ForceNextCrit();
        }
    }

    private void ResolveReferences()
    {
        if (partyManager == null)
        {
            partyManager = FindObjectOfType<PartyManager>();
            if (partyManager != null)
                Debug.Log("[BattleTestBootstrap] PartyManager asignado dinámicamente.");
            else
                Debug.LogWarning("[BattleTestBootstrap] No se encontró PartyManager en escena.");
        }

        if (battleManager == null)
        {
            battleManager = FindObjectOfType<BattleManager>();
            if (battleManager != null)
                Debug.Log("[BattleTestBootstrap] BattleManager asignado dinámicamente.");
        }
    }

    private EncounterData BuildEncounterForTest()
    {
        // Si hay un asset asignado y tiene suficientes spawn points, úsalo tal cual.
        if (encounterDataOptional != null && encounterDataOptional.EnemiesToSpawn != null && encounterDataOptional.EnemiesToSpawn.Count > 0)
        {
            // Si además se especificaron anchors, generamos un runtime copy para no modificar el asset.
            if (enemySpawnAnchors != null && enemySpawnAnchors.Count > 0)
            {
                var copy = ScriptableObject.CreateInstance<EncounterData>();
                foreach (var e in encounterDataOptional.EnemiesToSpawn)
                    copy.EnemiesToSpawn.Add(e);
                FillSpawnPoints(copy, copy.EnemiesToSpawn.Count, isEnemy:true);
                Debug.Log("[BattleTestBootstrap] Usando copia runtime de EncounterData con anchors proporcionados.");
                return copy;
            }
            // Si el asset no tiene spawn points suficientes, creamos copia y rellenamos.
            if (encounterDataOptional.SpawnPoints.Count < encounterDataOptional.EnemiesToSpawn.Count)
            {
                var copy = ScriptableObject.CreateInstance<EncounterData>();
                foreach (var e in encounterDataOptional.EnemiesToSpawn)
                    copy.EnemiesToSpawn.Add(e);
                // Copiar puntos existentes y completar
                copy.SpawnPoints.AddRange(encounterDataOptional.SpawnPoints);
                while (copy.SpawnPoints.Count < copy.EnemiesToSpawn.Count)
                    copy.SpawnPoints.Add(Vector3.zero);
                FillSpawnPoints(copy, copy.EnemiesToSpawn.Count, preserveExisting: true, isEnemy:true);
                Debug.Log("[BattleTestBootstrap] Usando copia runtime y completando spawn points faltantes.");
                return copy;
            }
            return encounterDataOptional;
        }

        // Si no hay asset, generamos encuentro desde lista de prefabs.
        return GenerateEncounterFromList(enemyPrefabs);
    }

    private EncounterData GenerateEncounterFromList(List<EnemyRuntime> enemies)
    {
        if (enemies == null || enemies.Count == 0)
        {
            Debug.LogWarning("[BattleTestBootstrap] Lista de enemigos vacía para generar EncounterData.");
            return null;
        }

        var encounter = ScriptableObject.CreateInstance<EncounterData>();

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            encounter.EnemiesToSpawn.Add(enemy);
        }

        FillSpawnPoints(encounter, encounter.EnemiesToSpawn.Count, isEnemy:true);

        Debug.Log($"[BattleTestBootstrap] EncounterData runtime generado con {encounter.EnemiesToSpawn.Count} enemigos y {encounter.SpawnPoints.Count} spawn points.");
        return encounter;
    }

    private void FillSpawnPoints(EncounterData data, int required, bool preserveExisting = false, bool isEnemy = false)
    {
        // Ensure list exists (EncounterData getter guarantees non-null)
        var sp = data.SpawnPoints;
        if (!preserveExisting)
            sp.Clear();

        // 1) Si hay anchors, usarlos (hasta required)
        int added = 0;
        if (enemySpawnAnchors != null && enemySpawnAnchors.Count > 0)
        {
            foreach (var a in enemySpawnAnchors)
            {
                if (a == null) continue;
                sp.Add(a.position);
                added++;
                if (added >= required) break;
            }
        }

        // 2) Si faltan puntos, generar formación (iso o lineal)
        if (useIsometricLayout)
        {
            var origin = isEnemy ? isoEnemyOrigin : isoPartyOrigin;
            Vector3 right, forward;
            GetBasis(out right, out forward);
            if (isEnemy) right = -right; // enemigos a la izquierda visual
            var generated = GenerateIsometricPositions(required - added, origin, isoSpacingX, isoSpacingY, isoRows, right, forward);
            if (lockYToZero)
            {
                for (int i = 0; i < generated.Count; i++)
                {
                    var p = generated[i]; p.y = 0f; generated[i] = p;
                }
            }
            sp.AddRange(generated);
        }
        else
        {
            float spacing = 2.0f;
            Vector3 start = Vector3.zero;
            for (int i = added; i < required; i++)
            {
                sp.Add(start + new Vector3(i * spacing, 0f, 0f));
            }
        }
    }

    private List<Vector3> GenerateIsometricPositions(int count, Vector3 origin, float spacingX, float spacingY, int rows, Vector3 basisRight, Vector3 basisForward)
    {
        var list = new List<Vector3>();
        if (count <= 0) return list;
        int perRow = Mathf.Max(1, Mathf.CeilToInt(count / (float)rows));
        int generated = 0;
        for (int r = 0; r < rows && generated < count; r++)
        {
            for (int c = 0; c < perRow && generated < count; c++)
            {
                float offsetX = (c * spacingX + r * (spacingX * 0.5f));
                float offsetZ = -(r * spacingY) + (c * (spacingY * 0.15f));
                Vector3 pos = origin + basisRight.normalized * offsetX + basisForward.normalized * offsetZ;
                list.Add(pos);
                generated++;
            }
        }
        return list;
    }

    private void ApplyPartyPositions(List<CharacterRuntime> party)
    {
        if (party == null || party.Count == 0) return;
        int idx = 0;
        // 1) Anchors si están
        if (partySpawnAnchors != null && partySpawnAnchors.Count > 0)
        {
            foreach (var c in party)
            {
                if (c == null) continue;
                if (idx < partySpawnAnchors.Count && partySpawnAnchors[idx] != null)
                    c.transform.position = partySpawnAnchors[idx].position;
                idx++;
            }
            return;
        }

        // 2) Isometric fallback
        if (useIsometricLayout)
        {
            Vector3 right, forward; GetBasis(out right, out forward);
            var pos = GenerateIsometricPositions(party.Count, isoPartyOrigin, isoSpacingX, isoSpacingY, isoRows, right, forward);
            for (int i = 0; i < party.Count && i < pos.Count; i++)
            {
                var c = party[i];
                if (c != null)
                {
                    var p = pos[i];
                    if (lockYToZero) p.y = 0f;
                    c.transform.position = p;
                }
            }
        }
    }

    private void GetBasis(out Vector3 right, out Vector3 forward)
    {
        var cam = layoutCamera != null ? layoutCamera : Camera.main;
        if (alignToCamera && cam != null)
        {
            right = cam.transform.right; right.y = 0f; right.Normalize();
            forward = cam.transform.forward; forward.y = 0f; forward.Normalize();
        }
        else
        {
            right = Vector3.right;
            forward = Vector3.forward;
        }
    }
}
