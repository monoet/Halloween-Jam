# StepScheduler MVP Hand-off

## 2025-11-05 - StepScheduler direct playback
- NewAnimOrchestratorAdapter now prefers ActionRecipes; when a recipe exists for the actionId it runs a RecipePlaybackSession (no timeline needed).
- Legacy timelines remain as fallback when no recipe is registered or the wrapper lacks IAnimationBindingResolver.
- Session management handles both paths via a shared dictionary keyed by actor; cancel/dispose semantics remain unchanged.
- Development logs show ""[AnimAdapter] Executing recipe ..."" whenever the scheduler path is active for quick verification.
- Router registration happens around scheduler execution so VFX/SFX bindings continue to resolve.
## Context
- Goal: deliver a lean runtime that can execute ActionRecipes for the pilot "BasicAttack_KS_Light" while we stand up the authoring surface.
- Legacy AnimationSequenceSession now routes payloads that include recipes/steps through the StepScheduler, keeping classic clip playback untouched.
- Event bus + timed-hit services are already injected so window/gate flow can raise gameplay events.

## Current Runtime State
- Sequential execution: no change from the baseline; steps run in order with conflict policies.
- Parallel execution (join = Any / All): join=Any short-circuits on the first Branch/Abort/Failure and cancels siblings; join=All waits for every child, honours timeouts, and reports the aggregate (branch/fail/abort). Timeout always cancels children before returning `Abort("ParallelTimeout")`.
- System steps supported: `window.open`, `window.close`, `gate.on`, `damage.apply`, `fallback` (emite `AnimationFallbackRequestedEvent`), `phase.lock`, `phase.unlock`.
- System step handlers viven ahora en `SystemStepRunner`, fuera de `StepScheduler`.
- `ActionRecipeCatalog` mantiene las recetas piloto (`BasicAttack_KS_*`, `UseItem`) generadas por `ActionRecipeBuilder`/`PilotActionRecipes` y las registra en el `StepScheduler` al iniciar.
- Conflict defaults: el scheduler aplica `WaitForCompletion` por defecto a `animatorClip`/`tween`/`flipbook` y `SkipIfRunning` para `sfx`/`vfx` cuando la receta no especifica política.
- Observabilidad: `IStepSchedulerObserver` now sees `OnStepStarted`, outcome `Branch`, y recibe callbacks `OnBranchTaken(sourceId, targetLabel)`; el metrics observer cuenta branched/cancelled/skipped y los logs siguen mostrando target y razon de abort.
- Cleanup: `ExecutionState.ImmediateCleanup()` cierra ventanas, libera locks (`AnimationLockEvent` false) y resetea `TimedHitService` tan pronto como un grupo aborta (además del dispose final).
- Core split (en progreso): `ExecutionState`, `StepResult`, `StepGroupResult` and `ListPool` viven ahora en `Execution/Runtime/Core/StepSchedulerCoreTypes.cs` para reducir el tama�o del scheduler y facilitar pruebas.

## Builder & Cat�logo
- `ActionRecipeBuilder` ofrece un blueprint DTO (`ActionRecipeDefinition`, `GroupDefinition`, `StepDefinition`) y un adaptador simple `BuildFromTimeline(ActionTimeline)` para traducir eventos con formato inline a recetas.
- `ActionRecipeCatalog` act�a como registro in-memory; expone `Register/RegisterRange/TryGet` y permite poblarse desde c�digo o futuros assets serializados.
- `PilotActionRecipes` encapsula las recetas base (ventana + gate, paths success/mediocre, uso de item). Cada rama cierra la ventana y dispara `damage.apply`/`fallback` seg�n corresponda.
- `ActionRecipeCatalogDiagnostics.ValidatePilotRecipes` corre en editor/desarrollo para verificar que BasicAttack_Light contenga los grupos esperados y que las recetas piloto incluyan los ejecutores clave.

## Gaps / Next Steps
1. **Telemetry polish**
   - Extend observers to surface branch targets and abort reasons explicitly (currently only logged).
   - Capture parallel group timeout/cancellation counts.
2. **Validation tooling**
   - Recipe validator should flag unmatched window open/close, unsupported join policies, and required conflict defaults.
3. **Authoring / Builder**
   - Integrar el builder con assets `ActionTimeline` (importer/editor) en lugar de blueprints hardcodeados.
   - Serializar cat�logo en assets/scriptable para authoring (el runtime ya soporta `RegisterRange`).
4. **Gameplay integration**
   - Subscribe battle systems to `AnimationDamageRequestEvent` and `AnimationFallbackRequestedEvent`.
   - Define fallback policy (timeline vs recipe) in the orchestrator when the event fires.
5. **Refactor / cleanup**
   - Split StepScheduler into fa�ade + helpers (e.g., `ParallelGroupRunner`, `SystemStepRunner`, `ExecutionState`).
   - Move event definitions to a shared location (e.g., `AnimationEventBus`) once fallback handling is wired.

## Definition of Done for the Pilot
- BasicAttack_KS_Light plays clip + tween in parallel; window opens/closes correctly.
- `gate.on` branches to success/fail recipes, with Abort causing immediate cleanup and fallback dispatch.
- Metrics observer reports executed/skipped/branched/cancelled counts; logs show branch targets and abort reasons.
- No dangling windows or timed-hit locks after abort or cancellation.

## Suggested Workflow for the Next Iteration
1. Finish telemetry observer updates (branch target, abort reason) and wire a lightweight validator pass.
2. Extract builder layer for ActionTimeline assets and connect pilot recipes.
3. Revisit StepScheduler structure to reduce file size once tests pass for the MVP path.
- Conflict handling moved to ActiveExecutionRegistry helper (Core/Conflict).
- Group execution split: SequentialGroupRunner y ParallelGroupRunner manejan la l�gica de slots, reduciendo StepScheduler.
