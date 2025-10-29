# JRPG Animation System - Development Log

> Mantener este registro sincronizado con los hitos del documento _LOCKED.

## 2025-10-31 - Checkpoint: Milestone 1 (Infraestructura Base)
- [x] Definido IAnimationOrchestrator + AnimationRequest (contrato PlayAsync respetado).
- [x] CombatClock monotono (CombatClock + ICombatClock), configurable para pruebas.
- [x] Event bus ligero (AnimationEventBus) con eventos Phase/Impact/Window/Lock.
- [x] Adaptador temporal LegacyAnimationOrchestratorAdapter enlazando con el orquestador legacy.
- [x] Guardrail de editor comprobando que BattleManagerV2 siga invocando PlayAsync.

Notas:
- No se tocaron flujos existentes; solo nuevas dependencias.
- Proximo objetivo: Milestone 2 (datos autorables ActionTimeline, ActionCatalog, validator). Preparar ScriptableObjects y validaciones editor.

## 2025-10-29 - Milestone 2 - Datos autorables
- [x] Crear ActionTimeline ScriptableObject con tracks y fases.
- [x] Crear ActionCatalog indexando timelines.
- [x] Implementar AnimValidator (rangos y tags).
- [x] Cargar timelines demo (basic_attack, magic_bolt).
- [x] Validar catalogo sin errores.

Notas:
- Assets/Animation/Timelines/ contiene los assets basic_attack, magic_bolt y ActionTimelineCatalog.
- ActionTimelineCatalogValidatorEditor agrega menu Battle/Animation/Validate Action Timeline Catalog para disparar validaciones usando ActionTimelineValidator.
- Los assets demo usan GUIDs fijos para integrarse con el nuevo contrato sin romper referencias.
- Actualizacion: renombrado el catalogo de animacion a ActionTimelineCatalog y el menu de validacion a Validate Action Timeline Catalog para evitar confusion con el ActionCatalog de gameplay.
- Mejoras extra: el validador ahora detecta solapamientos, tags duplicadas y muestra resumen por catalogo/total en consola; el catalogo expone ForceRebuild y warnings cuando faltan ActionIds.

## 2025-10-29 - Milestone 3 - Nucleo de ejecucion (en progreso)
- [x] IAnimationOrchestrator ahora acepta CancellationToken para soportar cancelaciones seguras.
- [x] TimelineCompiler genera fases normalizadas y mantiene metadata completa.
- [x] ActionLockManager, TimelineRuntimeBuilder y ActionSequencer coordinan locks y eventos sobre CombatClock.
- [x] AnimationEventBus expandido con tags, payload y razon de lock para los routers.
- [x] ActionTimelineCatalog y AnimValidator reforzados con chequeos adicionales.
- [ ] TimedHitService/InputBuffer quedaran para el siguiente tramo del milestone.

Notas:
- ActionSequencerDriver permite tickear secuencias en prototipos usando CombatClock.
- Se introdujo ActionSystem_Scripts_Export.txt para compartir rapidamente el estado del sistema.
