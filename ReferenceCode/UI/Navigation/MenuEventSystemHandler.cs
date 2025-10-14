using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;

[DefaultExecutionOrder(-100)]
public class MenuEventSystemHandler : MonoBehaviour
{
    [Header("Selectables del panel")]
    [Tooltip("Todos los botones, toggles o sliders dentro de este menu")]
    public List<Selectable> selectables = new List<Selectable>();

    [Header("Animacion de seleccion")]
    public bool enableGrow = true;
    public float animationScale = 1.1f;
    public float animationDuration = 0.15f;
    public Ease tweenEase = Ease.OutQuad;

    [Header("Sonido (opcional)")]
    public UnityEvent onSelectSound;

    [Header("Configuracion de input")]
    [Tooltip("Primer boton que se seleccionara al abrir el menu")]
    public GameObject firstSelected;
    [Tooltip("Referencia al input action Navigate (desde tu Input Actions Asset)")]
    public InputActionReference navigateAction;

    [Header("Exclusiones de animacion (sliders, dropdowns, etc.)")]
    public List<Selectable> animationExclusions = new List<Selectable>();

    protected readonly Dictionary<Selectable, Vector3> originalScales = new Dictionary<Selectable, Vector3>();

    protected EventSystem eventSystem;
    protected GameObject lastSelected;

    private Coroutine selectFirstCoroutine;
    private InputAction cachedNavigateAction;
    private bool navigateActionEnabledByHandler;

    protected virtual void Awake()
    {
        eventSystem = EventSystem.current;

        if (selectables.Count == 0)
        {
            selectables.AddRange(GetComponentsInChildren<Selectable>(true));
        }

        foreach (Selectable sel in selectables)
        {
            if (!originalScales.ContainsKey(sel))
                originalScales.Add(sel, sel.transform.localScale);

            AddSelectionListeners(sel);
        }

        if (navigateAction != null)
            cachedNavigateAction = navigateAction.action;
    }

    protected virtual void OnEnable()
    {
        foreach (var kvp in originalScales)
            kvp.Key.transform.localScale = kvp.Value;

        if (selectFirstCoroutine != null)
            StopCoroutine(selectFirstCoroutine);
        selectFirstCoroutine = StartCoroutine(SelectFirstAfterFrame());

        if (cachedNavigateAction != null)
        {
            if (!cachedNavigateAction.enabled)
            {
                cachedNavigateAction.Enable();
                navigateActionEnabledByHandler = true;
            }
            cachedNavigateAction.performed += OnNavigate;
        }
    }

    protected virtual void OnDisable()
    {
        if (selectFirstCoroutine != null)
        {
            StopCoroutine(selectFirstCoroutine);
            selectFirstCoroutine = null;
        }

        foreach (var sel in selectables)
            sel.transform.DOKill();

        if (cachedNavigateAction != null)
        {
            cachedNavigateAction.performed -= OnNavigate;
            if (navigateActionEnabledByHandler)
            {
                cachedNavigateAction.Disable();
                navigateActionEnabledByHandler = false;
            }
        }
    }

    protected virtual IEnumerator SelectFirstAfterFrame()
    {
        yield return null;

        if (firstSelected != null && eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(firstSelected);
            lastSelected = firstSelected;
        }

        selectFirstCoroutine = null;
    }

    protected virtual void OnNavigate(InputAction.CallbackContext ctx)
    {
        if (eventSystem == null)
            return;

        if (eventSystem.currentSelectedGameObject == null && lastSelected != null)
        {
            eventSystem.SetSelectedGameObject(lastSelected);
        }
    }

    protected virtual void AddSelectionListeners(Selectable sel)
    {
        if (sel == null)
            return;

        var trigger = sel.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = sel.gameObject.AddComponent<EventTrigger>();

        trigger.triggers ??= new List<EventTrigger.Entry>();

        AddTrigger(trigger, EventTriggerType.Select, _ => OnSelect(sel));
        AddTrigger(trigger, EventTriggerType.Deselect, _ => OnDeselect(sel));
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ => OnPointerEnter(sel));
        AddTrigger(trigger, EventTriggerType.PointerExit, _ => OnPointerExit(sel));
    }

    protected virtual void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    protected virtual void OnSelect(Selectable sel)
    {
        if (sel == null)
            return;

        if (animationExclusions.Contains(sel))
            return;

        onSelectSound?.Invoke();
        lastSelected = sel.gameObject;

        if (!enableGrow)
            return;

        sel.transform.DOKill();
        Vector3 newScale = originalScales[sel] * animationScale;
        sel.transform.DOScale(newScale, animationDuration).SetEase(tweenEase);
    }

    protected virtual void OnDeselect(Selectable sel)
    {
        if (sel == null)
            return;

        if (animationExclusions.Contains(sel))
            return;

        if (!enableGrow)
            return;

        sel.transform.DOKill();
        sel.transform.DOScale(originalScales[sel], animationDuration).SetEase(tweenEase);
    }

    protected virtual void OnPointerEnter(Selectable sel)
    {
        if (sel == null || eventSystem == null)
            return;

        if (eventSystem.currentSelectedGameObject != sel.gameObject)
        {
            eventSystem.SetSelectedGameObject(sel.gameObject);
        }
    }

    protected virtual void OnPointerExit(Selectable sel)
    {
        // EventSystem maneja el deselect automaticamente
    }
}

