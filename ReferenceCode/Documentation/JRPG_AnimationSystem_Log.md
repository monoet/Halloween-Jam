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
- [x] Crear `NewAnimOrchestratorAdapter` que resuelva wrappers, sockets y locks siguiendo la sección 5 del LOCKED.
- [ ] Validar assets `basic_attack` y `magic_bolt` con payloads de routers + escena de combate usando el installer.

Notas:
- `AnimatorWrapper` vive en `Assets/Scripts/BattleV2/AnimationSystem/Execution/AnimatorWrapper.cs`; inicializa el PlayableGraph bajo demanda y destruye clips/graph en `Dispose`.
- El fallback ahora interpola con un driver de coroutine propio, controlando pesos del mixer y cancelándose en dispose/cancel.
- Routers añadidos en `Assets/Scripts/BattleV2/AnimationSystem/Execution/Routers/`: `AnimationVfxRouter`, `AnimationSfxRouter`, `AnimationCameraRouter`, `AnimationUiRouter`. Cada uno expone contrato de servicio (`IAnimationVfxService`, etc.) y logs de guardrail cuando faltan bindings o cancelación libera locks.
- Timelines actualizados (carpeta `Assets/Animation/Timelines/`): `basic_attack.asset` incluye clip `basic_attack_swing`, payloads `vfx=/sfx=/camera=` y prompt UI para ventana; `magic_bolt.asset` define clips de charge/cast/projectile/impact con IDs únicos y payloads SFX/VFX camera shake.
- `NewAnimOrchestratorAdapter` en `Assets/Scripts/BattleV2/AnimationSystem/Runtime/NewAnimOrchestratorAdapter.cs` instancia `AnimatorWrapperResolver`, resuelve clips vía `AnimationClipResolver`, lanza secuencias con `ActionSequencerDriver` y coordina routers mediante `AnimationRouterBundle`.
- `AnimationSystemInstaller` ahora expone bindings serializables (`actorBindings`, `clipBindings`) y slots para servicios VFX/SFX/Camera/UI; si no se asignan, se usa un servicio nulo (solo logs).
- Para preparar escena: rellenar `actorBindings` con cada `CombatantState` (Animator + fallback), poblar `clipBindings` con IDs usados por los timelines y apuntar `vfxServiceSource`/`sfxServiceSource`/`cameraServiceSource`/`uiServiceSource` a componentes que implementen los contratos. Luego habilitar `useAnimationSystemInstaller` en `BattleManagerV2` y ejecutar `basic_attack` + `magic_bolt` verificando animación Playables visible, VFX/SFX disparados y locks liberados (fin controlado por `LockRelease`).
- Checklist de validación en escena (cuando Unity esté disponible):
  1. Abrir `Assets/Scenes/CombatSandbox.unity` (o una copia de validación).
  2. Seleccionar el objeto que contiene `AnimationSystemInstaller` y rellenar los nuevos campos:
     - `Sequencer Driver`: referencia al `ActionSequencerDriver` presente en escena.
     - `Timeline Catalog`: `ActionTimelineCatalog` con los timelines `basic_attack` y `magic_bolt`.
     - `Actor Bindings`: por cada `CombatantState` usado en pruebas (jugador/enemigo) asignar:
       * `Actor`: referencia al componente `CombatantState`.
       * `Animator`: el `Animator` que debe ser conducido por Playables.
       * `Fallback Clip`: `AnimationClip` estático o loop idle.
       * `Sockets`: transforms opcionales para spawn de VFX.
     - `Clip Bindings`: lista de pares `Id → AnimationClip` para todos los IDs presentes en los timelines (`basic_attack_swing`, `magic_charge_loop`, `magic_cast_release`, `magic_projectile_flight`, `magic_impact`, etc.).
     - Servicios:
       * `Vfx Service Source`: componente/ScriptableObject que implemente `IAnimationVfxService` (por ahora puede quedar nulo y usará el stub que solo loguea).
       * `Sfx Service Source`: idem para `IAnimationSfxService`.
       * `Camera Service Source`: idem para `IAnimationCameraService`.
       * `Ui Service Source`: idem para `IAnimationUiService`.
  3. En `BattleManagerV2`, activar `useAnimationSystemInstaller` y asignar el `AnimationSystemInstaller` configurado.
  4. Ejecutar la escena disparando acciones `basic_attack` y `magic_bolt`; observar:
     - Animación se reproduce vía Playables (verificar blend al fallback al finalizar/cancelar).
     - Eventos Impact/Phase disparan routers (cuando existan servicios concretos).
     - Locks se liberan al final (`timeline:{id}`) y `PlayAsync` sólo se completa tras `LockRelease`.
- Próximo orden sugerido: Routers → Adapter → Data → Escena de validación → Documentación adicional.
- Próximo orden sugerido: Routers → Adapter → Data → Escena de validación → Documentación adicional.

## 2025-11-04 - Milestone 5 - Timed hit y precisión (kickoff)
- [x] TimedHitToleranceProfileAsset permite definir tolerancias por ID (Perfect/Good/Early/Late) y se expone desde AnimationSystemInstaller.
- [x] TimedHitService parsea payloads (	olerance*, perfect, center) para resolver perfiles dedicados y calcular el centro real usando CombatClock.
- [x] TimedInputBuffer.TryConsume acepta centro personalizado, preservando deltas ms exactos para los juicios.
- [x] Las ventanas almacenan payloads completos, dejando preparada la UI/routers para reutilizar la metadata.
- [ ] Pendiente: capturar snapshots 30/60/120 FPS con los nuevos perfiles y afinar presets definitivos por timeline.


\n## 2025-11-04 - Milestone 5 - Timed hit y precisión (kickoff)\n- [x] TimedHitToleranceProfileAsset permite definir tolerancias por ID (Perfect/Good/Early/Late) y se expone desde AnimationSystemInstaller.\n- [x] TimedHitService parsea payloads (	olerance*, perfect, center) para resolver perfiles dedicados y calcular el centro real usando CombatClock.\n- [x] TimedInputBuffer.TryConsume acepta centro personalizado, preservando deltas ms exactos para los juicios.\n- [x] Las ventanas almacenan payloads completos; UI y routers pueden reutilizar metadata sin perder la tolerancia.\n- [x] Timeline magic_bolt actualizado con ventanas dobles (magic_primary / magic_secondary) y payloads consistentes con los nuevos IDs.\n- [ ] Pendiente: capturar snapshots 30/60/120 FPS con los nuevos perfiles y afinar presets definitivos por timeline.\n
- [x] Ks1TimedHitRunner usa TimelineDuration como duración por fase (timeline fijo aunque aumente el CP).
- [x] BattleDebugHarness ajustado para reflejar la misma duración por fase.
