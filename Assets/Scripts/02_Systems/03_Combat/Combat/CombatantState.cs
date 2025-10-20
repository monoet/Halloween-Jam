using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Estado de combate por entidad (HP runtime, banderas de vida).
/// Mantiene separado el runtime del combate de CharacterRuntime.
/// </summary>
public class CombatantState : MonoBehaviour
{
    [Header("Stat Runtime Source")]
    [SerializeField] private CharacterRuntime characterRuntime;
    [SerializeField] private bool autoInitializeFromRuntime = true;
    [SerializeField] private bool preserveFractionOnRuntimeUpdate = true;

    [Header("HP Runtime")]
    [SerializeField] private int maxHP;
    [SerializeField] private int currentHP;

    [Header("SP Runtime")]
    [SerializeField] private int maxSP;
    [SerializeField] private int currentSP;

    [Header("Defaults si el Archetype no provee HP")]
    [SerializeField] private int fallbackMaxHP = 10;

    [Header("Combo Points (CP)")]
    [SerializeField, Range(0, 10)] private int maxCP = 5;
    [SerializeField, Range(0, 10)] private int currentCP = 0;

    [Header("Eventos")]
    public UnityEvent OnVitalsChanged = new UnityEvent();

    [Header("Debug Options")]
    [SerializeField] private bool enableDebugLogs;

    private bool initialized;
    private UnityAction runtimeStatsListener;
    private string displayName;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public int MaxSP => maxSP;
    public int CurrentSP => currentSP;
    public bool IsAlive => currentHP > 0;
    public bool IsDead() => !IsAlive;
    public int MaxCP => maxCP;
    public int CurrentCP => currentCP;
    public bool Initialized => initialized;
    public CharacterRuntime CharacterRuntime => characterRuntime;
    public FinalStats FinalStats => characterRuntime != null ? characterRuntime.Final : default;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Portrait => characterRuntime?.Core.portrait ?? characterRuntime?.Archetype?.portrait;

    private void Awake()
    {
        runtimeStatsListener = HandleRuntimeStatsChanged;

        if (characterRuntime == null)
        {
            characterRuntime = GetComponent<CharacterRuntime>();
        }

        AttachRuntime(characterRuntime);

        if (autoInitializeFromRuntime && characterRuntime != null)
        {
            InitializeFrom(characterRuntime, preserveCurrentFraction: false);
        }
    }

    private void OnEnable()
    {
        SubscribeRuntime();
    }

    private void OnDisable()
    {
        UnsubscribeRuntime();
    }

    public void InitializeFrom(CharacterRuntime character, bool preserveCurrentFraction = false)
    {
        AttachRuntime(character);

        int providedHP = character != null ? character.Final.HP : 0;
        int providedSP = character != null ? character.Final.SP : 0;

        int targetMaxHp = providedHP > 0 ? providedHP : fallbackMaxHP;
        int targetMaxSp = Mathf.Max(0, providedSP);

        if (preserveCurrentFraction && initialized && maxHP > 0)
        {
            float hpFraction = Mathf.Clamp01(currentHP / (float)maxHP);
            maxHP = targetMaxHp;
            currentHP = Mathf.RoundToInt(hpFraction * maxHP);
        }
        else
        {
            maxHP = targetMaxHp;
            currentHP = maxHP;
        }

        if (preserveCurrentFraction && initialized && maxSP > 0)
        {
            float spFraction = Mathf.Clamp01(currentSP / (float)maxSP);
            maxSP = targetMaxSp;
            currentSP = Mathf.RoundToInt(spFraction * maxSP);
        }
        else
        {
            maxSP = targetMaxSp;
            currentSP = maxSP;
        }

        initialized = true;
        Log($"Inicializado HP: {currentHP}/{maxHP} en {DisplayName}");
        OnVitalsChanged.Invoke();
    }

    public void EnsureInitialized(CharacterRuntime character)
    {
        if (!initialized)
        {
            InitializeFrom(character, preserveCurrentFraction: false);
            return;
        }

        AttachRuntime(character);
    }

    public void SetCharacterRuntime(CharacterRuntime runtime, bool initialize = true, bool preserveVitals = false)
    {
        AttachRuntime(runtime);

        if (initialize)
        {
            InitializeFrom(runtime, preserveVitals);
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount < 0)
        {
            amount = 0;
        }

        if (!initialized)
        {
            LogWarning($"{DisplayName} recibe dano sin estar inicializado. Forzando inicializacion con fallback.");
            InitializeFrom(null);
        }

        currentHP = Mathf.Max(0, currentHP - amount);
        Log($"{DisplayName} recibe {amount} dano. HP: {currentHP}/{maxHP}");

        if (currentHP == 0)
        {
            Log($"{DisplayName} ha caido.");
        }

        OnVitalsChanged.Invoke();
    }

    public void Heal(int amount)
    {
        if (amount < 0)
        {
            amount = 0;
        }

        if (!initialized)
        {
            LogWarning($"{DisplayName} intenta curarse sin estar inicializado. Forzando inicializacion con fallback.");
            InitializeFrom(null);
        }

        currentHP = Mathf.Min(maxHP, currentHP + amount);
        Log($"{DisplayName} cura {amount}. HP: {currentHP}/{maxHP}");
        OnVitalsChanged.Invoke();
    }

    public bool SpendCP(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (currentCP < amount)
        {
            LogWarning($"{DisplayName} no tiene CP suficientes ({currentCP}/{amount}).");
            return false;
        }

        currentCP -= amount;
        Log($"{DisplayName} gasta {amount} CP. CP: {currentCP}/{maxCP}");
        OnVitalsChanged.Invoke();
        return true;
    }

    public void AddCP(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int before = currentCP;
        currentCP = Mathf.Min(maxCP, currentCP + amount);
        int gained = currentCP - before;

        if (gained > 0)
        {
            Log($"{DisplayName} gana {gained} CP. CP: {currentCP}/{maxCP}");
            OnVitalsChanged.Invoke();
        }
    }

    public bool SpendSP(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (currentSP < amount)
        {
            LogWarning($"{DisplayName} no tiene SP suficientes ({currentSP}/{amount}).");
            return false;
        }

        currentSP -= amount;
        Log($"{DisplayName} gasta {amount} SP. SP: {currentSP}/{maxSP}");
        OnVitalsChanged.Invoke();
        return true;
    }

    public void RestoreSP(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int before = currentSP;
        currentSP = Mathf.Min(maxSP, currentSP + amount);
        int gained = currentSP - before;

        if (gained > 0)
        {
            Log($"{DisplayName} recupera {gained} SP. SP: {currentSP}/{maxSP}");
            OnVitalsChanged.Invoke();
        }
    }

    private void AttachRuntime(CharacterRuntime runtime)
    {
        runtimeStatsListener ??= HandleRuntimeStatsChanged;

        if (ReferenceEquals(characterRuntime, runtime))
        {
            RefreshMetadataFromRuntime();
            return;
        }

        UnsubscribeRuntime();
        characterRuntime = runtime;
        RefreshMetadataFromRuntime();
        SubscribeRuntime();
    }

    private void SubscribeRuntime()
    {
        if (characterRuntime == null || runtimeStatsListener == null)
        {
            return;
        }

        if (characterRuntime.OnStatsChanged == null)
        {
            characterRuntime.OnStatsChanged = new UnityEvent();
        }

        characterRuntime.OnStatsChanged.RemoveListener(runtimeStatsListener);
        characterRuntime.OnStatsChanged.AddListener(runtimeStatsListener);
    }

    private void UnsubscribeRuntime()
    {
        if (characterRuntime == null || runtimeStatsListener == null)
        {
            return;
        }

        if (characterRuntime.OnStatsChanged != null)
        {
            characterRuntime.OnStatsChanged.RemoveListener(runtimeStatsListener);
        }
    }

    private void HandleRuntimeStatsChanged()
    {
        if (characterRuntime == null)
        {
            return;
        }

        InitializeFrom(characterRuntime, preserveFractionOnRuntimeUpdate);
    }

    private void RefreshMetadataFromRuntime()
    {
        if (characterRuntime == null)
        {
            displayName = gameObject.name;
            return;
        }

        var runtimeName = characterRuntime.Core.characterName;
        if (!string.IsNullOrWhiteSpace(runtimeName))
        {
            displayName = runtimeName;
            return;
        }

        if (characterRuntime.Archetype != null && !string.IsNullOrWhiteSpace(characterRuntime.Archetype.characterName))
        {
            displayName = characterRuntime.Archetype.characterName;
            return;
        }

        displayName = gameObject.name;
    }

    private void Log(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CombatantState] {message}");
        }
    }

    private void LogWarning(string message)
    {
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[CombatantState] {message}");
        }
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying || !gameObject.activeInHierarchy)
        {
            return;
        }

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = Color.cyan }
        };

        float offsetY = 10 + (transform.GetSiblingIndex() * 100);
        GUILayout.BeginArea(new Rect(10, offsetY, 250, 100), GUI.skin.box);
        GUILayout.Label($"<b>{DisplayName}</b>", style);
        GUILayout.Label($"HP: {currentHP}/{maxHP}", style);
        GUILayout.Label($"SP: {currentSP}/{maxSP}", style);
        GUILayout.Label($"CP: {currentCP}/{maxCP}", style);
        GUILayout.EndArea();
    }
#endif
}
