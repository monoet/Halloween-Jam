Principles KISS flow for basic melee; YAGNI for future systems; SOLID split between scheduler (timing) and listeners (presentation).
Scope Flags now: attack/windup, attack/runup, attack/impact, attack/runback, action/cancel. Everything else stays excluded until gameplay demands it (idle, buffs, spotlight, timed-hit extras, etc.).
Contracts Flags are immutable string IDs. Listeners implement ICombatEventListener.OnCombatEventRaised(flagId, context). Context is read-only with ActorView (caster ids/sides/anchors), ActionView (action/family/weapon/element), TargetsView (array + per_target flag), and Tags (persist_transform, needs_runback). Less data means fewer bugs.
Scheduler Relationship Recipes/StepScheduler remain the timing authority; they emit the five flags at defined action beats (windup pre-tween, runup move start, impact damage, runback return, cancel early exit). Listeners never mutate flow.
Filters Required on every route: role, direction, scope. Defaults: windup/runup/runback/cancel → Any/Outgoing/CasterOnly; impact → Any/Incoming/TargetOnly. This avoids allies animating when enemies attack.
CombatEventRouter MonoBehaviour that resolves tweens and SFX via: tweenByTrigger (triggerId → TweenPreset) and sfxByKey (family:weapon:element → SfxPreset). SFX fallback order: exact → weapon wildcard → family wildcard → default. Router applies filters before dispatching to listeners, tracks counters (cacheHit, cacheMiss, missingTween, eventsRaised), and logs per raise (flagId, caster_id, targets_count, cache hits/misses, tween started). Missing tween/SFX warnings are rate-limited.
Listeners
DOTweenListener: uses router-resolved TweenPreset. Modes: MoveLocal, MoveWorld, RunBackToAnchor, FrameSeq. TweenGate enforces per-actor mutex + kill on start. Default presets: windup FrameSeq (60 fps, forward/minor_back/recoil values, OutCubic/InOutSine eases), runup MoveLocal (abs pos offset, 0.30s, OutCubic, adds persist_transform tag), runback RunBackToAnchor (0.25s, InOutSine). Persisted transforms only reset via runback.
FMODListener: pulls SfxPreset using router key/fallback (family:weapon:element). Examples: attack/basic:sword:neutral, attack/basic:bow:*, default (always required).
Utilities Static CombatFlags/SfxFamilies constants; TweenGate.For(actorId) API; EventReplayRunner coroutine to play scripted (flag, delayMs, stubContext) sequences for QA/demo; LoggerCounters on router for asset coverage.
Guardrails Tags drive persistence; runback must tween (no teleport); filters mandatory; keep persist_transform until attack/runback.
Future SO Upgrade Path Optional ScriptableObjects (TweenCueSet, SoundCueSet) hydrate router dictionaries in Awake when assigned, otherwise fallback to hardcoded MVP data.
Risk Mitigation Filters prevent enemy attacks animating player, TweenGate removes jitter, fallback SFX & counters expose gaps, enforced runback prevents teleport resets.

{
  "version": "2.0.1-architecture",
  "title": "Arquitectura Conceptual — Combat Event Listeners (MVP+) — Clarifications",
  "changes_from_2_0_0": [
    "Lifecycle explícito de CombatEventContext.",
    "Regla de secuenciación para multi-target (AoE/hit-react).",
    "Fallback definido para runback sin caster_anchor.",
    "Contrato de acción/cancel para listeners.",
    "Plan de visibilidad de métricas/counters del Router.",
    "Ciclo de vida/limpieza de TweenGate."
  ],

  "A_context_lifecycle": {
    "type": "pooled-class",
    "immutability_contract": "Context es de solo lectura para listeners. Cualquier intento de mutación es bug.",
    "ownership": {
      "creator": "BattleManager/StepScheduler construye y rellena Context antes de Raise.",
      "router": "No muta Context. Puede generar vistas derivadas in-memory (no compartidas).",
      "listeners": "No mutan Context. Pueden leer y cachear IDs primitivos, NO referencias de Transform a largo plazo."
    },
    "pooling": {
      "why": "Evitar GC en beats frecuentes.",
      "policy": "Context se toma del pool por Raise(). Tras despachar a todos los listeners, el Dispatcher lo devuelve al pool.",
      "safety": "Nunca guardes referencias al Context fuera del frame de OnCombatEventRaised; si necesitas persistir, copia datos primitivos (ids, flags, números)."
    },
    "per_target_clone_rule": {
      "broadcast": {
        "when": "AoE simultáneo.",
        "shape": "Un solo Context con TargetsView.targets = [t1..tn], TargetsView.per_target = false.",
        "note": "Listeners iteran si necesitan operar por-target."
      },
      "per_target": {
        "when": "AoE escalonado o efectos con timing diferenciado.",
        "shape": "Scheduler emite N Raises. Cada Raise crea/copía un Context con un solo target (TargetsView.targets = [ti], per_target = true).",
        "perf": "Se reutiliza el mismo objeto del pool con overwrite por target; entre Raises se considera ‘nuevo’ Context desde la perspectiva del listener."
      }
    }
  },

  "B_multi_target_sequencing_rule": {
    "authority": "Scheduler decide simultáneo vs. escalonado.",
    "simultaneous": {
      "emit": "Un Raise('attack/impact', ctx_broadcast).",
      "router/listeners": "No cambian el orden. Si un listener necesita por-target, itera localmente sobre ctx.TargetsView.targets."
    },
    "staggered": {
      "emit": "N Raises secuenciales, uno por target, espaciados por ActionView.stagger_step.",
      "router/listeners": "Se procesan como eventos independientes; NO mantienen estado entre targets salvo lo que el listener diseñe explícitamente."
    },
    "deadlock_avoidance": "Nunca mezclar broadcast y per_target para el mismo beat. Si se requiere mezcla, partir en dos beats distintos."
  },

  "C_runback_fallbacks": {
    "primary_anchor": "caster_anchor (Transform).",
    "fallback_order": [
      "caster_anchor (si no es null y activo)",
      "caster_root (si anchor es null o está inválido)",
      "posición actual (si ambos son inválidos) → no mover y log warning"
    ],
    "logging": "Warn rate-limited: 'RunBack anchor missing; using caster_root' | 'RunBack root missing; skipping'.",
    "designer_note": "En escenas de prueba basta con caster_root; el anchor da control fino de formación."
  },

  "D_action_cancel_contract": {
    "flag": "action/cancel",
    "router_behavior": [
      "Despacha a listeners con filtros por defecto (Outgoing/CasterOnly).",
      "Cuenta eventosRaised y log rate-limited."
    ],
    "listeners_behavior": {
      "DOTweenListener": [
        "TweenGate.KillActive(complete=false) para el actor caster.",
        "Limpia efectos en curso del caster (no teletransportar)."
      ],
      "FMODListener": [
        "Stop de eventos one-shot en progreso si aplica (fade corto opcional).",
        "No alterar música global."
      ],
      "VFXListener_future": [
        "Kill/stop de instancias asociadas al caster con fade corto si existe."
      ]
    },
    "gate_policy": "Tras cancel, TweenGate queda libre para aceptar nuevo tween inmediatamente."
  },

  "E_router_metrics_visibility": {
    "counters": ["eventsRaised", "cacheHit", "cacheMiss", "missingTween"],
    "exposure": {
      "inspector": "Panel simple en CombatEventRouter con totales y reset button.",
      "dev_console": "Comando: 'router.stats' imprime contadores y top-5 keys con cacheMiss.",
      "telemetry": "Hook opcional (desactivado por defecto) para enviar snapshot cada X segundos en modo Dev/QA."
    },
    "rate_limit": {
      "warnings_per_second": 2,
      "grouping": "Agrupar por triggerId o sfxKey para no spamear."
    }
  },

  "F_tweengate_lifecycle": {
    "storage": "Diccionario estático actorId → TweenHandle.",
    "acquire": "TweenGate.For(actorId).Start(tween) hace KillOnStart del handle previo y registra el nuevo.",
    "release": [
      "OnTweenComplete limpia el handle para actorId.",
      "Escena/unload: TweenGate.KillAll(complete=false) en OnSceneUnloaded."
    ],
    "pooled_ids": "Si actorId proviene de un pool, el sistema que recicla IDs debe llamar TweenGate.KillActive antes de reusar el ID.",
    "safety": "Métodos son null-safe; no lanzan excepción si el actor no existe."
  },

  "G_scope_flags_in_scope": {
    "include": ["attack/windup", "attack/runup", "attack/impact", "attack/runback", "action/cancel"],
    "exclude_until_needed": ["anim/idle", "ui/*", "spell/*", "target/*", "intensity/*", "buffs/*", "debuff/*", "system_cancels/*"]
  },

  "H_filters_defaults": {
    "windup|runup|runback|action/cancel": { "role": "Any", "direction": "Outgoing", "scope": "CasterOnly" },
    "impact": { "role": "Any", "direction": "Incoming", "scope": "TargetOnly" }
  },

  "I_router_contract": {
    "maps": {
      "tweenByTrigger": "triggerId → TweenPreset",
      "sfxByKey": "family:weapon:element → SfxPreset"
    },
    "fallback_sfx": ["family:weapon:element", "family:weapon:*", "family:*:*", "default"],
    "so_opt_in": "Si existen TweenCueSet/SoundCueSet asignados, el Router hidrata en Awake(); si no, usa diccionarios embebidos.",
    "logging": "Por Raise: {flagId, caster_id, targets_count, cacheHit/Miss, tweenStarted(bool)}"
  },

  "J_listeners_contract": {
    "dotween": {
      "modes": ["FrameSeq (windup)", "MoveLocal (runup)", "MoveWorld", "RunBackToAnchor (runback)"],
      "persist_transform": "Respetar hasta que llegue runback.",
      "cancel": "En action/cancel, KillActive(complete=false) del caster."
    },
    "fmod": {
      "key": "family:weapon:element",
      "fallback": "exacto → weapon wildcard → family wildcard → default",
      "cancel": "Stop one-shots del caster si aplica (fade corto)."
    },
    "vfx_future": "Mismo patrón que FMOD (router/filtros/fallback/cancel)."
  },

  "K_utilities_notes": {
    "Constants": "IDs centralizados evitan typos.",
    "EventReplayRunner": "Permite QA sin combate real. Acepta broadcast y per_target con delays.",
    "RouterLogger": "Encapsula counters y rate-limit; imprime snapshot bajo comando."
  },

  "L_checklist_cierre": {
    "dispatcher": "Raise(flagId, ctx) en windup/runup/impact/runback + action/cancel.",
    "router": [
      "tweenByTrigger y sfxByKey poblados para basic_attack",
      "filtros por defecto aplicados",
      "counters (eventsRaised, cacheHit/Miss, missingTween)",
      "warnings con rate-limit visibles en inspector/console"
    ],
    "dotween_listener": [
      "FrameSeq (windup), MoveLocal (runup + persist_transform), RunBackToAnchor",
      "TweenGate por actor (mutex + KillOnStart)"
    ],
    "fmod_listener": [
      "SfxKey y fallback → default",
      "Mapeos mínimos: espada, arco, default"
    ],
    "event_replay_runner": "Secuencia de 4 flags con stubContext (broadcast y per_target).",
    "risk_notes": [
      "Ally se mueve en turno enemigo → filtros por defecto",
      "Snap back → persist_transform + runback",
      "Assets faltantes → default + cacheMiss",
      "Jitter/solapes → TweenGate"
    ]
  }
}



plan de trabajo:

Pasada 1 — Wiring MVP (4–6 commits)

feat(core): add CombatEventContext (pooled) and ICombatEventListener

feat(core): add CombatEventDispatcher and hook StepScheduler flags (windup/runup/impact/runback/cancel)

feat(router): add CombatEventRouter with filters and in-memory maps

feat(presentation): add DOTweenListener + TweenGate (mutex per actor)

feat(audio): add FMODListener (family:weapon:element with fallback)

chore(core): guard persist_transform + ensure runback beat emitted

Cierra la pasada con una etiqueta: tag: v0.1.0-mvp-wire

Pasada 2 — Presets + QA (3–4 commits)

feat(presets): seed basic_attack tweens and sfx (sword, bow, default)

feat(qa): add EventReplayRunner with 4-beat sequence (broadcast + per-target stub)

feat(router): add warning rate-limit and inspector counters snapshot

test(qa): add replay scene and smoke tests (`Assets/Scenes/QA/CombatEventReplay.unity` con `CombatEventReplaySceneBootstrap`)

tag: v0.2.0-mvp-presets

Pasada 3 — Cancel & Anchors (3 commits)

feat(core): implement action/cancel contract dispatch

feat(presentation): DOTweenListener cancels active tween for caster; FMOD stops one-shots

fix(router): runback anchor fallback (anchor→root→no-move + warn)

tag: v0.3.0-cancel-anchors

Opcional 4 — AoE/Stagger (2–3 commits)

feat(core): add AoE sequencing rules (broadcast vs per-target with stagger_step)

feat(presentation): target-loop handling in listeners (if needed for visuals)

docs(core): document deterministic target order

tag: v0.4.0-aoe-stagger

Opcional 5 — SO opt-in + Telemetry (2–3 commits)

feat(data): add TweenCueSet/SoundCueSet ScriptableObjects and router hydration

feat(telemetry): add dev console command and optional snapshot hook

docs(data): usage notes and migration path

tag: v0.5.0-so-optin
