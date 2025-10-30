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
- [x] TimelineCompiler normaliza y convierte timelines a tiempos reales, despachando eventos en el orden del LOCKED con tolerancias configurables y hook `EventDispatched`.
- [x] ActionSequencer y ActionLockManager coordinan locks, cancelaciones amigables y liberaciones deterministas sobre CombatClock.
- [x] AnimationEventBus expandido con tags, payload, razon de lock y tolerancias para los routers.
- [x] ActionTimelineCatalog y ActionTimelineValidator ahora garantizan tags únicas, tracks mínimos y warnings limpios.
- [x] TimedHitService/InputBuffer para escuchar AnimationWindowEvent y producir juicios (Perfect/Good/Miss).
- [x] Infra inicial de snapshot tests/telemetría para validar determinismo multi-FPS.

Notas:
- ActionSequencerDriver permite tickear secuencias en prototipos usando CombatClock.
- Scripts relevantes exportados a `ReferenceCode/Documentation/AnimationSystem_Scripts_Export.txt`.
- Catalogo y validator sin warnings tras refuerzos de tags/locks.
- TimedHitService + TimedInputBuffer resuelven ventanas usando CombatClock, perfil de tolerancias y publican `TimedHitResultEvent` (Perfect/Good/Miss) en el bus.
- `AnimationSystemInstaller` arma el pipeline (clock, bus, runtime builder, TimedHitService) y expone el nuevo orquestador; falta cablear en escena y sustituir definitivamente al adapter legacy.
- `TimedHitInputRelay` y `TimedHitHudBridge` conectan inputs/HUD al bus de animación; `SequencerSnapshotHarness` permite capturar trazas del sequencer a 30/60/120 FPS como base para determinismo.
