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
- `BattleManagerV2` ahora puede usar `AnimationSystemInstaller` (toggle `useAnimationSystemInstaller` + referencia) para delegar al nuevo orquestador vía `BattleAnimationSystemBridge`.

## 2025-11-01 - Milestone 4 - Integración visual (kickoff)
- [x] Crear esqueleto de `AnimatorWrapper` con PlayableGraph propio, mixer a dos entradas y clip fallback configurable por actor.
- [x] Exponer API `PlayClip`, `Stop`, `ResetToFallback`, `AttachCancellation` y tipos `AnimatorClipOptions`/`AnimatorClipHandle` alineados con el contrato LOCKED.
- [x] Añadir guardrails: warning al reproducir clips nulos, log cuando se cancela el token, TODO explícito para blends cronometrados.
- [x] Implementar blend real hacia el fallback usando driver de tiempo del sequencer.
- [x] Cablear routers visuales (VFX/SFX/Camera/UI) al `AnimationEventBus`.
- [x] Enriquecer timelines `basic_attack` y `magic_bolt` con payloads para routers y clips PlayableGraph.
- [ ] Crear `NewAnimOrchestratorAdapter` que resuelva wrappers, sockets y locks siguiendo la sección 5 del LOCKED.
- [ ] Validar assets `basic_attack` y `magic_bolt` con payloads de routers + escena de combate usando el installer.

Notas:
- `AnimatorWrapper` vive en `Assets/Scripts/BattleV2/AnimationSystem/Execution/AnimatorWrapper.cs`; inicializa el PlayableGraph bajo demanda y destruye clips/graph en `Dispose`.
- El fallback ahora interpola con un driver de coroutine propio, controlando pesos del mixer y cancelándose en dispose/cancel.
- Routers añadidos en `Assets/Scripts/BattleV2/AnimationSystem/Execution/Routers/`: `AnimationVfxRouter`, `AnimationSfxRouter`, `AnimationCameraRouter`, `AnimationUiRouter`. Cada uno expone contrato de servicio (`IAnimationVfxService`, etc.) y logs de guardrail cuando faltan bindings o cancelación libera locks.
- Timelines actualizados (carpeta `Assets/Animation/Timelines/`): `basic_attack.asset` incluye clip `basic_attack_swing`, payloads `vfx=/sfx=/camera=` y prompt UI para ventana; `magic_bolt.asset` define clips de charge/cast/projectile/impact con IDs únicos y payloads SFX/VFX camera shake.
- Checklist de validación en escena: abrir `Assets/Scenes/CombatSandbox.unity`, habilitar `BattleManagerV2.useAnimationSystemInstaller`, asignar `AnimationSystemInstaller`, `ActionSequencerDriver` y servicios para routers; reproducir acciones `basic_attack` y `magic_bolt` verificando animación Playables visible, VFX/SFX disparados y locks liberados (fin controlado por `LockRelease`).
- Próximo orden sugerido: Routers → Adapter → Data → Escena de validación → Documentación adicional.
