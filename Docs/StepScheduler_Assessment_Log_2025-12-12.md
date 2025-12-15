# StepScheduler Assessment Log — vNext (2025-12-12)

**Estado:** investigación / assessment (sin fixes).  
**Objetivo:** documentar con evidencia trazable por qué aparece “tween soup”, qué piezas están redundantes/sueltas, y qué guardrails deben existir para un StepScheduler v2 (y v2.1 para timelines/timed hit), manteniendo al StepScheduler como orquestador principal y a los demás sistemas como listeners.

**Implementation plan:** `DOCS/StepScheduler_ImplementationPlan_v2.md`

---

## 0) Componentes clave y rol esperado
- **Orquestador:** `StepScheduler` (recipes → groups → steps, lifecycle).
- **Authoring:** `StepRecipeAsset` (steps/groups/params).
- **Composition root:** `AnimationSystemInstaller` (wiring de executors/observers/catalog).
- **Listeners (no deberían orquestar):** VFX/SFX/UI/Camera, Motion/Tween, Telemetry/metrics.
- **Síntoma:** `RecipeTweenObserver` actúa como movement orchestrator de facto (no solo listener).

---

## 1) Evidencia por claim (trazable)
- **Claim:** `TweenExecutor` es NO-OP.  
  **Evidence:** `Assets/Scripts/BattleV2/AnimationSystem/Execution/Runtime/Executors/TweenExecutor.cs`, método `ExecuteAsync` → `return Task.CompletedTask;`.  
  **Impact:** locomoción real ocurre fuera del scheduler.  
  **Re-verificar:** abrir el método y confirmar que no crea tween ni registra tarea.

- **Claim:** los group runners pueden continuar off-main-thread.  
  **Evidence:** `SequentialGroupRunner.ExecuteAsync` y `ParallelGroupRunner.ExecuteAsync` usan `ConfigureAwait(false)` (en `Assets/.../Core/GroupRunners`).  
  **Impact:** `NotifyObservers` puede ejecutarse en threadpool; Unity/DOTween desde observers se vuelve no determinista.  
  **Re-verificar:** buscar `ConfigureAwait(false)` en ambos runners.

- **Claim:** conflictos se registran solo por `executorId`.  
  **Evidence:** `ActiveExecutionRegistry` (`Assets/.../Core/Conflict/ActiveExecutionRegistry.cs`) usa `Dictionary<string, ActiveExecution> activeExecutions`; clave = `executorId`.  
  **Impact:** no hay modelado de recurso/actor; no evita dos locomociones del mismo actor; no ve tweens externos.  
  **Re-verificar:** leer `Register`, `ResolveConflictAsync`.

- **Claim:** `run_up_target` se dispara por dos rutas.  
  **Evidence:** `RecipeTweenObserver.OnRecipeStarted` (case `run_up_target`) y `OnGroupStarted` (group.Id == `run_up_target`) en `Assets/.../Execution/Runtime/Observers/RecipeTweenObserver.cs`.  
  **Impact:** doble trigger / timing distinto para el mismo intent.  
  **Re-verificar:** leer ambos métodos.

- **Claim:** root motion y variants son tocados por el observer.  
  **Evidence:** `RecipeTweenObserver.SetRootMotion/RestoreRootMotion` + `ResetVariantScope`/`ConsumeCommand` en `OnRecipeStarted` (mismo archivo).  
  **Impact:** doble autoridad con `AnimatorWrapper` sobre pose/root motion.  
  **Re-verificar:** inspeccionar llamadas a `AnimatorWrapper`.

- **Claim:** kill incompleto de tweens.  
  **Evidence:** `RecipeTweenObserver.KillTween` solo mata `activeTween`; no `DOKill(target)`.  
  **Impact:** puede haber tweens vivos en `tweenTarget` creados por otros scripts/sequences.  
  **Re-verificar:** leer `KillTween`.

- **Claim:** dualidad de fuentes de recipe.  
  **Evidence:** `StepScheduler.recipeRegistry` + `ActionRecipeCatalog` en installer; se registran recipes en ambos.  
  **Impact:** dos rutas de lookup; riesgo de divergencia.  
  **Re-verificar:** `AnimationSystemInstaller.BuildRecipeCatalog` y `RegisterInspectorRecipes`.

---

## 2) Causas probables de los bugs (solapaciones/teleports)
- Locomoción fuera del canal oficial → scheduler no arbitra (`ActiveExecutionRegistry` no ve esos tweens).
- Kill parcial (`activeTween` solamente) → quedan tweens vivos sobre el mismo target/jerarquía.
- Doble señal `run_up_target` (recipe vs group) → triggers desincronizados.
- Doble autoridad con `AnimatorWrapper` (root motion/pose/variants).
- Continuations off-main-thread por `ConfigureAwait(false)` → callbacks Unity/DOTween fuera del main thread.
- Start positions a mitad de tránsito: nuevo tween arranca desde la posición actual de un tween previo o de un snap.

---

## 3) Contratos/Especificaciones mínimas (v2)
- **Locomotion Resource:** `Locomotion(actorId)` (opcional `bindingId`/`Transform instanceId` si hay múltiples roots). Solo un dueño activo por recurso.
- **Trackeable:** toda ejecución de locomoción/timeline devuelve `Task/handle` que respeta `CancellationToken`, y se registra para conflicto (Wait/Cancel/Skip) y await de completion.
- **Commit Point:** `LastCommittedLocalPos` (y rot/scale si aplica) se actualiza solo en `onComplete/onCancel` del movimiento. Mientras `IsInTransit=true`, no se usa `transform.localPosition` como fuente de verdad.
- **Source of Truth (MotionState):** mantener estado lógico del movimiento (target, intent, isInTransit, lastCommitted) separado del `Transform`.
- **Conflicto por recurso:** arbitraje por Actor+Resource (Locomotion/Pose/Timeline/Camera/UI/FX), no solo por `executorId`.
- **Main-thread determinism:** ninguna operación Unity-touching se ejecuta off-main-thread; observer/step notifications deben correr en main thread (quitar `ConfigureAwait(false)` o hop con `IMainThreadInvoker`).

---

## 4) Guardrails de locomoción (deberían estar en el núcleo, no como addendum)
1) **Lock de `Locomotion(actor)`**: cualquier movimiento (run_up, run_up_target, run_back, snap) adquiere el recurso. Si ocupado: `WaitForCompletion` o `CancelRunning`; nunca arrancar encima.  
2) **Movimiento trackeable/cancelable**: cada tween/timeline de locomoción debe devolver handle `Task` cancelable; el scheduler lo registra por recurso.  
3) **Commit points**: actualizar `LastCommittedLocalPos` solo en `onComplete/onCancel`; opcional settle corto (ms) para pausa visual.  
4) **Kill total por recurso**: al cancelar/reemplazar locomoción, matar todos los tweens del target/recurso (no solo `activeTween`).  
5) **Una sola señal de “move-to-target”**: eliminar duplicidad recipe/group para `run_up_target`; un único intent dispara el movimiento.  
6) **Invariantes de espacio (coord):** todos los moves se calculan en el espacio de `tweenTarget.parent`; los targets (home/spotlight/targetSpot) se convierten a local al inicio del movimiento; no usar localPosition como “latest known” si `IsInTransit=true`.  
7) **Main-thread obligatorio:** scheduler/observers que toquen Unity corren en main thread.

---

## 5) Flujo deseado sin overlaps (ejemplo de acción física)
1. Turn start: `Locomotion.MoveToSpotlight` (Lock Locomotion(actor), WaitForCompletion)  
2. UI/Camera en paralelo (recursos distintos)  
3. Acción: `Locomotion.MoveToTarget` (Lock, WaitForCompletion)  
4. Payload: anim/timeline/FX/SFX/TimedHit (Pose/Timeline/FX con policies)  
5. `Locomotion.ReturnHome` (Lock, WaitForCompletion)

> Discreto no es “otro frame”, es “await con ownership + commit points”.

---

## 6) StepScheduler v2.1 (timed hit + timelines: cancel vs wait)
- Timelines deben ser ejecuciones trackeables/cancelables (honran CancellationToken).  
- Timed hit produce outcome (success/fail/timeout) → `gate.on` branch/abort.  
- Policies por recurso Timeline/Pose: `CancelRunning` (snappy) o `WaitForCompletion` (cinemático).  
- Acceptance: scheduler puede cancelar/esperar timelines de forma determinista.

---

## 7) Research plan (Codex) — entregables esperados
- **R1:** Lista de `ConfigureAwait(false)` y mapa de cómo llegan callbacks a observers; conclusión sobre main-thread.  
- **R2:** Mapa “tween oficial vs paralelo”: `TweenExecutor` NO-OP + todas las llamadas DOTween externas.  
- **R3:** Auditoría `ActiveExecutionRegistry`: clave, políticas, si cruza actores.  
- **R4:** Mapa de writes (formato esperado):  
  - quién escribe `tweenTarget.localPosition` / `transform.position`  
  - quién toca `Animator.applyRootMotion`  
  - quién llama DOTween sobre el target  
  - quién hace snaps/ResetToFallback  
  - para cada write: callsite + condición + supuesto de hilo.  
- **R5:** Ownership de `AnimatorWrapper`: qué resetea y cuándo; cruce con RecipeTweenObserver.  
- **R6:** Explicar duplicidad `run_up_target` (recipe vs group) con flujo real.  
- **R7:** Inventario timelines (KS1, TimedHitStepExecutor, routers) y dónde vive la cancelación.

---

## 8) ROI Triage y harness de aceptación
- **Repro Case 1 (A/B):** turn → run_up (spotlight) → run_up_target → payload → run_back.  
  **Expected:** 0 overlaps (conteo de tweens por target), 0 snaps mid-flight, thread main siempre, commits ordenados en MotionState.  
- **Acceptance gates:**  
  - Nunca hay 2 locomotions activas por actor.  
  - Ningún observer off-thread.  
  - No hay doble trigger del intent move-to-target.  
  - MotionState commit ocurre solo en complete/cancel.
- **A/B:**  
  - Deshabilitar `RecipeTweenObserver` vs ruta de executor (para aislar doble sistema).  
  - Silenciar una ruta de `run_up_target` (group o recipe) para ver doble trigger.  
  - Instrumentar conteo de tweens activos en `tweenTarget` durante run_up/run_back.

---

## 9) Checklist mínima (v2)
- [ ] Callbacks Unity-touching siempre en main thread.  
- [ ] Lock por `Locomotion(actor)` (y opcional `Pose(actor)`, `Timeline(actor)`).  
- [ ] Ejecuciones trackeables: tween/timeline devuelve handle cancelable y se registra.  
- [ ] Intent explícito (no magic strings) para locomoción.  
- [ ] Una fuente de verdad de recipes (o puente contractual).  
- [ ] Invariantes de espacio locales + commit points + kill total por recurso.

---

### Apéndice (archivos/símbolos a auditar con evidencia dura)
- `ActiveExecutionRegistry`
- `SequentialGroupRunner`, `ParallelGroupRunner`
- `TweenExecutor`
- `StepSchedulerRecipeExecutor`
- `AnimatorWrapper`
- `RecipeTweenObserver`
- `TimedHitStepExecutor` + cualquier runner/timeline bridge relacionado

--- 
