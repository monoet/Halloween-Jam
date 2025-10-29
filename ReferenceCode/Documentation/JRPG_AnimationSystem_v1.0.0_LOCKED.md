LOCKED: Este documento esta bloqueado; cualquier nueva funcionalidad debe cumplir los Contratos Duros y Guardrails o sera rechazada en revision.

## JRPG Animation System - Data-Driven + Playables + Timed Hit (v1.0.0)

Autor: orientado a Unity 2021+. Objetivo: reemplazar el sistema legacy sin tocar BattleManagerV2 (contrato PlayAsync -> Task).

### Checklist Maestro
- [ ] Milestone 1 - Contrato base y arquitectura
  - [ ] Definir IAnimationOrchestrator (PlayAsync).
  - [ ] Implementar CombatClock monotono.
  - [ ] Implementar EventBus basico (Phase/Impact/Window/Lock).
  - [ ] Validar que BattleManagerV2 invoque PlayAsync.
  - [ ] Agregar guardrails (manager intocable, CI check).
- [ ] Milestone 2 - Datos autorables
  - [ ] Crear ActionTimeline ScriptableObject con tracks y fases.
  - [ ] Crear ActionCatalog indexando timelines.
  - [ ] Implementar AnimValidator (rangos y tags).
  - [ ] Cargar timelines demo (basic_attack, magic_bolt).
  - [ ] Validar catalogo sin errores.
- [ ] Milestone 3 - Nucleo de ejecucion
  - [ ] TimelineCompiler: markers [0..1] -> segundos reales.
  - [ ] ActionSequencer: controlar fases y ticks.
  - [ ] Implementar LockManager simple.
  - [ ] Emitir eventos Window, Impact, Release.
  - [ ] Probar ciclo completo con logs.
- [ ] Milestone 4 - Integracion visual
  - [ ] AnimatorWrapper con Playables.
  - [ ] Routers (VFX, SFX, Camera, UI) escuchando EventBus.
  - [ ] Implementar NewAnimOrchestratorAdapter.
  - [ ] Sustituir orquestador viejo (sin borrarlo aun).
  - [ ] Ejecutar basic_attack y magic_bolt visibles.
- [ ] Milestone 5 - Timed hit y precision
  - [ ] Crear InputBuffer con timestamps.
  - [ ] TimedHitService: evaluar ventanas y emitir resultados.
  - [ ] Mostrar feedback HUD (Perfect/Good/Miss).
  - [ ] Validar determinismo 30/60/120 FPS.
  - [ ] Sincronizar feedback con EventBus.
- [ ] Milestone 6 - Tooling, tests y limpieza
  - [ ] TimelinePreviewWindow (scrubbing visual).
  - [ ] Snapshot tests y telemetria (deltaMs, judgment).
  - [ ] Validar AnimValidator en CI.
  - [ ] Eliminar scripts y prefabs legacy.
  - [ ] Confirmar build limpia (_LOCKED).
- [ ] Contrato IAnimationOrchestrator intacto (PlayAsync).
- [ ] Arquitectura Clean/Hexagonal + Track Pipeline + Playables operativa.
- [ ] Timed hits evaluados exclusivamente con CombatClock.
- [ ] ActionCatalog + ActionTimeline 100% data-driven.
- [ ] Tooling (validator + preview) en funcionamiento.
- [ ] Orquestador/strategies legacy retirados solo tras validacion final.
LOCKED: Este documento esta bloqueado; cualquier nueva funcionalidad debe cumplir los Contratos Duros y Guardrails o sera rechazada en revision.

## JRPG Animation System – Data-Driven + Playables + Timed Hit (v1.0.0)

Autor: orientado a Unity 2021+. Objetivo: reemplazar el sistema legacy sin tocar BattleManagerV2 (contrato PlayAsync ? Task).

### Checklist Maestro
- [ ] Milestone 1 – Contrato base y arquitectura
  - [ ] Definir IAnimationOrchestrator (PlayAsync).
  - [ ] Implementar CombatClock monotono.
  - [ ] Implementar EventBus basico (Phase/Impact/Window/Lock).
  - [ ] Validar que BattleManagerV2 invoque PlayAsync.
  - [ ] Agregar guardrails (manager intocable, CI check).
- [ ] Milestone 2 – Datos autorables
  - [ ] Crear ActionTimeline SO con tracks y fases.
  - [ ] Crear ActionCatalog indexando timelines.
  - [ ] Implementar AnimValidator (rangos y tags).
  - [ ] Cargar timelines demo (asic_attack, magic_bolt).
  - [ ] Validar catalogo sin errores.
- [ ] Milestone 3 – Nucleo de ejecucion
  - [ ] TimelineCompiler: markers [0..1] ? segundos reales.
  - [ ] ActionSequencer: controlar fases y ticks.
  - [ ] Implementar LockManager simple.
  - [ ] Emitir eventos Window, Impact, Release.
  - [ ] Probar ciclo completo con logs.
- [ ] Milestone 4 – Integracion visual
  - [ ] AnimatorWrapper con Playables.
  - [ ] Routers (VFX, SFX, Camera, UI) escuchando EventBus.
  - [ ] Implementar NewAnimOrchestratorAdapter.
  - [ ] Sustituir orquestador viejo (sin borrarlo aun).
  - [ ] Ejecutar asic_attack y magic_bolt visibles.
- [ ] Milestone 5 – Timed hit y precision
  - [ ] Crear InputBuffer con timestamps.
  - [ ] TimedHitService: evaluar ventanas y emitir resultados.
  - [ ] Mostrar feedback HUD (Perfect/Good/Miss).
  - [ ] Validar determinismo 30/60/120 FPS.
  - [ ] Sincronizar feedback con EventBus.
- [ ] Milestone 6 – Tooling, tests y limpieza
  - [ ] TimelinePreviewWindow (scrubbing visual).
  - [ ] Snapshot tests y telemetria (deltaMs, judgment).
  - [ ] Validar AnimValidator en CI.
  - [ ] Eliminar scripts y prefabs legacy.
  - [ ] Confirmar build limpia (_LOCKED).
- [ ] Contrato IAnimationOrchestrator intacto (PlayAsync).
- [ ] Arquitectura Clean/Hexagonal + Track Pipeline + Playables operativa.
- [ ] Timed hits evaluados exclusivamente con CombatClock.
- [ ] ActionCatalog + ActionTimeline 100% data-driven.
- [ ] Tooling (validator + preview) en funcionamiento.
- [ ] Orquestador/strategies legacy retirados solo tras validacion final.



