```md
# Refactor + Bugfix Plan (KISS / YAGNI) — PlayerActionExecutor + BattleUITargetInteractor

> Regla: **cada step compila**, un cambio de comportamiento por commit cuando aplique, y **revisión en escena** cuando se indique.

---

## Orden recomendado (por riesgo / visibilidad)
1) **BattleUITargetInteractor** (idempotencia + subs por modo) — bug visible/timeline
2) **PlayerActionExecutor** (refund + snapshots + helper) — bug silencioso/deuda

---

# Track B — BattleUITargetInteractor (UI target confirm)

## B0 — Baseline (no code)
- [x ] Capturar baseline con logs (antes de tocar nada)
  - [ x] Caso Panel: seleccionar target y confirmar normalmente (1 ejecución)
  - [x ] Caso Panel: spamear Enter/Submit (no debería duplicar timeline, documentar si duplica)
  - [ x] Cancel/back: regresar sin quedar bloqueado
- [x ] Guardar evidencia: 1 screenshot de consola + nota de pasos exactos

B0 Baseline verificada:
- Confirm normal: 1 commit, 1 pipeline → OK
- Spam Enter/Submit en selector: NO duplicó confirm ni timeline → OK
- Cancel/Back libera estado y no bloquea navegación → OK

Conclusión: flujo UI y ejecución estable previos al refactor; no hay reentradas activas en BUITI.

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
- [x] Asegurar que `confirmInFlight` **solo se setea** dentro de `ResolveOnce`
- [ ] Commit: `UI Targeting: ResolveOnce idempotent confirm/cancel`

### Revisión en escena (obligatoria)
- [ ] Panel: click/confirm normal -> **1** resolución
- [ ] Panel: spam Enter/Submit -> **sigue siendo 1** resolución (no doble timeline)
- [ ] Cancel: vuelve atrás sin lock
- [ ] Repetir 5 veces: confirm/cancel alternados (no debe aumentar spam de logs)

> Nota (antes de B2): vamos a meter un hotfix de hilo/doble cobro (sacar SpendCP/SP de LunarChain y quitar los hops off-main en el pipeline). Se aplica como caso especial entre B1 y B2 para evitar el crash `EnsureRunningOnMainThread`.

---

## B2 — Subs por modo (Panel NO escucha Confirm; Virtual SÍ)
**Objetivo:** evitar doble fuente de confirm en panel.

- [ ] En `SelectAsync`:
  - [ ] **Virtual mode** (`targetPanel == null`): subscribirse a `uiRoot.OnTargetConfirmed`
  - [ ] **Panel mode** (`targetPanel != null`): **NO** subscribirse a `uiRoot.OnTargetConfirmed`
- [ ] Mantener `uiRoot.OnTargetCancel` si aplica para ambos (si no duplica)
- [ ] Commit: `UI Targeting: mode-based subscriptions (panel ignores OnTargetConfirmed)`

### Revisión en escena (obligatoria)
- [ ] Panel: confirmar con UI (OnTargetSelected) funciona
- [ ] Panel: Enter/Submit NO dispara ruta alterna (no doble confirm)
- [ ] Virtual (si puedes correrlo): Enter confirma target actual

---

## B3 — Cleanup simétrico (helpers de subscribe/unsubscribe) (refactor sin cambiar comportamiento)
**Objetivo:** evitar leaks/callback duplicado y bajar complejidad.

- [ ] Crear helpers:
  - [ ] `SubscribePanel()` / `UnsubscribePanel()`
  - [ ] `SubscribeUiRoot()` / `UnsubscribeUiRoot()`
  - [ ] `SubscribeVirtual()` / `UnsubscribeVirtual()`
- [ ] `ResolveAndClear` llama *siempre* a los Unsubscribe correspondientes (sin ramas raras)
- [ ] Dejar `confirmInFlight` reset **solo** en:
  - [ ] `SelectAsync` (inicio sesión)
  - [ ] `ResolveAndClear` (fin sesión)
- [ ] Commit: `UI Targeting: symmetric cleanup + subscription helpers`

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

- [ ] Introducir variables:
  - [ ] `preCost`
  - [ ] `postCost` (aunque sea igual por ahora)
  - [ ] `afterAction` (aunque sea igual por ahora)
- [ ] Ajustar nombres/lectura sin cambiar orden real
- [ ] Commit: `Executor: clarify snapshot naming (preCost/postCost/afterAction)`

### Revisión en escena (obligatoria)
- [ ] Repetir los 3 casos baseline (idéntico)

---

## A2 — Refund robusto (cpSpent afuera del try; NO usar selection.CpCharge para refund)
**Objetivo:** evitar CP gratis / refund incorrecto.

- [ ] Declarar `int cpSpent = 0;` **antes** del `try`
- [ ] Centralizar refund en función local/hhelper:
  - [ ] `RefundCp(CombatantState actor, int amount)` (best-effort, amount>0)
- [ ] En fail y exception:
  - [ ] refund **solo** `cpSpent`
  - [ ] prohibido refund con `selection.CpCharge`
- [ ] Commit: `Executor: refund uses cpSpent only (no selection-based refund)`

### Revisión en escena (obligatoria)
- [ ] Success: CP baja 1 vez
- [ ] Fail: CP neto 0
- [ ] (Si puedes) Forzar exception antes de Spend: CP no cambia

---

## A3 — Orden correcto: charge -> postCost -> judgment; WithPostCost usa postCost (bugfix de snapshots)
**Objetivo:** separar “post-cost” vs “post-action”.

- [ ] Cobrar CP **antes** de construir `judgment` (cuando no venga pre-inyectado)
- [ ] `postCost = ResourceSnapshot.FromCombatant(actor)` inmediatamente tras Spend
- [ ] `afterAction = ResourceSnapshot.FromCombatant(actor)` tras pipeline (solo logs)
- [ ] `finalJudgment = judgment.WithPostCost(postCost)` (no afterAction)
- [ ] Commit: `Executor: correct snapshot semantics (postCost vs afterAction)`

### Revisión en escena (obligatoria)
- [ ] Repetir 3 casos baseline
- [ ] Verificar que ya no aparecen warnings falsos de “Expected CP charge but none occurred”
- [ ] Verificar marks/gating no cambian inesperadamente (si hay mark que depende de CP)

---

## A4 — Extraer helper `ChargeSelectionCosts` (refactor estructural sin cambiar comportamiento)
**Objetivo:** bajar tamaño del método y reducir drift.

- [ ] Crear `ChargeResult` en el mismo file:
  - [ ] `bool Success`
  - [ ] `int CpSpent`
  - [ ] `ResourceSnapshot Pre`
  - [ ] `ResourceSnapshot PostCost`
- [ ] Implementar `ChargeSelectionCosts(ctx)` (solo cobra + snapshots + logs)
- [ ] `ExecuteAsync` consume `ChargeResult`
- [ ] Commit: `Executor: extract ChargeSelectionCosts helper`

### Revisión en escena (obligatoria)
- [ ] Repetir 3 casos baseline (idéntico)

---

## A5 — LunarChain: clamp del cap incluso si SpendCP(overflow) falla (cambio acotado)
**Objetivo:** cap real siempre.

- [ ] Si `totalComboPointsAwarded > refundCap`:
  - [ ] intentar `SpendCP(overflow)`
  - [ ] si falla: log + **clamp** `totalComboPointsAwarded = refundCap`
- [ ] Commit: `Executor: enforce LunarChain refund cap even on overflow spend failure`

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
```



























# Code Health — PlayerActionExecutor & BattleUITargetInteractor

Focus: robustness, clarity, and multi-party safety for CP spend and target confirmation.

## PlayerActionExecutor (Assets/Scripts/BattleV2/Orchestration/Services/PlayerActionExecutor.cs)
- **Single-responsibility drift:** Method mixes logging, charge, refunds, pipeline exec, marks, triggered effects, defeat checks. Hard to read and reason.
- **Charge/refund coupling:** CP charge is inline; refund lives in multiple branches (pipeline failure and catch) with duplicated selection-based refund logic.
- **Snapshot semantics blurred:** `resourcesPost` first represents post-charge, then is overwritten after pipeline. Judgment creation uses pre/post that may not match “post-cost pre-effects”.
- **Error handling asymmetry:** Refund uses `selection.CpCharge` even if cost computation changes; SP cost not handled (future risk).
- **Logging noise vs signal:** `AddCp.Debugging01` used for multiple phases; lacks structured tags for actor/selection vs charge vs refund.
- **Extension risk:** LunarChain refunds mutate CP without guarding against concurrent charge logic; SpendCP overflow path still modifies totals if SpendCP fails (logged, but state remains inconsistent).

### Recommendations
1) **Extract helpers**: `ChargeResources(context)` → returns (success, pre, post, chargedCp), encapsulates SpendCP/SP + logging; `HandlePipelineFailure` for refund.
2) **Define snapshots explicitly**: `preCost`, `postCostPreEffects`, `postAction`. Use them consistently in ActionJudgment and logs.
3) **Single refund path**: Centralize refund on failure/exception with the actual charged amount, not selection defaults.
4) **Structured logging**: Separate tags: `CP.Charge`, `CP.Refund`, `Pipeline.Result`, `Refund.LunarChain` with consistent payload (actorId, actionId, cpCost, cpAwarded).
5) **Guard refunds**: In LunarChain overflow, only mutate totals if SpendCP succeeds; otherwise keep awarded as-is.
6) **Future-proof costs**: Add SP/base CP costs into the charge helper; ensure duplicate-charge detection stays as a warning/error hook.
7) **Naming**: Consider renaming `Player` to `Actor` in context to reduce confusion in multi-party flows.

## BattleUITargetInteractor (Assets/Scripts/BattleV2/UI/BattleUITargetInteractor.cs)
- **Mode branching noise:** Panel vs virtual paths diverge across handlers; `confirmInFlight` is touched in multiple places with comments describing uncertainty.
- **State lifecycle unclear:** `confirmInFlight` reset only in `SelectAsync`; no explicit reset on new selection state from the driver; risk of stale lock if ResolveAndClear is bypassed.
- **Dependency lookups at runtime:** Uses `FindFirstObjectByType` in Awake/Start; obscures required dependencies and complicates testing.
- **Event subscription scatter:** Subscribes/unsubscribes in several branches; easy to miss unsubscribe on new paths.
- **Highlighting flow:** ClearAllHighlights called in several spots; potential redundant calls and missing calls if early returns occur.
- **Logging verbosity:** Mixed Debug.Log and BattleDiagnostics; inconsistent tagging for targeting lifecycle.

### Recommendations
1) **Explicit state struct**: Track `SelectionSession` (pendingTcs, confirmInFlight, candidates, mode) to centralize resets and prevent stale flags.
2) **Unified confirm path**: Route both panel and virtual confirmation through a single `ConfirmSelection(TargetSet)` that sets `confirmInFlight` and resolves once.
3) **Dependency injection**: Require serialized references (or constructor for tests); avoid `FindFirstObjectByType` except as editor-time validation.
4) **Subscription helpers**: `SubscribePanel()` / `UnsubscribePanel()` and `SubscribeUiRoot()` / `UnsubscribeUiRoot()` to ensure symmetry.
5) **Input gating**: Provide clear hooks to disable input driver during confirm and re-enable when selection ends; keep the lock localized.
6) **Logging cleanup**: Standardize tags (e.g., `Targeting.Start`, `Targeting.Confirm`, `Targeting.Cancel`) with actor/target info; remove stray Debug.Log unless guarded by dev flags.
7) **Highlight lifecycle**: Only clear once on resolve/cancel; avoid redundant calls on early returns.

## Quick Wins (low-risk refactors)
- PlayerActionExecutor: extract `ChargeCpOnce` helper and replace inline blocks; keep current behavior but improve readability.
- TargetInteractor: move `confirmInFlight` handling into `ConfirmSelection` and reset it explicitly in `ResolveAndClear`.
- Add TODOs for SP charge support and ActionJudgment snapshot semantics to prevent regressions in marks/gating features.

## Suggested Refactor Plan (concrete, small steps)
### PlayerActionExecutor
1) Introduce a tiny helper in the same file:
   ```csharp
   private readonly struct ChargeResult { public bool Success; public int CpSpent; public ResourceSnapshot Pre; public ResourceSnapshot PostCost; }
   private static ChargeResult ChargeSelectionCosts(PlayerActionExecutionContext ctx) { /* SpendCP/SP + logs, no judgment */ }
   ```
2) Flow in `ExecuteAsync`:
   - `var charge = ChargeSelectionCosts(ctx); if (!charge.Success) return fallback;`
   - Build `judgment` after charge using `charge.Pre` / `charge.PostCost`.
   - Run pipeline; on failure/exception refund `charge.CpSpent` only.
   - Take `afterAction = ResourceSnapshot.FromCombatant(player)` only for logs, not for `WithPostCost`.
   - Clamp LunarChain overflow even if `SpendCP(overflow)` fails.

### BattleUITargetInteractor
1) Single exit:
   ```csharp
   private void ResolveOnce(TargetSet set) { if (pendingTcs == null || confirmInFlight) return; confirmInFlight = true; ResolveAndClear(set); }
   ```
2) Mode rule:
   - Virtual: subscribe to `uiRoot.OnTargetConfirmed` and call `ResolveOnce(TargetSet.Single(currentId))`.
   - Panel: do **not** subscribe to `OnTargetConfirmed`; rely on `OnTargetSelected` → `ResolveOnce(...)`.
3) Helpers for symmetry: `SubscribePanel/UnsubscribePanel`, `SubscribeUiRoot/UnsubscribeUiRoot`, `SubscribeVirtual/UnsubscribeVirtual`.
4) Reset `confirmInFlight` only in `SelectAsync` and `ResolveAndClear`; nowhere else.

Applying these keeps the bugfix intact, removes refund risk, and makes confirm idempotent without a large refactor.
