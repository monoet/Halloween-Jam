using UnityEngine;

/// <summary>
/// Estado de combate por entidad (HP runtime, banderas de vida).
/// Mantiene separado el runtime del combate de CharacterRuntime.
/// </summary>
public class CombatantState : MonoBehaviour
{
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
    public UnityEngine.Events.UnityEvent OnVitalsChanged = new UnityEngine.Events.UnityEvent();

    [Header("Debug Options")]
    [SerializeField] private bool enableDebugLogs = false; // ✅ toggle global

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;
    public int MaxSP => maxSP;
    public int CurrentSP => currentSP;
    public bool IsAlive => currentHP > 0;
    public int MaxCP => maxCP;
    public int CurrentCP => currentCP;
    public bool Initialized => initialized;

    private bool initialized;

    public void InitializeFrom(CharacterRuntime character, bool preserveCurrentFraction = false)
    {
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
        Log($"Inicializado HP: {currentHP}/{maxHP} en {name}");
        OnVitalsChanged.Invoke();
    }

    public void EnsureInitialized(CharacterRuntime character)
    {
        if (initialized)
            return;

        InitializeFrom(character, preserveCurrentFraction: false);
    }

    public void TakeDamage(int amount)
    {
        if (amount < 0) amount = 0;
        if (!initialized)
        {
            LogWarning($"{name} recibe daño sin estar inicializado. Forzando inicialización con fallback.");
            InitializeFrom(null);
        }

        currentHP = Mathf.Max(0, currentHP - amount);
        Log($"{name} recibe {amount} daño. HP: {currentHP}/{maxHP}");

        if (currentHP == 0)
            Log($"{name} ha caído.");

        OnVitalsChanged.Invoke();
    }

    public void Heal(int amount)
    {
        if (amount < 0) amount = 0;
        if (!initialized)
        {
            LogWarning($"{name} intenta curarse sin estar inicializado. Forzando inicialización con fallback.");
            InitializeFrom(null);
        }

        currentHP = Mathf.Min(maxHP, currentHP + amount);
        Log($"{name} cura {amount}. HP: {currentHP}/{maxHP}");
        OnVitalsChanged.Invoke();
    }

    public bool SpendCP(int amount)
    {
        if (amount <= 0) return true;
        if (currentCP < amount)
        {
            LogWarning($"{name} no tiene CP suficientes ({currentCP}/{amount}).");
            return false;
        }

        currentCP -= amount;
        Log($"{name} gasta {amount} CP. CP: {currentCP}/{maxCP}");
        OnVitalsChanged.Invoke();
        return true;
    }

    public void AddCP(int amount)
    {
        if (amount <= 0) return;
        int before = currentCP;
        currentCP = Mathf.Min(maxCP, currentCP + amount);
        int gained = currentCP - before;

        if (gained > 0)
        {
            Log($"{name} gana {gained} CP. CP: {currentCP}/{maxCP}");
            OnVitalsChanged.Invoke();
        }
    }

    public bool SpendSP(int amount)
    {
        if (amount <= 0) return true;
        if (currentSP < amount)
        {
            LogWarning($"{name} no tiene SP suficientes ({currentSP}/{amount}).");
            return false;
        }

        currentSP -= amount;
        Log($"{name} gasta {amount} SP. SP: {currentSP}/{maxSP}");
        OnVitalsChanged.Invoke();
        return true;
    }

    public void RestoreSP(int amount)
    {
        if (amount <= 0) return;
        int before = currentSP;
        currentSP = Mathf.Min(maxSP, currentSP + amount);
        int gained = currentSP - before;

        if (gained > 0)
        {
            Log($"{name} recupera {gained} SP. SP: {currentSP}/{maxSP}");
            OnVitalsChanged.Invoke();
        }
    }

    // 🔹 Métodos auxiliares de logging -----------------------------
    private void Log(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[CombatantState] {message}");
    }

    private void LogWarning(string message)
    {
        if (enableDebugLogs)
            Debug.LogWarning($"[CombatantState] {message}");
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying || !gameObject.activeInHierarchy)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = Color.cyan }
        };

        float offsetY = 10 + (transform.GetSiblingIndex() * 100);
        GUILayout.BeginArea(new Rect(10, offsetY, 250, 100), GUI.skin.box);
        GUILayout.Label($"<b>{name}</b>", style);
        GUILayout.Label($"HP: {currentHP}/{maxHP}", style);
        GUILayout.Label($"SP: {currentSP}/{maxSP}", style);
        GUILayout.Label($"CP: {currentCP}/{maxCP}", style);
        GUILayout.EndArea();
    }
#endif
}
