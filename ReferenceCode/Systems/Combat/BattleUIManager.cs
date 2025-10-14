using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

/// <summary>
/// Maneja la interacción del usuario con el combate (comandos, selección de objetivos).
/// </summary>
public class BattleUIManager : MonoBehaviour
{
    [System.Serializable]
    public class ActionSelectionEvent : UnityEvent<CharacterRuntime, ActionData, CharacterRuntime> { }

    [System.Serializable]
    public class TargetRequestEvent : UnityEvent<CharacterRuntime> { }

    [Header("Acciones base disponibles")]
    [SerializeField] private List<ActionData> defaultActions = new List<ActionData>();

    [Header("UI Containers (opcional)")]
    [SerializeField] private RectTransform actionButtonsRoot;
    [SerializeField] private RectTransform targetButtonsRoot;

    [Header("Prefabs (opcional)")]
    [SerializeField] private GameObject actionButtonPrefab; // Button + texto
    [SerializeField] private GameObject targetButtonPrefab; // Button + texto

    [Header("Comportamiento")]
    [SerializeField] private bool autoSelectIfNoUI = true;
    [SerializeField, Min(0f)] private float autoSelectDelay = 0f;
    [SerializeField] private bool buildRuntimeUIIfMissing = true;

    [Header("Acción seleccionada - colores")]
    [SerializeField] private Color actionNormalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color actionSelectedColor = new Color(0.6f, 0.85f, 1f, 1f);

    [Header("Eventos UI")]
    [SerializeField] private ActionSelectionEvent onActionSelected = new ActionSelectionEvent();
    [SerializeField] private TargetRequestEvent onTargetSelectionRequested = new TargetRequestEvent();

    public List<ActionData> DefaultActions => defaultActions;
    public ActionSelectionEvent OnActionSelected => onActionSelected;

    // Acción seleccionada por el usuario (flujo interactivo)
    private ActionData selectedAction;
    private readonly System.Collections.Generic.List<UnityEngine.UI.Button> actionButtons = new System.Collections.Generic.List<UnityEngine.UI.Button>();
    private UnityEngine.UI.Button selectedActionButton;

    public void ShowCommandPanel(CharacterRuntime actor, List<ActionData> availableActions)
    {
        if (actor == null)
        {
            Debug.LogWarning("[BattleUIManager] Actor nulo al intentar mostrar comandos.");
            return;
        }

        // TODO: Integrar con UI real (botones, highlights, target selector).
        Debug.Log("[BattleUIManager] Mostrar menú de comandos para " + actor.Archetype.characterName);

        if (availableActions == null || availableActions.Count == 0)
        {
            Debug.LogWarning("[BattleUIManager] Sin acciones disponibles, se auto-asignará la primera por defecto.");
            availableActions = defaultActions;
        }

        if (availableActions.Count == 0)
        {
            Debug.LogError("[BattleUIManager] No hay acciones configuradas en defaultActions.");
            return;
        }

        // Placeholder: ejecuta automáticamente la primera acción contra un objetivo por defecto (delegado en BattleManager),
        // pero lo hace en el siguiente frame para evitar recursión directa y StackOverflow.
        ActionData chosen = availableActions[0];
        Debug.Log("[BattleUIManager] (Placeholder) Seleccionando acción por defecto: " + chosen.ActionName);

        StartCoroutine(AutoInvokeNextFrame(actor, chosen));
    }

    private System.Collections.IEnumerator AutoInvokeNextFrame(CharacterRuntime actor, ActionData chosen)
    {
        yield return null; // espera un frame para evitar recursión inmediata
        onActionSelected.Invoke(actor, chosen, null);
    }

    public void RequestTargetSelection(CharacterRuntime actor)
    {
        Debug.Log("[BattleUIManager] Solicitando selección de objetivo para " + actor.Archetype.characterName);
        onTargetSelectionRequested.Invoke(actor);
    }

    // Construye botones de acción y targets si hay contenedores/prefabs configurados.
    // Devuelve true si se mostró UI interactiva; de lo contrario, false para que el caller haga fallback.
    public bool ShowInteractivePanels(CharacterRuntime actor, List<ActionData> actions, List<CharacterRuntime> possibleTargets)
    {
        if ((actionButtonsRoot == null || targetButtonsRoot == null) && buildRuntimeUIIfMissing)
            EnsureRuntimeUI();

        bool actionsReady = actionButtonsRoot != null;
        bool targetsReady = targetButtonsRoot != null;
        if (!actionsReady || !targetsReady)
            return false;

        selectedAction = null;

        // Acciones
        ClearContainer(actionButtonsRoot);
        actionButtons.Clear();
        foreach (var action in actions)
        {
            if (action == null) continue;
            var go = CreateUIButton(actionButtonsRoot, actionButtonPrefab, action.ActionName);
            var btn = go.GetComponent<UnityEngine.UI.Button>();
            btn.onClick.AddListener(() =>
            {
                selectedAction = action;
                Debug.Log("[BattleUIManager] Acción elegida: " + action.ActionName + " (lista para seleccionar objetivo)");
            });
        }

        // Targets
        ClearContainer(targetButtonsRoot);
        foreach (var target in possibleTargets)
        {
            if (target == null) continue;
            var go = CreateUIButton(targetButtonsRoot, targetButtonPrefab, target.Archetype.characterName);
            var btn = go.GetComponent<UnityEngine.UI.Button>();
            var st = target.GetComponent<CombatantState>();
            if (st != null && !st.IsAlive) btn.interactable = false;
            btn.onClick.AddListener(() =>
            {
                var actionToUse = selectedAction;
                if (actionToUse == null)
                    actionToUse = (actions != null && actions.Count > 0) ? actions[0] : (defaultActions.Count > 0 ? defaultActions[0] : null);
                if (actionToUse == null)
                {
                    Debug.LogWarning("[BattleUIManager] No hay acción para ejecutar.");
                    return;
                }
                onActionSelected.Invoke(actor, actionToUse, target);
            });
        }

        return true;
    }

    private void EnsureRuntimeUI()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var cgo = new GameObject("BattleUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
        }

        if (actionButtonsRoot == null)
        {
            var panel = new GameObject("ActionsPanel", typeof(RectTransform), typeof(Image));
            var rt = panel.GetComponent<RectTransform>();
            panel.transform.SetParent(canvas.transform, false);
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(20f, 20f);
            rt.sizeDelta = new Vector2(260f, 300f);
            actionButtonsRoot = rt;
            var img = panel.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.2f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 8, 8);
        }

        if (targetButtonsRoot == null)
        {
            var panel = new GameObject("TargetsPanel", typeof(RectTransform), typeof(Image));
            var rt = panel.GetComponent<RectTransform>();
            panel.transform.SetParent(canvas.transform, false);
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 20f);
            rt.sizeDelta = new Vector2(260f, 300f);
            targetButtonsRoot = rt;
            var img = panel.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.2f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 8, 8);
        }
    }

    private void ClearContainer(RectTransform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private GameObject CreateUIButton(RectTransform parent, GameObject prefab, string label)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, parent);
        }
        else
        {
            go = new GameObject("UI_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(160, 40);

            var textGO = new GameObject("Label");
            var trt = textGO.AddComponent<RectTransform>();
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
            var tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 20f;
#else
            var txt = textGO.AddComponent<UnityEngine.UI.Text>();
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 18;
#endif
        }

#if TMP_PRESENT || UNITY_TEXTMESHPRO
        var foundTMP = go.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (foundTMP != null) foundTMP.text = label;
#endif
        var foundText = go.GetComponentInChildren<UnityEngine.UI.Text>();
        if (foundText != null) foundText.text = label;

        var layout = go.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layout == null) layout = go.AddComponent<UnityEngine.UI.LayoutElement>();
        layout.minHeight = 40f;
        layout.preferredHeight = 40f;
        layout.minWidth = 150f;

        var img = go.GetComponent<UnityEngine.UI.Image>();
        if (img != null && img.color.a <= 0.01f)
            img.color = new Color(0.2f, 0.25f, 0.3f, 0.85f);
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (foundTMP != null && foundTMP.color.a <= 0.01f)
            foundTMP.color = Color.white;
#endif
        if (foundText != null && foundText.color.a <= 0.01f)
            foundText.color = Color.white;

        return go;
    }

    private bool HasUIButtonComponents(RectTransform root)
    {
        if (root == null) return false;
        return root.GetComponentInChildren<UnityEngine.UI.Button>() != null;
    }
}





