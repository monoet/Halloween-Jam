using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Controla la tarjeta visual de un personaje en el panel de Party.
/// Obtiene todos los datos (nombre, retrato, stats) desde CharacterRuntime.
/// </summary>
public class CharacterSlotUI : MonoBehaviour
{
    [Header("UI References (componentes del prefab)")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI spText;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image spFill;
    [Header("Animación de barras")]
    [SerializeField, Min(0f)] private float barAnimDuration = 0.25f;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color healFlashColor = new Color(0.4f, 1f, 0.6f, 1f);
    [SerializeField] private Image highlightFrame;
    [SerializeField] private Button button;

    [Header("Highlight Colors")]
    [SerializeField] private Color activeColor = new Color(0.4f, 1f, 0.6f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
    [SerializeField] private Color hoverColor = new Color(0.85f, 0.95f, 1f, 1f);

    private CharacterRuntime runtime;
    private CombatantState combatant;
    private int lastHP;
    private int lastSP;
    private PartyPanelSelector selector;
    private OverviewMenu.MenuMode currentMode;
    private bool selectable;

    private RectTransform rect;
    private Color basePortraitColor;
    private Vector2 baseAnchoredPos;
    private Tween hoverTween;

    public bool IsActive { get; private set; }

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        if (portraitImage != null)
            basePortraitColor = portraitImage.color;
        baseAnchoredPos = rect.anchoredPosition;
    }

    public void Init(CharacterRuntime character)
    {
        runtime = character;

        if (runtime == null)
        {
            Debug.LogWarning("[CharacterSlotUI] Init sin CharacterRuntime.");
            return;
        }

        runtime.OnStatsChanged.AddListener(Refresh);
        combatant = runtime.GetComponent<CombatantState>();
        if (combatant != null)
            combatant.OnVitalsChanged.AddListener(Refresh);

        lastHP = combatant != null ? combatant.CurrentHP : runtime.Final.HP;
        lastSP = combatant != null ? combatant.CurrentSP : runtime.Final.SP;
        selector = GetComponentInParent<PartyPanelSelector>();

        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            var colors = button.colors;
            colors.highlightedColor = hoverColor;
            colors.pressedColor = new Color(0.7f, 0.8f, 1f, 1f);
            button.colors = colors;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (runtime == null)
            return;

        var archetype = runtime.Archetype;

        if (archetype != null)
        {
            if (nameText != null)
                nameText.text = archetype.characterName;

            if (portraitImage != null && archetype.portrait != null)
                portraitImage.sprite = archetype.portrait;
        }
        else if (nameText != null)
        {
            nameText.text = "???";
        }

        if (levelText != null)
            levelText.text = "Lv " + runtime.Core.Level;

        int curHP = combatant != null ? combatant.CurrentHP : runtime.Final.HP;
        int maxHP = combatant != null ? combatant.MaxHP : runtime.Final.HP;
        int curSP = combatant != null ? combatant.CurrentSP : runtime.Final.SP;
        int maxSP = combatant != null ? combatant.MaxSP : runtime.Final.SP;

        if (hpText != null)
            hpText.text = $"HP {curHP}/{maxHP}";
        if (spText != null)
            spText.text = $"SP {curSP}/{maxSP}";

        float targetHpFill = maxHP > 0 ? Mathf.Clamp01((float)curHP / maxHP) : 0f;
        float targetSpFill = maxSP > 0 ? Mathf.Clamp01((float)curSP / maxSP) : 0f;

        if (hpFill != null)
        {
            hpFill.DOKill();
            hpFill.DOFillAmount(targetHpFill, barAnimDuration).SetEase(Ease.OutQuad);
        }
        if (spFill != null)
        {
            spFill.DOKill();
            spFill.DOFillAmount(targetSpFill, barAnimDuration).SetEase(Ease.OutQuad);
        }

        // Pequeño flash de color en el texto ante daño/curación
        if (hpText != null && curHP != lastHP)
        {
            var flash = curHP < lastHP ? damageFlashColor : healFlashColor;
            var original = hpText.color;
            hpText.DOKill();
            hpText.DOColor(flash, 0.08f).OnComplete(() => hpText.DOColor(original, 0.2f));
        }
        if (spText != null && curSP != lastSP)
        {
            var flash = curSP < lastSP ? damageFlashColor : healFlashColor;
            var original = spText.color;
            spText.DOKill();
            spText.DOColor(flash, 0.08f).OnComplete(() => spText.DOColor(original, 0.2f));
        }

        lastHP = curHP;
        lastSP = curSP;
    }

    public void SetActiveVisual(bool state)
    {
        IsActive = state;
        if (highlightFrame != null)
            highlightFrame.color = state ? activeColor : inactiveColor;
    }

    public void SetSelectable(bool value, OverviewMenu.MenuMode mode)
    {
        selectable = value;
        currentMode = mode;

        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
            return;

        button.interactable = true;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnButtonPressed);

        if (highlightFrame != null)
            highlightFrame.color = activeColor;
    }

    private void OnButtonPressed()
    {
        if (selector == null || runtime == null)
            return;

        if (selector.IsLocked() || !selectable)
        {
            Debug.Log("[CharacterSlotUI] Click ignorado (panel bloqueado o slot no activo).");
            return;
        }

        Debug.Log("[CharacterSlotUI] Seleccionado " + runtime.Archetype.characterName + " para " + currentMode);
        selector.OnSelectCharacter(runtime, currentMode);
    }

    public void AnimateHover(bool state)
    {
        if (selector != null && selector.IsLocked())
            return;
        if (!selectable)
            return;

        hoverTween?.Kill();

        const float offset = 10f;
        const float duration = 0.25f;

        if (state)
        {
            rect.DOAnchorPosX(baseAnchoredPos.x - offset, duration).SetEase(Ease.OutQuad);
            if (portraitImage != null)
                portraitImage.DOColor(new Color(1.05f, 1.05f, 1.05f, 1f), duration).SetEase(Ease.OutSine);
        }
        else
        {
            rect.DOAnchorPosX(baseAnchoredPos.x, duration).SetEase(Ease.InQuad);
            if (portraitImage != null)
                portraitImage.DOColor(basePortraitColor, duration).SetEase(Ease.InSine);
        }
    }

    private void OnDestroy()
    {
        if (runtime != null)
            runtime.OnStatsChanged.RemoveListener(Refresh);
        if (combatant != null)
            combatant.OnVitalsChanged.RemoveListener(Refresh);
    }

    public bool IsActiveSlot => true;
}

