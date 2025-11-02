 Este documento esta bloqueado: cualquier nueva funcionalidad debe cumplir los Contratos Duros y Guardrails o sera rechazada en revision.

## ✅ JRPG Animation System – Data-Driven + Playables + Timed Hit (v1.0.0)

Autor orientado a Unity 2021+. Objetivo: reemplazar el sistema legacy sin tocar `BattleManagerV2` (contrato `PlayAsync` → `Task`).

### Checklist Global
### ✅ Contrato `IAnimationOrchestrator`
- [ ] La firma `Task PlayAsync(ActionPlaybackRequest req, CancellationToken ct = default)` permanece sin cambios.
- [ ] `BattleManagerV2` **solo** invoca `PlayAsync` (sin lógica inline adicional).
- [ ] **Done = LockRelease:** la `Task` de `PlayAsync` **solo** completa con `LockReleased(ActionComplete)` (no por timers).
- [ ] **Cancel seguro:** respeta `CancellationToken` → emite `LockReleased(Cancelled)` y limpia Playables/VFX/SFX.

---

### ✅ Arquitectura Clean/Hexagonal + Track Pipeline + Playables
- [ ] Domain/Application **sin referencias a** `UnityEngine` (architecture test en CI).
- [ ] `AnimatorWrapper` + Playables residen en **Infrastructure** (no acoplados a Domain).
- [ ] EventBus, LockManager y Sequencer funcionan como **pipeline temporal**.
- [ ] **Tick ownership único:** un solo `SequencerDriver` llama `Sequencer.Tick()` por frame (único por escena).
- [ ] **Orden de eventos (empate temporal):** `WindowOpen` → `Impact` → `WindowClose` → `ReleaseLock`.

---

### ✅ Timed hits con `CombatClock`
- [ ] `CombatClock.Now` es la **única** fuente de timestamps (prohibido `Time.time` / `deltaTime` para ventanas/juicios).
- [ ] Determinismo validado a **30/60/120 FPS** (snapshot tests verdes).
- [ ] **ToleranceProfile** documentado y aplicado por `toleranceProfileId` (ms para `Perfect/Good/Miss`).

---

### ✅ `ActionCatalog` + `ActionTimeline` 100% data-driven
- [ ] Las acciones se describen **solo** con ScriptableObjects (sin parámetros hardcodeados).
- [ ] **Regla de normalización:** todos los `tNorm` se mapean a la **duración del `AnimTrack.clipRef` principal**.
- [ ] **IDs sin typos:** se generan **constants C#** desde el catálogo (no strings sueltas).
- [ ] `AnimValidator` pasa **sin advertencias** en editor/CI.

---

### ✅ Tooling (validator + preview)
- [ ] `AnimValidator` ejecuta checks de rangos, tags y referencias.
- [ ] **Build breaker:** la build de CI **falla** si `AnimValidator` reporta errores.
- [ ] `TimelinePreviewWindow` permite **scrub** visual y muestra **markers/ventanas/fases/sockets** y **blendIn/Out**.

---

### ✅ Swap y limpieza de legacy
- [ ] `NewAnimOrchestratorAdapter` reemplaza al orquestador anterior **sin modificar** `BattleManagerV2`.
- [ ] La escena principal usa el **nuevo** orquestador; scripts legacy **desconectados**.
- [ ] **Feature flag** temporal para alternar **nuevo/legacy** (rollback rápido).
- [ ] **Tag de repo** antes de borrar legacy; eliminación tras validación final.

---

### 🗂️ Registro de avance (obligatorio)
> Al **iniciar** o **concluir** cualquier tarea/milestone, deja constancia aquí:




---

## 0. Punto de anclaje (contrato actual)

- Mantener la interfaz:
  ```csharp
  public interface IAnimationOrchestrator
  {
      Task PlayAsync(ActionPlaybackRequest req, CancellationToken ct = default);
  }
  ```
- `BattleManagerV2` continua usando:
  ```csharp
  var playbackTask = animOrchestrator?.PlayAsync(request, ct) ?? Task.CompletedTask;
  await playbackTask;
  ```
- Guardrail: ningun PR modifica `BattleManagerV2`.

---

## 1. Arquitectura objetivo

### Principios
- **Desacoplamiento**: animacion oculta tras `IAnimationOrchestrator`; dominio testeable sin Unity; servicios vinculados por `EventBus`.
- **Precision**: `CombatClock` monotono; timed hits por timestamp; markers programados en tiempo absoluto.
- **Data-driven**: cada accion se describe con `ActionTimeline` (ScriptableObject) con tracks `Anim/VFX/SFX/Hitbox/Camera/UI`, markers [0..1], ventanas timed y politicas de lock.
- **Extensibilidad**: policies/strategies para seleccion, binding y locks; Playables para mezcla; tooling y validacion incluidos.

### Pattern stack
- Clean/Hexagonal (Domain, Application, Infrastructure, Composition)
- Pipeline temporal (TrackOps)
- Strategy/Policy (seleccion de timeline, binding, tolerancias, locks)
- Observer (EventBus)
- Adapter/Façade (`IAnimationOrchestrator`, `AnimatorWrapper`)
- Command (TrackOps ejecutables con `Execute`/`Cancel`)

### Capas
- **Domain**: `ActionTimeline`, tracks, markers, phases, timed hit windows, lock specs; contratos `ICombatClock`, `IEventBus`.
- **Application**: `ActionSequencer`, `TimelineCompiler`, servicios (`TimedHitService`, `InputBuffer`, `LockManager`), policies (`ActionSelectionPolicy`, `BindingPolicy`, `LockPolicy`, `ToleranceProfile`), `BattleAnimationFacade`.
- **Infrastructure**: `AnimatorWrapper` (PlayableGraph), routers (`VfxRouter`, `SfxRouter`, `CameraRouter`, `UiRouter`), `UnityEventBus`, `CombatClock`, `AnimatorDirectory`.
- **Composition**: installers/prefabs de escena, `ActionCatalog`, timelines autorables.
- **Ownership del EventBus**: `UnityEventBus` se instancia por escena dentro de Infrastructure; su ciclo de vida termina al descargar la escena (no hay singleton global).

---

## 2. Datos y estructuras

### `ActionTimeline` (ScriptableObject)
```
actionId: string
anim: AnimTrack
vfx: VfxTrack[]
sfx: SfxTrack[]
hitboxes: HitboxTrack[]
camera: CameraTrack[]
ui: UiTrack[]
phases: Phase[] (startup/active/recovery, t0..t1 en [0..1])
timed: TimedHitWindow[] (tag, tStart..tEnd, toleranceProfileId)
locks: LockSpec[] (scope, releaseOn)
markers: Marker[] (tag, tNorm en [0..1])
```

### Tracks minimos
- `AnimTrack`: clipRef o pose, layer, blendIn, blendOut, additive, markers.
- `VfxTrack`: prefabRef, socket, atTag, lifespan.
- `SfxTrack`: eventId, atTag.
- `HitboxTrack`: shapeRef, atTag, duration.
- `CameraTrack`: presetId, atTag.
- `UiTrack`: messageId, atTag, payload opcional.

### Tipos core
- `Marker(tag, tNorm)`
- `Phase(name, t0, t1)`
- `TimedHitWindow(tag, tStart, tEnd, toleranceProfileId)`
- `TimedHitResult(tag, judgment, deltaMs, timestamp)`
- `LockSpec(scope: Actor|Team|Battle, releaseOn: PhaseEnd|LastImpact|ExternalSignal)`

---

## 3. Runtime pipeline (flujo)

1. `BattleManagerV2` genera `ActionPlaybackRequest`.
2. `IAnimationOrchestrator.PlayAsync` lo traduce a `ActionRequest`/`BindingContext`.
3. `ActionCatalog.Resolve` devuelve `ActionTimeline`.
4. `TimelineCompiler` produce TrackOps con tiempos absolutos (segundos).
5. `ActionSequencer.Start` crea PlayableGraph (`AnimatorWrapper`) y agenda operaciones.
6. `ActionSequencer.Tick` consulta `CombatClock.Now` y ejecuta ops maduras.
7. `EventBus` emite `WindowEvt`, `ImpactEvt`, `Vfx/Sfx/Camera/Ui`.
8. `TimedHitService` evalua ventanas + `InputBuffer` y emite `TimedHitResult`.
9. `LockManager` libera segun `LockSpec`; la `Task` finaliza.

Ejemplos de TrackOps:
- `PlayClipOp(...) @ t=0.00`
- `OpenWindowOp(tag, 0.30..0.40)`
- `ImpactSignalOp(tag) @ 0.35`
- `EmitVfxOp(prefab, socket, atTag) @ 0.35`
- `ReleaseLockOp(LastImpact) @ 0.65`

---

## 4. Contratos y APIs

- `IAnimationOrchestrator` (firma arriba).
- `ICombatClock { double Now { get; } double Delta { get; } }`.
- `EventBus` emite:
  - `ActionPhaseEvt { actionId, actorId, phaseName }`
  - `WindowEvt { tag, open|close, tOpen, tClose }`
  - `ImpactEvt { actionId, actorId, tag }`
  - `TimedHitResult`
  - `LockEvt { scope, actorId, reason }`
  - `VfxEvt`, `SfxEvt`, `CameraEvt`, `UiEvt`
- `AnimatorWrapper`:
  - `PlayPose(AnimationClip clip, float blendIn, float blendOut, bool additive)`
  - `SetLayerWeight(int layer, float weight)`
  - `Transform GetSocket(string socketId)`

---

## 5. Adapter: `NewAnimOrchestratorAdapter`

```csharp
public sealed class NewAnimOrchestratorAdapter : MonoBehaviour, IAnimationOrchestrator
{
    [SerializeField] private ActionCatalog catalog;
    [SerializeField] private BattleEventBus eventBus;
    [SerializeField] private CombatClock clock;
    [SerializeField] private LockManager lockMgr;
    [SerializeField] private AnimatorDirectory animDir;

    public async Task PlayAsync(ActionPlaybackRequest req, CancellationToken ct = default)
    {
        var actionId = req.ActionId;
        var binding  = BindingContext.FromPlayback(req);
        var timeline = catalog.Resolve(actionId, binding.ContextTags);

        using var scope = lockMgr.BeginActionScope(binding.SourceActorId, timeline);
        var wrapper = animDir.GetWrapper(binding.SourceActorId);
        var sequencer = new ActionSequencer(timeline, binding, clock, eventBus, wrapper, lockMgr);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnReleased(LockEvt e)
        {
            if (e.Scope == LockScope.Actor &&
                e.ActorId == binding.SourceActorId &&
                e.Reason == LockReleaseReason.ActionComplete)
            {
                tcs.TrySetResult(true);
            }
        }

        try
        {
            eventBus.OnLock += OnReleased;
            sequencer.Start();

            using (ct.Register(() => tcs.TrySetCanceled(ct)))
            {
                await tcs.Task;
            }
        }
        finally
        {
            eventBus.OnLock -= OnReleased;
            sequencer.Dispose();
        }
    }
}
```

Guardrail: la `Task` se resuelve por liberacion de lock (ej. `LastImpact`), nunca por `WaitForSeconds`.

---

## 6. Policies
- `ActionSelectionPolicy`: decide timeline segun `ActionId`, equipamiento o estado.
- `BindingPolicy`: asigna `SourceActorId`, `PrimaryTargetId`, sockets nominales.
- `LockPolicy`: define scope y trigger (`PhaseEnd`, `LastImpact`, `ExternalSignal`).
- `ToleranceProfile`: especifica leniency Perfect/Good/Miss (ms) por ventana/tipo.

---

## 7. Timed hit subsystem

- **InputBuffer**: cola de inputs con timestamp (`CombatClock.Now`), ventana configurable (ej. 180 ms), soporte late cancel.
- **TimedHitService**:
  1. Escucha `WindowEvt`.
  2. Selecciona el input mas cercano dentro de la ventana.
  3. Emite `TimedHitResult { tag, judgment, deltaMs, timestamp }`.
  4. Determinismo garantizado a 30/60/120 FPS (realtime y replay).

---

## 8. Invariantes y validaciones
- 0 ≤ `Marker.tNorm`, `TimedHitWindow.tStart`, `TimedHitWindow.tEnd` ≤ 1.
- Fases ordenadas (startup ≤ active ≤ recovery).
- `TimedHitWindow.tStart < tEnd`.
- Todos los tags referenciados existen en markers.
- `LockSpec.releaseOn` ∈ {`PhaseEnd`, `LastImpact`, `ExternalSignal`}.
- Referencias a clips/prefabs/sockets no nulas.

**AnimValidator** (Editor/CI)
- Recorre `ActionCatalog`.
- Falla build si algun invariante se rompe.

**TimelinePreviewWindow**
- Scrubbing de `tNorm`, markers, ventanas, fases, sockets, blendIn/Out.
- Ayuda al authoring con retro visual.

Presets iniciales: `physical_melee_single_hit`, `magic_bolt_multi_hit`, `ranged_arrow`, `support_buff`.

---

## 9. Plan de migracion

### Objetivos
- `BattleManagerV2` intocable.
- Nuevo orquestador + Playables conviven hasta validar demos.
- Legacy se elimina solo tras checklist completo.

### Mapeo legacy → nuevo
| Legacy (`ActionPlaybackRequest`) | Nuevo sistema |
|----------------------------------|---------------|
| `ActionId`                      | `ActionTimeline.actionId` |
| `Actor/Source`                  | `BindingContext.SourceActorId` |
| `PrimaryTarget`                 | `BindingContext.PrimaryTargetId` |
| `ExtraTargets[]`                | `BindingContext.SecondaryTargets[]` |
| `Callbacks (onStrike/onComplete)` | `ImpactEvt` / `LockReleased` |

### Roadmap recomendado (4 semanas)
1. **Semana 1**: `CombatClock`, `EventBus`, `LockManager`, `ActionCatalog` (2 timelines demo), `AnimValidator`.
2. **Semana 2**: `ActionSequencer`, `AnimatorWrapper` (Playables), routers (`Vfx/Sfx/Camera/Ui`).
3. **Semana 3**: `InputBuffer`, `TimedHitService`, overlay QA, snapshot tests.
4. **Semana 4**: Binding multi-actor, tooling de preview, telemetria, limpieza legacy.

### Checklist primer PR
- CombatClock, EventBus, LockManager operativos.
- ActionCatalog + timelines demo (`basic_attack`, `magic_bolt`).
- AnimatorWrapper (PlayableGraph aditivo).
- ActionSequencer + TimelineCompiler + TrackOps.
- TimedHitService + InputBuffer.
- `NewAnimOrchestratorAdapter` (`PlayAsync`).
- AnimValidator (editor).
- Escena configurada con nuevo orquestador; referencias legacy desconectadas.

### Acceptance criteria
- `BattleManagerV2` compila y corre sin cambios.
- Acciones single/multi-hit completan `Task` via `LockRelease`.
- Timed hits reproducibles a 30/60/120 FPS.
- Agregar habilidad nueva requiere solo SO.
- HUD/Audio/VFX escuchan unicamente `EventBus`.

---

## 10. Testing y telemetria
- **Snapshot tests**: inputs reproducidos y `TimedHitResult` iguales en distintos FPS; `LockRelease` en `LastImpact`.
- **Unit tests**:
  - `TimelineCompiler` genera tiempos correctos.
  - `LockPolicy` respeta `releaseOn`.
  - `ToleranceProfile` clasifica Perfect/Good/Miss segun delta.
- **Telemetria**:
  - Exportar `actionId`, `actorId`, `fps`, `phase`, `markerTag`, `timedHit.judgment`, `deltaMs`.
- **Performance objetivo**: `ActionSequencer.Tick()` ≤ 0.2 ms por actor a 60 FPS, sin asignaciones GC (verificado con Profiler en escenario pico).

---

## 11. Guardrails de Arquitectura
- Ningun `MonoBehaviour` fuera de Infrastructure puede depender de Application/Domain.
- `Domain` y `Application` no referencian `UnityEngine`.
- `BattleManagerV2` es intocable; cualquier cambio requiere aprobacion de arquitectura.
- `IAnimationOrchestrator` es la unica interfaz autorizada entre `BattleManagerV2` y animaciones.
- Todo nuevo `ActionTimeline` debe pasar `AnimValidator` sin errores antes de mergear.

---

## 12. Estructura de carpetas
- `BattleV2/Domain/...`
- `BattleV2/Application/...`
- `BattleV2/Infrastructure/...`
- `BattleV2/Authoring/...`
- `BattleV2/Tests/...`

---

## 13. Ejemplos de timelines

### `basic_attack`
- Phases: startup (0.00–0.25), active (0.25–0.55), recovery (0.55–1.00)
- Markers: windup (0.15), impact1 (0.35)
- Timed windows: impact1 (0.32–0.38) → profile `default_melee`
- LockSpec: scope `Actor`, releaseOn `LastImpact`

### `magic_bolt`
- Phases: startup (0.00–0.30), active (0.30–0.80), recovery (0.80–1.00)
- Markers: cast (0.28), impact1 (0.50), impact2 (0.70)
- Timed windows: impact1 (0.47–0.53), impact2 (0.67–0.73) → `default_magic`
- LockSpec: scope `Actor`, releaseOn `LastImpact`

---

## 14. Riesgos y mitigaciones
- Complejidad de Playables → encapsular en `AnimatorWrapper` y cubrir con tests.
- Datos invalidos → AnimValidator + ruptura de build.
- Desfase de tiempos → uso exclusivo de `CombatClock`.
- Coste de authoring → presets + tooling de preview.

---

## 15. Highlights (portafolio)
- Arquitectura Clean/Hexagonal con adapters Unity.
- Pipeline temporal determinista (TrackOps).
- Playables para mezcla granular y overlays aditivos.
- Timed hit desacoplado, reproducible y medible.
- Data-driven real: nuevas habilidades sin tocar codigo.
- Tooling (validator + preview) listo para produccion.

---

## 16. Contratos Duros
- **Normalizacion temporal:** todos los `tNorm` se mapean sobre la duracion principal del `AnimTrack.clipRef`. No se permite otro origen de tiempo.
- **Orden de eventos garantizado:** ante empate temporal el orden es: (1) `WindowOpen`, (2) `Impact`, (3) `WindowClose`, (4) `ReleaseLock(ActionComplete)`.
- **Concurrencia por actor:** cada actor mantiene un unico `ActionSequencer` activo. Un nuevo `PlayAsync` sobre el mismo actor cancela el anterior y emite `LockReleased(Cancelled)`.
- **Cancelacion:** `Cancel()` detiene el `PlayableGraph`, emite `LockReleased(Cancelled)`, corta VFX persistentes y silencia SFX activos.
- **Politica de errores:** si un asset falla en runtime (clip, socket, prefab), se ejecuta `FallbackPose`, se loguea advertencia y el lock se libera igualmente para no bloquear el flujo.
- **Tick ownership:** solo un `SequencerDriver` (unico por escena) llama `ActionSequencer.Tick()` por frame. Ningun otro sistema puede invocar `Tick` manualmente.

---

## 17. Folder y versionamiento
- Nombre del archivo: `JRPG_AnimationSystem_v1.0.0_LOCKED.md`.
- Cualquier cambio futuro se tramita como RFC aprobado por arquitectura y versiona un nuevo archivo `*_vX.Y.Z_LOCKED.md`.

---

## 18. Milestones compactos

### Regla de constancia
Al iniciar o concluir cualquier tarea o milestone se debe registrar en **Registro de avance** con:
- Fecha `YYYY-MM-DD`
- Nombre o iniciales
- Tarea o milestone afectado
- Estado (`Iniciado` / `Completado` / `Bloqueado`)
- Nota breve (1–2 líneas)

### 1️⃣ Contrato base y arquitectura
**Objetivo**: establecer el esqueleto base sin modificar `BattleManagerV2`.
- Definir `IAnimationOrchestrator (PlayAsync)`.
- Implementar `CombatClock` monotono.
- Implementar `EventBus` básico (Phase/Impact/Window/Lock).
- Validar que `BattleManagerV2` invoque `PlayAsync` sin romperse.
- Agregar guardrails (manager intocable, CI check).
➡ Resultado: flujo funcional mínimo; contrato base cerrado.

### 2️⃣ Datos autorables
**Objetivo**: permitir que acciones se definan solo con datos.
- Crear `ActionTimeline` SO con tracks y fases.
- Crear `ActionCatalog` que indexe timelines.
- Implementar `AnimValidator` (rangos y tags).
- Crear timelines demo (`basic_attack`, `magic_bolt`).
- Validar catálogo sin errores.
➡ Resultado: acciones 100% data-driven y validadas automáticamente.

### 3️⃣ Núcleo de ejecución
**Objetivo**: ejecutar timelines de forma determinista.
- `TimelineCompiler`: markers [0..1] → segundos reales.
- `ActionSequencer`: controla fases y ticks.
- Implementar `LockManager` simple.
- Emitir eventos `Window`, `Impact`, `Release`.
- Probar ciclo completo con logs.
➡ Resultado: ejecución temporal reproducible y lista para integrar visual.

### 4️⃣ Integración visual
**Objetivo**: conectar gráficos, VFX y audio al pipeline.
- `AnimatorWrapper` con Playables.
- Routers (`VFX`, `SFX`, `Camera`, `UI`) escuchando `EventBus`.
- Implementar `NewAnimOrchestratorAdapter`.
- Sustituir orquestador viejo (sin borrarlo aún).
- Ejecutar `basic_attack` y `magic_bolt` con animación visible.
➡ Resultado: animaciones funcionales y orquestadas sin tocar el manager.

### 5️⃣ Timed hit y precisión
**Objetivo**: feedback exacto y reproducible en todos los FPS.
- Crear `InputBuffer` (inputs timestamped).
- `TimedHitService`: evalúa ventanas y emite resultados.
- Mostrar feedback en HUD (Perfect/Good/Miss).
- Validar determinismo 30/60/120 FPS.
- Sincronizar feedback con `EventBus`.
➡ Resultado: timed hits confiables y sincronizados con animación.

### 6️⃣ Tooling, tests y limpieza
**Objetivo**: dejar el sistema robusto y sin dependencias legacy.
- `TimelinePreviewWindow` (scrubbing visual).
- Snapshot tests y telemetría (`deltaMs`, `judgment`).
- Validar `AnimValidator` en CI.
- Eliminar scripts y prefabs antiguos.
- Confirmar build limpia (`_LOCKED`).
➡ Resultado: build final estable, documentada y mantenible.

---

## Registro de avance
- 2025-11-02 | D.M. | Milestone 1 – Contrato base y arquitectura | Iniciado | Preparando interfaces base.
- 2025-11-03 | D.M. | Milestone 1 – Contrato base y arquitectura | Completado | Contrato PlayAsync validado en BattleManagerV2.
z
- 2025-11-04 | Codex | Milestone 5 - Timed hit y precisión | Iniciado | Perfil de tolerancias data-driven y parsing de payload.
