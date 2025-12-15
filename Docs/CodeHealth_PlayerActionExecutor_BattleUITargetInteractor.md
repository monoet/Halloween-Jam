# Refactor + Bugfix Plan — PlayerActionExecutor + BattleUITargetInteractor

> Regla: cada step compila, un cambio de comportamiento por commit cuando aplique, y revisión en escena cuando se indique.

---

## Orden recomendado (por riesgo / visibilidad)
1) **BattleUITargetInteractor** (idempotencia + subs por modo) — bug visible/timeline  
2) **PlayerActionExecutor** (refund + snapshots + helper) — bug silencioso/deuda

---

# Track B — BattleUITargetInteractor (UI target confirm)

## B0 — Baseline (no code)
- [x] Capturar baseline con logs (antes de tocar nada)
  - [x] Caso Panel: seleccionar target y confirmar normalmente (1 ejecución)
  - [x] Caso Panel: spamear Enter/Submit (no duplica timeline; documentar si duplica)
  - [x] Cancel/back: regresar sin quedar bloqueado
- [x] Guardar evidencia: 1 screenshot de consola + nota de pasos exactos

Baseline verificada:
- Confirm normal: 1 commit, 1 pipeline → OK
- Spam Enter/Submit en selector: NO duplica confirm ni timeline → OK
- Cancel/Back libera estado y no bloquea navegación → OK

Conclusión: flujo UI y ejecución estables previos al refactor; no hay reentradas activas en BUITI.

---

## B1 — Introducir `ResolveOnce(TargetSet)` y usarlo en handlers (bugfix central)
**Objetivo:** idempotencia real (resolver 1 vez por sesión).

- [x] Implementar `ResolveOnce(TargetSet result)`
  - [x] `if (pendingTcs == null) return;`
  - [x] `if (confirmInFlight) return;`
  - [x] `confirmInFlight = true;`
  - [x] `ResolveAndClear(result);`
- [x] Cambiar handlers para que TODOS pasen por `ResolveOnce`
  - [x] `HandleSelected(int id)` -> `ResolveOnce(TargetSet.Single(id))`
  - [x] `HandleCancel()` -> `ResolveOnce(TargetSet.Back)`
  - [x] `HandleConfirm()` (virtual mode) -> `ResolveOnce(TargetSet.Single(currentId))`
- [x] Asegurar que `confirmInFlight` solo se setea dentro de `ResolveOnce`
- [x] Commit: `UI Targeting: ResolveOnce idempotent confirm/cancel`

### Revisión en escena (obligatoria)
- [ ] Panel: click/confirm normal -> 1 resolución
- [ ] Panel: spam Enter/Submit -> sigue siendo 1 resolución (no doble timeline)
- [ ] Cancel: vuelve atrás sin lock
- [ ] Repetir 5 veces: confirm/cancel alternados (no debe aumentar spam de logs)

> Nota (antes de B2): meter hotfix de hilo/doble cobro (sacar SpendCP/SP de LunarChain y quitar hops off-main en pipeline). Se aplica como caso especial entre B1 y B2 para evitar crash `EnsureRunningOnMainThread`.

---

## B2 — Subs por modo (Panel no escucha Confirm; Virtual sí)
**Objetivo:** evitar doble fuente de confirm en panel.

- [x] En `SelectAsync`:
  - [x] Virtual mode (`targetPanel == null`): suscribirse a `uiRoot.OnTargetConfirmed`
  - [x] Panel mode (`targetPanel != null`): NO suscribirse a `uiRoot.OnTargetConfirmed`
- [x] Mantener `uiRoot.OnTargetCancel` si aplica para ambos (si no duplica)
- [x] Commit: `UI Targeting: mode-based subscriptions (panel ignores OnTargetConfirmed)`

### Revisión en escena (obligatoria)
- [ ] Panel: confirmar con UI (OnTargetSelected) funciona
- [ ] Panel: Enter/Submit no dispara ruta alterna (no doble confirm)
- [ ] Virtual (si puedes correrlo): Enter confirma target actual

---

## B3 — Cleanup simétrico (helpers de subscribe/unsubscribe) (refactor sin cambiar comportamiento)
**Objetivo:** evitar leaks/callback duplicado y bajar complejidad.

- [x] Crear helpers:
  - [x] `SubscribePanel()` / `UnsubscribePanel()`
  - [x] `SubscribeUiRoot()` / `UnsubscribeUiRoot()`
  - [x] `SubscribeVirtual()` / `UnsubscribeVirtual()`
- [x] `ResolveAndClear` llama siempre a los Unsubscribe correspondientes (sin ramas raras)
- [x] Dejar `confirmInFlight` reset solo en:
  - [x] `SelectAsync` (inicio sesión)
  - [x] `ResolveAndClear` (fin sesión)
- [x] Commit: `UI Targeting: symmetric cleanup + subscription helpers`

### Revisión en escena (obligatoria)
- [ ] 10 selecciones seguidas (confirm/cancel alternado)
- [ ] Verificar: logs no se duplican por callback acumulado
- [ ] Highlights se limpian siempre al salir

---

# Track A — PlayerActionExecutor (CP charge/refund + snapshots + judgment)

## A0 — Baseline (no code)
- [ ] Capturar baseline de 3 casos (logs + CP antes/después):
  - [ ] CpCharge=0 (CP igual)
  - [ ] CpCharge>0 + pipeline success (CP baja 1 vez)
  - [ ] CpCharge>0 + pipeline fail/abort (CP neto 0 por refund)
- [ ] Guardar evidencia: consola + pasos exactos

---

## A1 — Snapshots con nombres explícitos (refactor sin cambiar lógica)
**Objetivo:** claridad sin mover comportamiento.

- [x] Introducir variables:
  - [x] `preCost`
  - [x] `postCost` (aunque sea igual por ahora)
  - [x] `afterAction` (aunque sea igual por ahora)
- [x] Ajustar nombres/lectura sin cambiar orden real
- [x] Commit: `Executor: clarify snapshot naming (preCost/postCost/afterAction)`

### Revisión en escena (obligatoria)
- [ ] Repetir los 3 casos baseline (idéntico)

---

## A2 — Refund robusto (cpSpent afuera del try; NO usar selection.CpCharge para refund)
**Objetivo:** evitar CP gratis / refund incorrecto.

- [x] Declarar `int cpSpent = 0;` antes del `try`
- [x] Centralizar cobro/refund en helper `ChargeSelectionCosts` (usa `cpSpent`/`spSpent`)
- [x] En fail y exception: refund solo `cpSpent`/`spSpent` (no selection-based)
- [x] Commit: `Executor: refund uses cpSpent only (no selection-based refund)`

### Revisión en escena (obligatoria)
- [ ] Success: CP baja 1 vez
- [ ] Fail: CP neto 0
- [ ] (Si puedes) Forzar exception antes de Spend: CP no cambia

---

## A3 — Orden correcto: charge -> postCost -> judgment; WithPostCost usa postCost (bugfix de snapshots)
**Objetivo:** separar post-cost vs post-action.

- [x] Cobrar CP antes de construir `judgment` (cuando no venga pre-inyectado)
- [x] `postCost = ResourceSnapshot.FromCombatant(actor)` inmediatamente tras Spend
- [x] `afterAction = ResourceSnapshot.FromCombatant(actor)` tras pipeline (solo logs)
- [x] `finalJudgment = judgment.WithPostCost(postCost)` (no afterAction)
- [x] Commit: `Executor: correct snapshot semantics (postCost vs afterAction)`

### Revisión en escena (obligatoria)
- [ ] Repetir 3 casos baseline
- [ ] Verificar que ya no aparecen warnings falsos de “Expected CP charge but none occurred”
- [ ] Verificar marks/gating no cambian inesperadamente (si hay mark que depende de CP)

---

## A4 — Extraer helper `ChargeSelectionCosts` (refactor estructural sin cambiar comportamiento)
**Objetivo:** bajar tamaño del método y reducir drift.

- [x] Crear `ChargeResult` en el mismo file:
  - [x] `bool Success`
  - [x] `int CpSpent`
  - [x] `ResourceSnapshot Pre`
  - [x] `ResourceSnapshot PostCost`
- [x] Implementar `ChargeSelectionCosts(ctx)` (solo cobra + snapshots + logs)
- [x] `ExecuteAsync` consume `ChargeResult`
- [x] Commit: `Executor: extract ChargeSelectionCosts helper`

### Revisión en escena (obligatoria)
- [ ] Repetir 3 casos baseline (idéntico)

---

## A5 — LunarChain: clamp del cap incluso si SpendCP(overflow) falla (cambio acotado)
**Objetivo:** cap real siempre.

- [x] Si `totalComboPointsAwarded > refundCap`:
  - [x] intentar `SpendCP(overflow)`
  - [x] si falla: log + clamp `totalComboPointsAwarded = refundCap`
- [x] Commit: `Executor: enforce LunarChain refund cap even on overflow spend failure`

### Revisión en escena (si tienes caso LunarChain)
- [ ] Ejecutar acción LunarChain que exceda cap y confirmar que nunca sobrepasa `refundCap`

---

# Post-check (anti-regresión)
- [ ] 20 turnos seguidos (mezcla de acciones con/ sin CP; confirm/cancel targets)
- [ ] No hay:
  - [ ] doble timeline por confirm
  - [ ] CP “gratis” por excepción
  - [ ] logs duplicados por callbacks acumulados
  - [ ] warnings inconsistentes de charge
