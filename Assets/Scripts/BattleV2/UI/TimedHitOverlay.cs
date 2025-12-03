using System;
using System.Collections;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using BattleV2.AnimationSystem.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.UI
{
    public class TimedHitOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AnimationSystemInstaller installer;
        [SerializeField, Min(0f)] private float closeHoldSeconds = 0.25f;

        [Header("UI References")]
        [SerializeField] private RectTransform container;
        [SerializeField] private RectTransform timelineBar;
        [SerializeField] private RectTransform cursor;
        [SerializeField] private RectTransform successZone;
        [SerializeField] private Text resultText;
        
        [Header("Prefabs (Optional)")]
        [SerializeField] private GameObject timelineBarPrefab;
        [SerializeField] private GameObject cursorPrefab;
        [SerializeField] private GameObject successZonePrefab;

        private IAnimationEventBus eventBus;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        
        private GameObject timelineBarInstance;
        private RectTransform timelineBarRect;
        
        private GameObject cursorInstance;
        private RectTransform cursorRect;
        
        private GameObject successZoneInstance;
        private RectTransform successZoneRect;

        private Coroutine activeSequenceRoutine;

        [Header("Slider Integration")]
        [SerializeField] private Slider progressSlider;

        private void Awake()
        {
            container ??= GetComponent<RectTransform>();
            if (progressSlider == null)
            {
                progressSlider = GetComponentInChildren<Slider>(true);
            }
            TryInitialize();
        }

        private void OnEnable()
        {
            TryInitialize();
        }

        public void Initialize(IAnimationEventBus bus)
        {
            if (bus == null) return;

            Cleanup();

            eventBus = bus;
            subscriptions.Add(eventBus.Subscribe<AnimationWindowEvent>(OnWindowEvent));
            subscriptions.Add(eventBus.Subscribe<TimedHitResultEvent>(OnResultEvent));
            Debug.Log($"[TimedHitOverlay] Initialized with bus Hash: {bus.GetHashCode()}");
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void OnDisable()
        {
            StopActiveSequence();
        }

        private void Cleanup()
        {
            foreach (var sub in subscriptions)
            {
                sub.Dispose();
            }
            subscriptions.Clear();
        }

        private void OnWindowEvent(AnimationWindowEvent evt)
        {
            if (evt.IsOpening)
            {
                HandleWindowOpen(evt);
            }
        }

        private void HandleWindowOpen(AnimationWindowEvent evt)
        {
            StopActiveSequence();

            if (container != null) container.gameObject.SetActive(true);
            if (progressSlider != null) 
            {
                progressSlider.gameObject.SetActive(true);
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
                progressSlider.wholeNumbers = false;
                progressSlider.interactable = false;
                progressSlider.normalizedValue = 0f;
            }

            EnsureUIElements();

            // Parse Payload for Duration and Perfect Center
            var payload = AnimationEventPayload.Parse(evt.Payload);
            float duration = 1.0f; // Default fallback
            if (payload.TryGetFloat("duration", out var d))
            {
                duration = d;
            }

            float perfectCenter = 0.5f;
            if (payload.TryGetFloat("perfect", out var p))
            {
                perfectCenter = p;
            }
            else if (payload.TryGetFloat("center", out var c))
            {
                perfectCenter = c;
            }

            // Setup Success Zone (Visual only for now, logic is in Runner)
            // Assuming a fixed visual width for the zone, e.g., 20% of the bar centered on perfectCenter
            float zoneWidth = 0.2f; 
            PositionSuccessZone(perfectCenter, zoneWidth);

            // Start Cursor Animation
            activeSequenceRoutine = StartCoroutine(RunSequence(duration));
        }

        private void OnResultEvent(TimedHitResultEvent evt)
        {
            Debug.Log($"[TimedHitOverlay] Result: {evt.Judgment} ({evt.DeltaMilliseconds:F1}ms)");
            if (resultText != null)
            {
                resultText.text = evt.Judgment.ToString();
                resultText.gameObject.SetActive(true);
            }
        }

        private IEnumerator RunSequence(float duration)
        {
            if (cursorRect != null) cursorRect.gameObject.SetActive(true);
            if (successZoneRect != null) successZoneRect.gameObject.SetActive(true);
            if (resultText != null) resultText.gameObject.SetActive(false);

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float normalized = Mathf.Clamp01(timer / duration);
                SetCursorPosition(normalized);
                yield return null;
            }

            SetCursorPosition(1f);

            // Hold for a moment before hiding
            yield return new WaitForSeconds(closeHoldSeconds);
            HideAll();
        }

        private void SetCursorPosition(float normalized)
        {
            if (progressSlider != null)
            {
                progressSlider.normalizedValue = normalized;
                return;
            }

            if (cursorRect != null)
            {
                // Anchor Min/Max X are both 'normalized' to position it horizontally
                cursorRect.anchorMin = new Vector2(normalized, 0f);
                cursorRect.anchorMax = new Vector2(normalized, 1f);
                cursorRect.anchoredPosition = Vector2.zero;
            }
        }

        private void PositionSuccessZone(float center, float width)
        {
            if (successZoneRect == null) return;

            float start = Mathf.Clamp01(center - (width * 0.5f));
            float end = Mathf.Clamp01(center + (width * 0.5f));

            successZoneRect.anchorMin = new Vector2(start, 0f);
            successZoneRect.anchorMax = new Vector2(end, 1f);
            successZoneRect.offsetMin = Vector2.zero;
            successZoneRect.offsetMax = Vector2.zero;
        }

        private void StopActiveSequence()
        {
            if (activeSequenceRoutine != null)
            {
                StopCoroutine(activeSequenceRoutine);
                activeSequenceRoutine = null;
            }
        }

        private void HideAll()
        {
            if (container != null) container.gameObject.SetActive(false);
            if (progressSlider != null) progressSlider.gameObject.SetActive(false);
        }

        private void EnsureUIElements()
        {
            if (progressSlider != null)
            {
                EnsureSuccessZone();
                return;
            }

            EnsureTimelineBar();
            EnsureCursor();
            EnsureSuccessZone();
        }

        private void EnsureTimelineBar()
        {
            if (timelineBarInstance != null) return;

            if (timelineBar != null)
            {
                timelineBarInstance = timelineBar.gameObject;
                timelineBarRect = timelineBar;
                return;
            }

            if (timelineBarPrefab != null && container != null)
            {
                timelineBarInstance = Instantiate(timelineBarPrefab, container);
                timelineBarRect = timelineBarInstance.GetComponent<RectTransform>();
                SetupFullScreenRect(timelineBarRect);
                timelineBar = timelineBarRect;
            }
        }

        private void EnsureCursor()
        {
            if (cursorInstance != null) return;

            if (cursor != null)
            {
                cursorInstance = cursor.gameObject;
                cursorRect = cursor;
                return;
            }

            if (cursorPrefab != null && container != null)
            {
                cursorInstance = Instantiate(cursorPrefab, container);
                cursorRect = cursorInstance.GetComponent<RectTransform>();
                // Cursor needs specific setup, usually a thin vertical bar
                cursorRect.anchorMin = new Vector2(0f, 0f);
                cursorRect.anchorMax = new Vector2(0f, 1f);
                cursorRect.sizeDelta = new Vector2(4f, 0f); // 4px width
                cursorRect.pivot = new Vector2(0.5f, 0.5f);
                cursor = cursorRect;
            }
            else if (container != null)
            {
                // Create default cursor if no prefab
                var go = new GameObject("Cursor", typeof(Image));
                go.transform.SetParent(container, false);
                go.GetComponent<Image>().color = Color.white;
                cursorInstance = go;
                cursorRect = go.GetComponent<RectTransform>();
                cursorRect.anchorMin = new Vector2(0f, 0f);
                cursorRect.anchorMax = new Vector2(0f, 1f);
                cursorRect.sizeDelta = new Vector2(4f, 0f);
                cursorRect.pivot = new Vector2(0.5f, 0.5f);
                cursor = cursorRect;
            }
        }

        private void EnsureSuccessZone()
        {
            if (successZoneInstance != null) return;

            // If a slider is used, avoid reusing its Fill rect (slider will drive its size). Create our own overlay child.
            if (progressSlider != null)
            {
                successZoneInstance = new GameObject("SuccessZoneOverlay", typeof(Image));
                successZoneInstance.transform.SetParent(progressSlider.transform, false);
                var img = successZoneInstance.GetComponent<Image>();
                img.color = new Color(0f, 1f, 0f, 1f);
                img.raycastTarget = false;

                successZoneRect = successZoneInstance.GetComponent<RectTransform>();
                successZoneRect.anchorMin = Vector2.zero;
                successZoneRect.anchorMax = Vector2.one;
                successZoneRect.offsetMin = Vector2.zero;
                successZoneRect.offsetMax = Vector2.zero;
                successZone = successZoneRect;
                return;
            }

            if (successZone != null)
            {
                successZoneInstance = successZone.gameObject;
                successZoneRect = successZone;
                return;
            }

            if (successZonePrefab != null && container != null)
            {
                successZoneInstance = Instantiate(successZonePrefab, container);
                successZoneRect = successZoneInstance.GetComponent<RectTransform>();
                SetupFullScreenRect(successZoneRect);
                successZone = successZoneRect;
            }
            else if (container != null)
            {
                // Create default zone
                var go = new GameObject("SuccessZone", typeof(Image));
                go.transform.SetParent(container, false);
                // Put it behind cursor if possible (sibling index)
                if (cursorInstance != null) go.transform.SetSiblingIndex(cursorInstance.transform.GetSiblingIndex());
                
                var img = go.GetComponent<Image>();
                img.color = new Color(0f, 1f, 0f, 0.3f);
                img.raycastTarget = false;
                
                successZoneInstance = go;
                successZoneRect = go.GetComponent<RectTransform>();
                SetupFullScreenRect(successZoneRect);
                successZone = successZoneRect;
            }
        }

        private void SetupFullScreenRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private void TryInitialize()
        {
            if (eventBus != null) return;

            var bus = installer != null
                ? installer.EventBus
                : AnimationSystemInstaller.Current != null ? AnimationSystemInstaller.Current.EventBus : null;

            if (bus != null)
            {
                Initialize(bus);
            }
        }
    }
}


/* 
FUTURO DE COMO MANEJAREMOS EL CUE VISUAL DEL TIMED HIT DE MANERA VALE OF ISERIS SUTIL Y HERMOSO.
```json
{
  "target_file": "Assets/Scripts/BattleV2/UI/TimedHitOverlay.cs",
  "intent": "Append a commented JSON spec at the end of the file describing the future Strategy-based visual widget refactor (no behavior changes now).",
  "warning_first": [
    "Current TimedHitOverlay mixes: orchestration (events/timing), layout (RectTransforms), and visuals (colors/overlays). That coupling will keep producing 'timer gamer' UI and make future visuals painful.",
    "Do NOT keep expanding this script with more UI variants via if/else booleans. It will become a second Harness.",
    "Do NOT let StepScheduler/Dispatcher own window timing for KS1 (single-emitter plan still stands). This change is purely presentation."
  ],
  "options": [
    {
      "option": "A) Strategy pattern (recommended)",
      "why": "Overlay remains the single source of progress/window state; visual implementations swap freely (SliderGamer vs GlintSlotSutil)."
    },
    {
      "option": "B) Prefab-only variants (not recommended long-term)",
      "why": "Fast, but still tends to leak behaviors into overlay and grows conditionals; hard to keep consistent across UIs."
    }
  ],
  "append_comment_block": {
    "location": "EOF",
    "format": "C# block comment /* ... ",
    "content": {
      "title": "TimedHitOverlay Future: Strategy-based Visual Widget (Slider vs Subtle Glint Slot)",
      "non_goals": [
        "No changes to KS1 timing or StepScheduler flow.",
        "No additional hacks in SceneFixer.",
        "No additional booleans for 'mode switching' inside TimedHitOverlay."
      ],
      "core_idea": {
        "overlay_role": [
          "Subscribe to AnimationWindowEvent + TimedHitResultEvent.",
          "Parse payload (duration + perfect/center).",
          "Run progress coroutine -> normalized [0..1].",
          "Delegate ALL drawing/animation to a visual strategy."
        ],
        "visual_role": [
          "Own references to UI elements (Slider, glint, slot glow).",
          "Render progress, window cue, and result feedback.",
          "No gameplay decisions, no window timing ownership."
        ]
      },
      "interfaces": {
        "ITimedHitVisualizer": {
          "methods": [
            "void EnsureBound(RectTransform containerOrRoot) // optional: find refs or validate wiring",
            "void Begin(in TimedHitVisualContext ctx)        // show + reset",
            "void SetProgress01(float t01)                  // called every frame",
            "void SetWindow01(float center01, float width01)// called once per window open (or per window update)",
            "void ShowResult(TimedHitJudgment judgment)     // pulse/glow, text optional",
            "void Hide()                                   // hide/disable visuals"
          ],
          "notes": [
            "Keep it dumb: feed it numbers, it renders. No event bus inside visual.",
            "All strategy classes must be pure presentation."
          ]
        },
        "TimedHitVisualContext": {
          "fields": [
            "float durationSeconds",
            "float perfectCenter01",
            "float windowWidth01",
            "string tag",
            "int windowIndex",
            "int windowCount",
            "bool useUnscaledTime (optional, if combat clock exists)"
          ]
        }
      },
      "strategies": {
        "SliderTimedHitVisualizer": {
          "purpose": "Debug / baseline. Keep existing slider approach.",
          "ui_refs": [
            "Slider progressSlider",
            "RectTransform successZoneOverlay (optional)",
            "Text resultText (optional)"
          ],
          "rules": [
            "Prefer slider.normalizedValue; do NOT also move cursorRect in this mode.",
            "Success zone should be subtle: alpha <= 0.12 (never solid neon green).",
            "Disable slider.interactable; wholeNumbers=false; min=0 max=1."
          ]
        },
        "GlintSlotTimedHitVisualizer": {
          "purpose": "Production subtle cue: moving glint + center diamond pulse.",
          "ui_refs": [
            "RectTransform barArea (the actual metal bar area, masked/clipped)",
            "RectTransform glint (small shine sprite/rect that moves across barArea)",
            "Graphic centerSlotGlow (diamond glow overlay at centered slot)",
            "AnimationCurve glintCurve (optional) OR tween params"
          ],
          "render_behavior": [
            "Begin: hide glow, place glint at bar start.",
            "SetProgress01: position glint along barArea X by t01; do not stretch UI; no fill-style blocks.",
            "SetWindow01: store window range; while progress is inside window -> raise centerSlotGlow alpha slightly (0.08-0.18).",
            "When progress crosses perfectCenter01 (or if judgment received): do a short pulse on centerSlotGlow (0.12 -> 0.28 -> 0.12).",
            "ShowResult: for PERFECT/GOOD: pulse stronger; for MISS: tiny dim red flash or no flash (keep subtle)."
          ],
          "timing_notes": [
            "Do NOT change underlying timing; do not apply easing to 'time' itself.",
            "If you want 'slow start then aggressive mid', do it as VISUAL interpolation only (piecewise mapping), not by altering duration.",
            "Optional piecewise visual mapping: map t01 to x01 with two segments: [0..perfectCenter] and [perfectCenter..1]."
          ]
        }
      },
      "TimedHitOverlay_refactor_steps": [
        {
          "step": "Extract UI drawing into strategy",
          "changes": [
            "Add [SerializeField] MonoBehaviour visualizerComponent implementing ITimedHitVisualizer (or ScriptableObject).",
            "Replace EnsureUIElements/EnsureSuccessZone/cursorRect logic with visualizer.Begin/SetProgress01/SetWindow01/ShowResult/Hide."
          ]
        },
        {
          "step": "Keep current code as fallback",
          "changes": [
            "If no visualizer assigned: default to SliderTimedHitVisualizer created inline OR keep current behavior temporarily behind a Debug flag."
          ]
        },
        {
          "step": "Support multi-window KS1 properly later",
          "changes": [
            "Upgrade SetWindow01 to SetWindows(list) or Begin(ctx includes windows array).",
            "Overlay receives per-window open events; strategy can highlight the relevant range if desired."
          ]
        }
      ],
      "acceptance_criteria": [
        "Overlay continues to work with KS1 single-emitter plan (no changes to timing authority).",
        "Swapping visual strategy changes only visuals; gameplay judgments stay identical.",
        "GlintSlot visual is subtle: no solid green blocks, minimal alpha, relies on motion + small glow pulse.",
        "No jitter: all UI elements anchored/pivoted consistently; glint movement is smooth."
      ],
      "do_not_do": [
        "Do not add more booleans like useSlider/useSleek/useFancy inside TimedHitOverlay.",
        "Do not read Input in the visual; input provider remains elsewhere.",
        "Do not make dispatcher/StepScheduler emit 'visual windows' as a workaround."
      ]
    }
  },
  "codex_instructions": {
    "task": "Append the above JSON spec as a C# block comment at the end of TimedHitOverlay.cs, verbatim and nicely formatted, without changing any runtime behavior.",
    "notes": [
      "No code refactor in this task. Only add comment block.",
      "Keep existing file formatting / namespace intact."
    ]
  }
}
```

*/