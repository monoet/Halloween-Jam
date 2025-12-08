# UI/UX QoL Diagnostic Log — BattleV2

> Propósito: documentar el estado y el plan de diagnóstico de fricción de diseño/gameplay en interfaces existentes (sin crear sistemas nuevos).

## Meta y Principios
- Propósito: diagnosticar fricción en interfaces; no agregar MonoBehaviours/SOs ni alterar gameplay.
- Principios: separar interfaz de diseñador vs técnica; reducir campos editables a decisiones de diseño; usar solo tooling de inspector (headers, tooltips, validación, ocultar/condicionar).
- No metas: no agregar componentes nuevos, no rearquitecturar pipelines, no cambiar gameplay salvo bugs evidentes por interfaz.

## Entradas que faltan del usuario
- Unity version (ej. 2022.3.x).
- Inspector stack (Built-in / Odin / Custom / None).
- Target users: Designer, Tech-anim, Engineering, QA (si aplica).
- Escenas a abrir: battle scene + staging de prefabs si existe.
- Pain points reportados por diseñador/ingeniería (listas breves).

## Áreas de diagnóstico y qué recolectar
- Abilities & ScriptableObjects: lista de SO editables (paths/tipos); capturas/notas de 3 acciones (simple, marks+timed, multi-target); campos que toca diseñador vs no debería tocar.
- Battle Manager & Orchestration: diagrama rápido del flujo (Root → Menu → Target → Execute → Resolve); campos serializados en BM y servicios que confunden; responsabilidades que diseñador no debería tocar.
- Animation Installer/System: cómo se asignan clips/sets; campos editables en installers/prefabs; errores comunes (clip faltante/evento no disparado).
- Combatant Prefabs: snapshot de 1 player y 1 enemy; qué se edita en prefab vs SO; campos repetidos.
- HUD Widgets & Panels: jerarquía UI root + panels; lista de widgets con bindings; campos serializados en widgets/panels.

## Taxonomía de fricción (para log FRIC-###)
- Ambiguity, Duplication, Hidden coupling, Missing validation, Poor affordance, Overexposure.
- Campos para cada FRIC: surface, location, symptom, who (Designer/Engineering/Both), risk, quick_fix_candidate, notes.

## Contratos de inspector (por definir)
- Para cada tipo principal (ActionData, Combatant prefab, widget): Required vs Optional vs Conditional.
- Roles: DesignerEditable, TechEditable, DebugOnly, ReadOnly.

## QoL Plan (reglas)
- Priorizar: nombres, tooltips, headers, order, ranges, HideInInspector, warnings.
- Custom editors solo si lo anterior no basta.
- Cada cambio incluye test manual mínimo: crear acción nueva, asignar a combatant, correr selección/ejecución, verificar HUD.

## Próximos pasos inmediatos (sin tocar runtime)
- Inventario de superficies: listar tipos editables (SOs, prefabs, scene objects) por área; elegir 3 casos representativos por área.
- Log de fricciones: registrar FRIC-### con la taxonomía arriba.
- Definir contratos de inspector: Required/Optional/Conditional + roles.
- QoL plan: tareas QOL-### con change_type (InspectorOnly/CustomEditor/Validation/RefactorInterface) + manual_test.

## Estado actual de ejecución de diagnóstico
- Pendiente iniciar inventario y fricción log (no se han recolectado muestras ni screenshots).
- Aún no se solicitaron datos de usuario (unity_version, inspector_stack, escenas, pain points).
- Aún no se han definido contratos ni tareas QOL; este log prepara el marco de trabajo.

---

## Versión operacional (JSON) para ejecución sin ambigüedad
```json
{
  "doc": {
    "title": "UI/UX QoL Diagnostic Log — BattleV2",
    "version": "1.0.0",
    "purpose": "Diagnosticar fricción de diseño/gameplay en interfaces existentes (sin crear componentes/sistemas nuevos).",
    "principles": [
      "Separar interfaz de diseñador vs técnica usando solo tooling de inspector (headers, tooltips, ranges, validación, ocultar/condicionar, read-only).",
      "Reducir campos editables a decisiones de diseño; ocultar o proteger knobs técnicos.",
      "Preferir cambios de bajo riesgo: naming, order, grouping, tooltips, validación, warnings con contexto."
    ],
    "non_goals": [
      "Agregar MonoBehaviours nuevos",
      "Agregar ScriptableObjects nuevos (como tipos nuevos) o sistemas nuevos",
      "Rearquitecturar pipelines",
      "Cambiar gameplay (salvo bug evidente derivado de interfaz engañosa)"
    ]
  },
  "missing_inputs": {
    "unity_version": "6000.2.8f1",
    "inspector_stack": {
      "type": "Custom",
      "notes": "Carpeta Assets/Editor detectada; existen scripts de edición propios."
    },
    "target_users": {
      "designer_gameplay": true,
      "tech_anim": null,
      "engineering": true,
      "qa": null
    },
    "scenes_to_open": {
      "battle_scene_path": "Assets/Scenes/BattleCore_Playground.unity",
      "prefab_staging_scene_path": null
    },
    "pain_points": {
      "designer_reported": [],
      "engineering_reported": []
    }
  },
  "surfaces": {
    "allowed_values": [
      "Abilities_ScriptableObjects",
      "BattleManager_Orchestration",
      "AnimationSystem_Installer",
      "CombatantPrefabs_PlayersEnemies",
      "HUD_Widgets_Panels"
    ],
    "notes": "Usar solo estos nombres para evitar categorías inconsistentes."
  },
  "execution_plan": {
    "phases": [
      {
        "id": "PHASE-0",
        "name": "Inventario (sin juicios)",
        "goal": "Mapear qué se edita, dónde, por quién.",
        "deliverables": [
          "inventory.items[]",
          "samples[] (mínimo 3 por surface)"
        ],
        "done_when": "Existe inventario con paths + owners + frecuencia + notas y muestras seleccionadas por superficie."
      },
      {
        "id": "PHASE-1",
        "name": "Friction log (observaciones)",
        "goal": "Registrar fricciones FRIC-### con evidencia.",
        "deliverables": [
          "friction_log[]"
        ],
        "done_when": "Cada FRIC incluye location exacta, campo(s) implicados, síntoma, impacto, evidencia y quick-fix candidato."
      },
      {
        "id": "PHASE-2",
        "name": "Contratos de inspector (curación)",
        "goal": "Definir Required/Optional/Conditional + roles (DesignerEditable/TechEditable/DebugOnly/ReadOnly).",
        "deliverables": [
          "inspector_contracts[]"
        ],
        "done_when": "Para cada tipo principal se definió la superficie mínima y el rol de cada campo."
      },
      {
        "id": "PHASE-3",
        "name": "Plan QoL (tareas)",
        "goal": "Convertir hallazgos en tareas QOL-### con criterio de aceptación y test manual.",
        "deliverables": [
          "qol_plan[]"
        ],
        "done_when": "Cada QOL-### tiene change_type, riesgo, alcance, aceptación y test manual reproducible."
      }
    ]
  },
  "evidence_rules": {
    "required_per_fric": [
      "unity_object_path_or_asset_path",
      "component_or_type_name",
      "field_names_involved",
      "1_screenshot_or_inspector_dump_reference",
      "impact_statement_in_plain_language"
    ],
    "warning": "Sin evidencia mínima, el FRIC no se considera accionable."
  },
  "inventory": {
    "items": [],
    "schema": {
      "type": "ScriptableObject|Prefab|SceneObject|CodeType",
      "path": "string",
      "owner": "Designer|Engineering|Both",
      "edited_frequency": "Daily|Weekly|Rare",
      "surface": "one_of_surfaces.allowed_values",
      "notes": "string"
    }
  },
  "samples": {
    "selected": [],
    "selection_rules": {
      "per_surface_min": 3,
      "abilities_min_set": [
        "Action simple single-target (sin marks/timedhit)",
        "Action con marks + gating (CP/TimedHit)",
        "Action multi-target (All) con StepScheduler steps"
      ],
      "prefabs_min_set": [
        "1 Player representativo",
        "1 Enemy representativo"
      ],
      "ui_min_set": [
        "1 panel de lista (Mag/Item/Actions)",
        "1 widget de recursos (HP/CP/SP)",
        "1 widget de turno (turn order/active actor)"
      ]
    },
    "schema": {
      "id": "SAMPLE-###",
      "surface": "one_of_surfaces.allowed_values",
      "path_or_reference": "string",
      "why_this_sample": "string",
      "expected_owner": "Designer|Engineering|Both",
      "notes": "string"
    }
  },
  "friction_log": {
    "items": [],
    "taxonomy": [
      "Ambiguity",
      "Duplication",
      "HiddenCoupling",
      "MissingValidation",
      "PoorAffordance",
      "Overexposure"
    ],
    "schema": {
      "id": "FRIC-###",
      "surface": "one_of_surfaces.allowed_values",
      "taxonomy": "one_of_friction_log.taxonomy",
      "location": "file path / prefab path / scene object path / type name",
      "fields_involved": [
        "string"
      ],
      "symptom": "string",
      "who": "Designer|Engineering|Both",
      "risk": "Low|Med|High",
      "quick_fix_candidate": "Yes|No",
      "evidence": {
        "screenshot_ref": null,
        "inspector_dump_ref": null,
        "notes": "string"
      ],
      "impact": {
        "time_waste": "Low|Med|High",
        "bug_proneness": "Low|Med|High",
        "learning_curve": "Low|Med|High"
      },
      "cause_hypothesis": "string",
      "suggested_fix_types": [
        "RenameField",
        "Tooltip",
        "HeaderGrouping",
        "Reorder",
        "RangeUnits",
        "ConditionalDisplay",
        "HideDebugFields",
        "ReadOnly",
        "ValidationWarning"
      ],
      "acceptance_check": "string"
    }
  },
  "inspector_contracts": {
    "items": [],
    "schema": {
      "id": "CONTRACT-###",
      "surface": "one_of_surfaces.allowed_values",
      "type_or_component": "string",
      "required_fields": [
        "string"
      ],
      "optional_fields": [
        "string"
      ],
      "conditional_fields": [
        {
          "field": "string",
          "shown_when": "string",
          "notes": "string"
        }
      ],
      "field_roles": [
        {
          "field": "string",
          "role": "DesignerEditable|TechEditable|DebugOnly|ReadOnly",
          "notes": "string"
        }
      ],
      "validation_rules": [
        "string"
      ]
    }
  },
  "qol_plan": {
    "items": [],
    "change_types": [
      "InspectorOnly",
      "CustomEditor",
      "Validation",
      "RefactorInterface"
    ],
    "schema": {
      "id": "QOL-###",
      "surface": "one_of_surfaces.allowed_values",
      "change_type": "one_of_qol_plan.change_types",
      "what_changes": "string",
      "risk": "Low|Med|High",
      "scope_notes": "string",
      "acceptance_criteria": [
        "string"
      ],
      "manual_test": [
        "Create new action asset",
        "Assign to combatant",
        "Run battle selection flow",
        "Execute action (incl. timed hit si aplica)",
        "Verify HUD updates"
      ],
      "linked_fric_ids": [
        "FRIC-###"
      ]
    }
  },
  "risk_rules": {
    "high_risk_if": [
      "Cambiar serializados usados por prefabs/escenas sin migración",
      "Renombrar campos/IDs consumidos por runtime (ActionId, string keys) sin auditoría completa",
      "Tocar flujo de selección/targeting en runtime"
    ],
    "low_risk_examples": [
      "Tooltips, headers, reordenamiento de campos",
      "Ocultar debug knobs del diseñador",
      "Warnings/validation en OnValidate sin cambiar lógica de ejecución"
    ]
  },
  "next_actions": [
    {
      "id": "NEXT-1",
      "task": "Llenar missing_inputs mínimos (unity_version, inspector_stack, battle_scene_path).",
      "blocking": true
    },
    {
      "id": "NEXT-2",
      "task": "PHASE-0: inventario inicial por superficie (mínimo 10 items totales).",
      "blocking": true
    },
    {
      "id": "NEXT-3",
      "task": "Seleccionar samples[] según selection_rules.",
      "blocking": true
    },
    {
      "id": "NEXT-4",
      "task": "Crear los primeros 10 FRIC-### con evidencia mínima (aunque sea screenshot_ref pendiente).",
      "blocking": false
    }
  ]
}
```

### Inventario inicial (PHASE-0) — 10 ítems
```json
{
  "inventory": {
    "items": [
      {
        "type": "SceneObject",
        "path": "Assets/Scenes/BattleCore_Playground.unity",
        "owner": "Both",
        "edited_frequency": "Weekly",
        "surface": "BattleManager_Orchestration",
        "notes": "Escena principal de playground; usada por diseño y engineering."
      },
      {
        "type": "CodeType",
        "path": "Assets/Scripts/BattleV2/Orchestration/BattleManagerV2.cs",
        "owner": "Engineering",
        "edited_frequency": "Weekly",
        "surface": "BattleManager_Orchestration",
        "notes": "Orquestador central; configura servicios, turnos, marks, input."
      },
      {
        "type": "CodeType",
        "path": "Assets/Scripts/BattleV2/Orchestration/Services/PlayerActionExecutor.cs",
        "owner": "Engineering",
        "edited_frequency": "Weekly",
        "surface": "BattleManager_Orchestration",
        "notes": "Ejecución de acciones player; cobra CP/SP, pipeline, triggered effects, marks."
      },
      {
        "type": "CodeType",
        "path": "Assets/Scripts/BattleV2/Orchestration/Services/BattleTurnService.cs",
        "owner": "Engineering",
        "edited_frequency": "Weekly",
        "surface": "BattleManager_Orchestration",
        "notes": "Control de turnos + contadores; hook para expiración de marks."
      },
      {
        "type": "CodeType",
        "path": "Assets/Scripts/BattleV2/Marks/MarkInteractionProcessor.cs",
        "owner": "Engineering",
        "edited_frequency": "Weekly",
        "surface": "Abilities_ScriptableObjects",
        "notes": "Middleware de marks; aplica/refresh/detonate según reglas puras."
      },
      {
        "type": "CodeType",
        "path": "Assets/Scripts/BattleV2/Marks/MarkService.cs",
        "owner": "Engineering",
        "edited_frequency": "Weekly",
        "surface": "Abilities_ScriptableObjects",
        "notes": "Slot de mark en CombatantState; eventos OnMarkChanged."
      },
      {
        "type": "CodeType",
        "path": "Assets/Scripts/BattleV2/Marks/MarkRulesEngine.cs",
        "owner": "Engineering",
        "edited_frequency": "Weekly",
        "surface": "Abilities_ScriptableObjects",
        "notes": "Reglas puras (gates, resolve interaction, AoE RNG)."
      },
      {
        "type": "CodeType",
        "path": "Assets/Scripts/BattleV2/Actions/LunarChainAction.cs",
        "owner": "Engineering",
        "edited_frequency": "Weekly",
        "surface": "Abilities_ScriptableObjects",
        "notes": "Acción con lógica de CP/refund; caso representativo de acción compleja."
      },
      {
        "type": "CodeType",
        "path": "Assets/Scripts/02_Systems/03_Combat/Combat/CombatantState.cs",
        "owner": "Engineering",
        "edited_frequency": "Weekly",
        "surface": "CombatantPrefabs_PlayersEnemies",
        "notes": "Estado runtime (HP/SP/CP/MarkSlot/StableId); usado por prefabs."
      },
      {
        "type": "CodeType",
        "path": "Assets/Scripts/BattleV2/AnimationSystem/Runtime/AnimationSystemInstaller.cs",
        "owner": "Engineering",
        "edited_frequency": "Weekly",
        "surface": "AnimationSystem_Installer",
        "notes": "Installer de sistema de animación; binding de buses y orchestrator."
      },
      {
        "type": "CodeType",
        "path": "Assets/Scripts/BattleV2/UI/HUDManager.cs",
        "owner": "Engineering",
        "edited_frequency": "Weekly",
        "surface": "HUD_Widgets_Panels",
        "notes": "HUD principal; controla widgets de recursos/turno."
      }
    ]
  }
}
```
