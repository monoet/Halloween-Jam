# StepScheduler — Implementation Plan v2 (y v2.1) (2025-12-12)

Plan alineado al assessment (`DOCS/StepScheduler_Assessment_Log_2025-12-12.md`). Objetivo: atacar los hard bugs de tween overlaps/teleports con guardrails en backend, manteniendo DOTween como motor de movimiento y evitando tocar escenas salvo lo mínimo.

---

## 0) Riesgos reales (para no sorprendernos)

- **Threading:** neutralizar `ConfigureAwait(false)` puede cambiar timings “implícitos” que hoy funcionan accidentalmente. Debe hacerse con logs + harness desde el primer chunk.
- **Conflictos por recurso:** migrar de “executorId” a “resource key” puede tocar varias rutas. Propuesta: **back-compat** (introducir `ResourceKey` sin romper lo viejo) y arrancar solo por **Locomotion**.

Opciones de estrategia:

- **Ruta A (mínima):** mantener `RecipeTweenObserver` en prefabs, pero que deje de hacer DOTween directo → llama a `MotionService` (backend) que lockea/trackea/commitea.
- **Ruta B (más “pura”):** implementar `MotionExecutor` real y mover locomoción a steps oficiales (requiere tocar recipe assets; no escenas).  
  Recomendación: **Ruta A primero** por ROI + mínimo cableado.

---

## 1) Target Architecture (patrones a imponer)

### 1.1 Contratos (SOLID/KISS/YAGNI)

- `StepScheduler` **solo orquesta** (no entiende `run_up/run_back`).
- Movimiento = **canal** `Locomotion(actor,binding)` con **resource lock**.
- Toda ejecución Unity-touching es **main thread** (regla absoluta).
- Toda locomoción/timeline es **trackeable**: devuelve `IMotionHandle`/`Task` cancelable + completion.
- **Commit points:** la “posición verdad” vive en `MotionState`, no en `transform.localPosition` durante tránsito.

### 1.2 Módulos (limpio y debuggable)

- `BattleV2.AnimationSystem.Execution.Runtime.Core`
  - `ResourceKey`, `IResourceLockRegistry`, `ConflictPolicy`
- `BattleV2.AnimationSystem.Motion`
  - `MotionService`, `MotionStateStore`, `MotionHandle`, `MotionIntents`, `MotionTargetsResolver`
- `BattleV2.Diagnostics`
  - toggles + logger con tags `[DEBUG-XX###]`

---

## 2) Debug Logs (infra + glosario) — Chunk 0 (testeable)

Meta: antes de cambiar comportamiento, tener visibilidad + kill-switch.

### 2.1 Toggle global (sin escena)

- `BattleDebugSettings` (ScriptableObject opcional autocargable por `Resources`/Addressables). Si no se quieren assets: `static` + `PlayerPrefs`.
- API:
  - `BattleDebug.IsEnabled("MS")`
  - `BattleDebug.Log("MS", 12, "...")` → `[DEBUG-MS012] ...`

### 2.2 Glosario de tags (mínimo útil)

- `[DEBUG-SS001]` StepScheduler: “observer callback thread = X”
- `[DEBUG-GR001]` GroupRunner: “thread hop detected”
- `[DEBUG-AR001]` Resource/Registry: “acquire/release resourceKey=… policy=…”
- `[DEBUG-MS001]` MotionService: “Start intent=… resourceKey=…”
- `[DEBUG-MS002]` MotionService: “Cancel previous motion resourceKey=… reason=…”
- `[DEBUG-MS003]` MotionService: “Commit position local=… anchor=…”
- `[DEBUG-MS004]` MotionService: “DOTween active tweens on target = N”
- `[DEBUG-RTO001]` RecipeTweenObserver: “received recipeId/groupId=… chosen path=…”

Test en Unity (Chunk 0):

- Repro Case 1 y confirmar logs con toggles ON/OFF.
- Si aparece callback off-main-thread, se vuelve prioridad del Chunk 1.

---

## 3) Main-thread determinism — Chunk 1 (bloqueante, testeable)

Meta: ningún observer/executor que toque Unity puede correr off-thread.

Implementación (mínimo backend):

- Reusar `IMainThreadInvoker` (ya existe wiring en installer); si hay gaps, introducir interfaz y adaptador.
- En `StepScheduler.NotifyObservers(...)`: garantizar ejecución en main thread vía invoker.
- En runners (`SequentialGroupRunner`, `ParallelGroupRunner`):
  - Opción 1 (más segura): remover `ConfigureAwait(false)`.
  - Opción 2 (menos invasiva): dejarlo, pero *toda* ruta Unity-touching hace hop al main thread.

Acceptance (Chunk 1):

- `[DEBUG-SS001]` siempre reporta main thread.
- No hay errores “fantasma” al spamear turn/action.

---

## 4) Conflictos por recurso (sin romper lo viejo) — Chunk 2 (testeable)

Meta: habilitar locking por `ResourceKey` sin reescribir todo.

Implementación:

- Crear `struct ResourceKey { string Channel; int ActorId; int BindingId; }`
- `ActiveExecutionRegistry`:
  - Mantener diccionario actual por `executorId` (legacy).
  - Agregar diccionario por `ResourceKey`.
  - Agregar APIs:
    - `AcquireAsync(ResourceKey key, ConflictPolicy policy, CancellationToken ct)`
    - `Release(ResourceKey key, handleId)`
- Políticas mínimas:
  - `WaitForCompletion`
  - `CancelRunning`
  - `SkipIfRunning` (opcional)

Test (sin Unity):

- Unit tests puros: acquire/release, cancel, wait, skip.

---

## 5) MotionService + MotionState (DOTween sigue) — Chunk 3 (core del bug, testeable)

Meta: un solo dueño del movimiento, kill total, commit points, sin hardcodear coords.

### 5.1 MotionState (source of truth)

Por actor/binding:

- `IsInTransit`
- `LastCommittedLocalPos/Rot/Scale`
- `CurrentAnchor` (Home/Spotlight/Target/Custom)
- `LastIntent`
- `ActiveHandleId`

### 5.2 MotionService (DOTween debajo, pero gobernado)

API ejemplo:

- `Task MoveToAsync(MotionRequest req, CancellationToken ct)`
- `Task SettleAsync(ResourceKey key, TimeSpan duration)` (opcional; ms, no “1 frame” por default)
- `void Cancel(ResourceKey key)`

Reglas:

- Antes de iniciar: `Acquire Locomotion(actor,binding)`.
- **Kill total** por target/recurso: `DOTween.Kill(tweenTarget)` / `tweenTarget.DOKill(false)` (según convenga) para garantizar no queden tweens afectando el target.
- Calcular targets desde anchors al momento de start:
  - `homeAnchor` (o snapshot inicial)
  - `spotlight`
  - `targetSpot`
  - siempre convertir a local usando `parent.InverseTransformPoint(world)`
- Commit:
  - en `OnComplete` y en cancel/kill: actualizar `LastCommittedLocalPos` (ideal: a la posición final alcanzada) + `IsInTransit=false`.
  - `Release` del lock **después del commit**.
- “Pausa visual”:
  - No como hack general.
  - Sí como paso explícito (`motion.settle`) si diseño lo requiere, y solo después del commit.

Acceptance (Chunk 3):

- `[DEBUG-MS004]` nunca reporta >1 tween de locomoción vivo por actor/binding.
- Overlaps/teleports bajan a ~0 incluso con inputs agresivos.

---

## 6) Migrar RecipeTweenObserver sin tocar escena — Chunk 4 (testeable)

Meta: cero cambios en prefabs/escenas; solo cambiar código.

Cambios:

- `RecipeTweenObserver` deja de crear DOTween directo para locomoción.
- En `OnRecipeStarted`: traducir `recipeId/groupId` a `MotionIntent` y llamar a `MotionService`.
- Eliminar `KillTween(activeTween)` (ya no aplica para locomoción).
- Unificar señal `run_up_target`:
  - En este chunk: si llega por group, ignorar (o viceversa), pero que sea una sola ruta + log si llega duplicado.

Acceptance (Chunk 4):

- Ya no existe DOTween directo fuera de `MotionService` para locomoción.
- Duplicidad `run_up_target` se reporta y queda neutralizada.

---

## 7) Normalizar recipes sin magic strings (sin escena; assets sí) — Chunk 5 (opcional v2.0+ o v2.1)

Meta: locomoción no depende de `recipe.Id == "run_up"`.

Enfoque mínimo:

- En `StepRecipeAsset`, agregar params:
  - `intent=Locomotion.MoveToTarget`
  - `socket=SpotlightDestination`
  - `binding=MotionRoot`
- `RecipeTweenObserver`/MotionExecutor lee params, no strings.

---

## 8) v2.1 Timelines + timed hit (cuando locomotion ya esté estable)

Chunks sugeridos:

- Chunk 6: `TimelineExecutor` trackeable/cancelable (Task + CancellationToken).
- Chunk 7: policies “Cancel on confirm” vs “Wait to finish” por recurso `Timeline(actor)`.
- Chunk 8: branches por outcome de timed hit (success/fail/timeout) sin hacks.

---

## 9) Plan de pruebas por chunk (siempre parar y testear)

Repro Case Base:

1. Turn start → MoveToSpotlight
2. Selección → MoveToTarget
3. Payload (anim/timed hit si aplica)
4. ReturnHome

Gates:

- Nunca 2 locomotions activas por actor/binding.
- Nunca observer off-thread.
- Commit ocurre solo en complete/cancel.
- No hay doble trigger move-to-target.

---

## 10) Documentación de cableado (mínima)

Meta: cero escena.

Solo si en el futuro quieres un `HomeAnchor` explícito:

- Opcional: agregar `Transform homeAnchor` al prefab (no requerido si capturas home en Awake).
- Si se agrega, documentar:
  - GameObject: `MotionRoot/HomeAnchor`
  - Campo: `RecipeTweenObserver.homeAnchor = ...`

