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
- **Action/Turn envelope (determinismo):** el “ReturnHome/run_back” no debe depender de cada acción. La orquestación debe garantizar un “post-step” (finally) que regrese a home salvo override explícito.

### 1.2 Módulos (limpio y debuggable)

- `BattleV2.AnimationSystem.Execution.Runtime.Core`
  - `ResourceKey`, `IResourceLockRegistry`, `ConflictPolicy`
- `BattleV2.AnimationSystem.Motion`
  - `MotionService`, `MotionStateStore`, `MotionHandle`, `MotionIntents`, `MotionTargetsResolver`
- `BattleV2.Diagnostics`
  - toggles + logger con tags `[DEBUG-XX###]`

### 1.3 Tres capas (fronteras estrictas, no se mezclan)

Para no volver al soup, mantenemos 3 capas separadas:

1) **StepScheduler (core):** orquesta + lifecycle + observers. No sabe de `run_up`.
2) **Motion (backend):** `MotionService` + lock + commit + kill total. Aqui vive la verdad del movimiento.
3) **Orchestration (arriba del scheduler):** ActionEnvelope/RecipeChain = "1 accion -> N recipes secuenciales".

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

## 5.5 Action/Turn Envelope: ReturnHome garantizado — Chunk 3.5 (ROI alto, sin escena)

Problema que resuelve:

- Hoy “run_back” vive como detalle de cada action/spell. Si una acción (p.ej. spell) no lo incluye, el actor nunca regresa → se percibe como bug en escena.

Objetivo v2:

- Mover la responsabilidad de “volver a home” al orquestador (envoltura de acción/turno), no a cada acción.
- El payload de la acción solo declara intención (ApproachTarget / CastFromHome / ReturnHome override), no “cómo” volver.

Implementación mínima (backend, compatible con Ruta A):

- Introducir un **ActionEnvelope** (o “plan maestro”) que encapsula:
  1) (si aplica) `MoveToSpotlight`
  2) (si aplica) `MoveToTarget`
  3) `Payload` (spell/melee/timed hit/etc.)
  4) `ReturnHome` **SIEMPRE**, como post-step garantizado (finally), salvo override explícito
- Con `MotionService`:
  - API sugerida: `Task EnsureReturnHomeAsync(ResourceKey key, MotionTarget home, CancellationToken ct)`
  - Regla: si el actor no está en home y no está en tránsito, adquirir lock de Locomotion y ejecutar return determinista.

Acceptance (Chunk 3.5):

- Spells y acciones sin run_back explícito igual regresan a home.
- “ReturnHome” no genera overlaps (pasa por lock + kill total + commit points).

Test Unity:

- Ejecutar spells repetidos (sin modificar recipes) y verificar retorno consistente a home.

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

## Estado real (auditable) + checklists por chunk

> Esta secciA3n es la fuente de verdad para "en quAc chunk vamos" y quAc falta.

### Chunk 0 — Debug logs / interruptor (✅)

- [x] Existe un switch inspector (`BattleDebugTogglesBehaviour`) para encender canales `EG/MS/RTO/SS` sin tocar cA3digo.
- [x] `BattleDebug` no llama `PlayerPrefs.GetInt` off-main-thread (cache en memoria; persist solo en main thread).

**Logs a capturar para cerrar Chunk 0:**
- [x] `SS`: `[DEBUG-SS001] NotifyObservers threadId=1 isMain=True`
- [x] `EG`: `[DEBUG-EG010] awaiting ...` y `[DEBUG-EG011] scope complete ...`
- [x] `MS`: `[DEBUG-MS004] Start ...` y `[DEBUG-MS003] Commit ...`

### Chunk 1 — Main-thread determinism (✅)

- [x] Removido `ConfigureAwait(false)` de group runners (evitar continuations off-thread dentro del scheduler).
- [x] Observers que tocan Unity (p.ej. root motion) se ejecutan en main thread via invoker.

### Chunk 2/3/4 — Ruta A (Motion + Gate + wiring) (✅)

- [x] `MotionService` (DOTween) con kill total + commit points + single-owner por `ResourceKey`.
- [x] `ExternalBarrierGate` (BeginGroup + Register + AwaitGroup/All) evita que payload arranque mientras la locomociA3n sigue viva.
- [x] Fix del bug real del "soup": **registrar barreras en `OnGroupStarted`** (no en `OnRecipeStarted`) para que el scope no quede vacAcO y no se borre al entrar al group.

### Chunk 5 — Authoring real (assets) + envelope formal (🚧)

**Objetivo:** dejar de depender de recipes inline/fallbacks y de la feature flag `APF`.

#### 5.1 Recipes v2 en Resources (locomociA3n-only)

- [ ] Generar assets en `Assets/Resources/Battle/StepRecipesV2/`:
  - [ ] `move_to_spotlight.asset`
  - [ ] `move_to_target.asset`
  - [ ] `return_home.asset`
- [x] Runtime auto-registra recipes desde `Resources/Battle/StepRecipesV2` (sin scene wiring) en `AnimationSystemInstaller`.

**C3mo generar (Editor):**
- Menu: `Battle/Animation/StepScheduler v2/Generate Locomotion Recipes (Resources)`

#### 5.2 Envelope (sin APF / reglas claras)

- [x] `basic_attack` inyecta `move_to_target` (melee approach) sin depender de `BattleDebug.IsEnabled("APF")`.
- [x] `ReturnHome` se garantiza en `finally` (prefiere recipe `return_home`, fallback a `run_back`).

#### 5.3 Observer compat (ids nuevos)

- [x] `RecipeTweenObserver` reconoce groups `move_to_spotlight` y `return_home` (ademAs de `run_up/run_back/move_to_target`).

---

## Path B (a futuro, no v2.1)

**Meta:** locomociA3n como step oficial (executor), no como side-effect del observer.

- [ ] Crear `MotionExecutor` (StepScheduler executor) que consume params/intent y llama `MotionService` devolviendo `Task` trackeable.
- [ ] Mover locomociA3n de `RecipeTweenObserver` a steps `executorId="motion"` dentro de recipes.
- [ ] Reducir `RecipeTweenObserver` a: AnimatorWrapper commands + (opcional) debug/telemetry.

## 10) Documentación de cableado (mínima)

Meta: cero escena.

Solo si en el futuro quieres un `HomeAnchor` explícito:

- Opcional: agregar `Transform homeAnchor` al prefab (no requerido si capturas home en Awake).
- Si se agrega, documentar:
  - GameObject: `MotionRoot/HomeAnchor`
  - Campo: `RecipeTweenObserver.homeAnchor = ...`

---

## Insert (sin reordenar): Chunk 4.5 — ActionEnvelope / RecipeChain (1 accion -> N recipes)

Para mantener las 3 capas separadas, este chunk vive **arriba** del `StepScheduler` (orchestration) y se inserta **entre**:

- `## 5.5 Action/Turn Envelope: ReturnHome garantizado — Chunk 3.5`
- `## 6) Migrar RecipeTweenObserver sin tocar escena — Chunk 4`

Entrega:

- `ActionPlanRunner` / `RecipeChainRunner` (backend) que ejecuta **N recipes secuenciales** por accion con `await scheduler.ExecuteAsync(...)`.
- Logs `DEBUG-AP###` para ver el plan y la duracion por node.

Primer punto donde se puede probar `run_to_target` como recipe separado:

- En este chunk: Node A `move_to_target` (inline recipe en codigo, locomotion-only) -> Node B `attack_payload` -> Node C `return_home`.
- Este es el primer punto porque ya existe "1 accion -> N recipes"; antes solo se puede probar `run_to_target` como group dentro de un recipe.

---

## Chunk Checklist (auditable)

Regla: no se considera "cerrado" un chunk hasta que pase sus gates en Unity y se vea el set de logs esperado.

Formato:
- **Estado:** ✅ / 🚧 / ⛔
- **Feature flags:** lista de canales/toggles relevantes
- **Acceptance gates (Unity):** 3–6 condiciones verificables
- **Logs esperados:** tags `[DEBUG-XX###]` que deben aparecer (en orden si aplica)
- **Commits/PR:** hash(s) o "uncommitted"
- **Repro harness:** pasos exactos para repro

### Chunk 0 — Debug logs / toggles
- **Estado:** 🚧
- **Feature flags:** `EG`, `MS`, `RTO`, `SS`, `AP` (logs), `APF` (feature)
- **Acceptance gates (Unity):**
  - No crashea por `PlayerPrefs` off-main-thread al leer toggles.
  - Activar/desactivar un channel cambia el volumen de logs de ese channel.
  - Logs no generan exceptions ni allocations absurdas en spam (observación cualitativa).
- **Logs esperados:**
  - `EG`: `[DEBUG-EG###]` register/await/complete/timeout
  - `MS`: `[DEBUG-MS###]` start/cancel/commit
  - `SS`: `[DEBUG-SS###]` thread info de callbacks (si está habilitado)
- **Commits/PR:** uncommitted (validado en Unity con toggles ON/OFF)
- **Repro harness:**
  - En runtime: `BattleDebug.SetEnabled("EG", true)` y confirmar que aparecen logs.

### Chunk 1 — Main-thread determinism
- **Estado:** 🚧
- **Feature flags:** `SS`, `EG`
- **Acceptance gates (Unity):**
  - Ningún callback que toque Unity (observers/executors relevantes) corre off-thread.
  - No aparece error de Unity tipo "can only be called from the main thread".
  - `ExternalBarrierGate` no introduce crashes por continuations off-thread.
- **Logs esperados:**
  - `[DEBUG-SS001]` (o equivalente) reporta main thread.
  - Si hay violación: debe verse error + channel/context.
- **Commits/PR:** `828ef8b` (working tree con cambios sin commit)
- **Repro harness:**
  - Repro Case 1 (ver Chunk 4) con spam de input durante 30–50 iteraciones.

### Chunk 2 — ResourceKey / base de conflictos por recurso (back-compat)
- **Estado:** 🚧
- **Feature flags:** `AR` (si aplica), `EG`
- **Acceptance gates (EditMode):**
  - `ResourceKey` serializa/comparea estable (Equals/GetHashCode) y no colisiona trivialmente.
  - `ExternalBarrierGate` tests pasan (bloqueo hasta completar tasks).
- **Logs esperados:**
  - `[DEBUG-EG001]` register count incrementa
  - `[DEBUG-EG011]` scope complete despues de completar tasks
- **Commits/PR:** `828ef8b` (working tree con cambios sin commit)
- **Repro harness:**
  - Ejecutar tests EditMode: `ExternalBarrierGateTests`, `StepSchedulerBarrierGateTests`.

### Chunk 3 — MotionService (Route A backend)
- **Estado:** 🚧
- **Feature flags:** `MS`, `EG`
- **Acceptance gates (Unity):**
  - Por actor/binding: nunca hay >1 locomotion "dueño" activo (single-owner).
  - Cancel/restart no deja el actor "in transit" colgado.
  - Commit points: al terminar/cancelar, el estado se consolida (no teleports al iniciar el siguiente move).
- **Logs esperados:**
  - `[DEBUG-MS001]` start intent/key
  - `[DEBUG-MS002]` cancel/replaced (si ocurre)
  - `[DEBUG-MS003]` commit
  - `[DEBUG-EG010]` awaiting N y `[DEBUG-EG011]` complete
- **Commits/PR:** `828ef8b` (working tree con cambios sin commit)
- **Repro harness:**
  - Turn start (run_up) y luego cancelar/relanzar rapido (spam) y observar commits.

### Chunk 4 — Gate + RecipeTweenObserver (sin escenas)
- **Estado:** 🚧
- **Feature flags:** `EG`, `MS`, `RTO`
- **Acceptance gates (Unity):**
  - Si un movimiento se dispara, se registra barrera (no "gate bonito pero inutil").
  - `StepScheduler` no avanza al siguiente group hasta que termine la barrera (o timeout DEV logueado).
  - No hay deadlocks: si algo queda vivo, aparece dump de pendientes.
  - `Animator.applyRootMotion` set/restore no crashea por off-thread (hardening).
- **Logs esperados (orden típico):**
  - `[DEBUG-EG001]` register barrier
  - `[DEBUG-EG010]` awaiting ...
  - `[DEBUG-MS003]` commit ...
  - `[DEBUG-EG011]` scope complete ...
- **Commits/PR:** `828ef8b` (working tree con cambios sin commit)
- **Repro harness (Repro Case 1):**
  1. Turn → `run_up` (spotlight)
  2. Confirm action (`basic_attack`)
  3. Verificar que no hay overlaps/teleports evidentes

### Chunk 4.5 — Multi-recipes por acción (RecipeChain / "move_to_target" separado)
- **Estado:** 🚧
- **Feature flags:** `APF`, `EG`, `MS`, `RTO` (`AP` opcional para logs)
- **Acceptance gates (Unity):**
  - Con `AP` ON y `basic_attack`: se ejecuta `move_to_target` antes del payload.
  - `move_to_target` registra barrera y el scheduler espera (no arranca payload en tránsito).
  - Con `AP` OFF: no cambia el flujo actual.
- **Logs esperados (orden mínimo):**
  - `[DEBUG-AP###]` (si existe) plan/node start/end (opcional)
  - `[DEBUG-EG001]` register reason=`move_to_target`
  - `[DEBUG-EG010]` awaiting ...
  - `[DEBUG-MS003]` commit ...
  - `[DEBUG-EG011]` complete ...
- **Commits/PR:** `828ef8b` (working tree con cambios sin commit)
- **Repro harness:**
  1. `BattleDebug.SetEnabled("APF", true)`
  2. Ejecutar `basic_attack` contra target
  3. Confirmar logs `move_to_target` + ausencia de soup entre approach/payload

### Chunk 5 — Authoring real (assets) (pendiente)
- **Estado:** ⛔
- **Feature flags:** n/a
- **Acceptance gates (Unity):**
  - `move_to_target` existe como recipe asset registrado (sin inline).
  - `attack_payload` no toca locomotion (MotionRoot).
  - `return_home` garantizado por envelope (sin depender del action).
- **Logs esperados:** mismos que 4.5 pero sin "inline".
- **Commits/PR:** pendiente
- **Repro harness:** Repro Case 1 usando assets reales.

---

## Checklist (Quick Close)

Marca un chunk como **cerrado** solo cuando:

- [ ] Pasan los **Acceptance gates** del chunk (Unity/EditMode).
- [ ] Se ven los **logs esperados** (tags correctos y orden si aplica).
- [ ] No hay errores Unity off-main-thread.
- [ ] Queda linkeado a **commit(s)/PR**.

### Estado por chunk

- [x] **Chunk 0 - Debug logs/toggles** (flags: `EG/MS/RTO/SS/AP`)
- [x] **Chunk 1 - Main-thread determinism** (flags: `SS/EG`)
- [ ] **Chunk 2 - ResourceKey / base back-compat** (flags: `EG` + tests EditMode)
- [ ] **Chunk 3 - MotionService (Route A backend)** (flags: `MS/EG`)
- [ ] **Chunk 4 - Gate + RecipeTweenObserver (sin escenas)** (flags: `EG/MS/RTO`)
- [ ] **Chunk 4.5 - Multi-recipes por accion (move_to_target separado)** (flags: `APF/EG/MS/RTO`)
- [ ] **Chunk 5 - Authoring real (assets)** (sin inline)
