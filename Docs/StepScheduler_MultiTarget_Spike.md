# Spike: StepScheduler Multi-Target / Multi-Party Support (Dev Flagged)

Estado: Diseño listo para implementar bajo define `STEP_SCHEDULER_MULTI` (o bool equivalente). No debe afectar el MVP actual (Single/All + Marks) mientras el flag esté apagado.

## Objetivo
El scheduler debe coreografiar acciones con listas de targets (Single/All) y múltiples actores en party, sin recalcular listas vivas ni depender de `context.Enemy`. Spotlight/cámaras/anim deben usar el target del *step*.

## Definiciones / Flag
- Define de compilación sugerido: `STEP_SCHEDULER_MULTI`.
- Cuando el flag está *apagado*: comportamiento actual.
- Cuando el flag está *encendido*: se usan steps por target con transform específico.

## Cambios por área

### 1) Payload del step/evento
- `StepSchedulerStep` (o tipo equivalente) añade:
  - `CombatantState Target`
  - `Transform TargetTransform`
- `ActionStartedEvent/CompletedEvent` ya llevan lista de targets; se mantiene igual.

### 2) Generación de steps (StepSchedulerRecipeExecutor / CombatEventDispatcher)
- Recibe `IReadOnlyList<CombatantState> targets` (snapshot resuelto).
- Itera en orden estable de la lista.
- Crea un step por target:
  - `step.Target = target`
  - `step.TargetTransform = target != null ? target.transform : null`
- Stagger:
  - Usa `BattleActionData.combatEventStaggerStep` (ya existe).
  - Si `stagger > 0`, inserta delay entre steps; si `0`, back-to-back.
- Envuelve la ruta nueva en `#if STEP_SCHEDULER_MULTI` y deja el camino viejo como fallback.

### 3) Providers / Spotlight / Transforms
- `RunnerUpToSpotlightProvider`, `TransformTweenProvider`, y cualquier provider que lea `context.Enemy`:
  - Bajo flag, usar `step.Target` / `step.TargetTransform`.
  - Si el target es null/muerto, hacer skip seguro; no recalcular listas vivas.
- Spotlight/cámaras deben apuntar al `TargetTransform` del step actual, no a un “target global”.

### 4) Triggered / Follow-ups
- Asegurar que follow-ups usen la misma `TargetSet` snapshot del action original (no recalcular vivos).
- Si ya se pasa `resolvedTargets` a triggered, documentar; si no, inyectar bajo flag.
- Orden estable: iterar la misma lista, sin reordenar.

### 5) Telemetría / Debug (dev-only)
- Log opcional bajo flag:
  - `[Scheduler] actor=X steps=N stagger=Y targets=[id1,id2,...]`
  - Por step: `[Scheduler] step target=id transform=(name)`
- No contaminar builds sin flag.

### 6) Validación manual (flag encendido)
- Single: un target → spotlight correcto, sin cambios.
- All (3 enemigos): spotlight/anim apuntan a cada target en orden; stagger respeta `combatEventStaggerStep`.
- Target muerto a mitad: step lo salta sin crashear ni recalcular.
- Follow-up/marks: no se duplican impactos; orden por snapshot.

## Orden sugerido de implementación
1) Añadir campos Target/TargetTransform al step (sin usarlos).
2) Generador de steps por target + stagger (bajo flag).
3) Providers leen `step.Target`/`TargetTransform` (bajo flag).
4) Validar triggered/follow-up usan snapshot (bajo flag).
5) Logs dev y pruebas manuales con flag activado; flag off para builds normales.

## Notas
- No recalcular TargetSet en ningún punto; usar el snapshot capturado en commit/resolve.
- Este spike no aborda concurrencia de múltiples actores simultáneos; ejecución sigue secuencial por actor. Solapado visual se puede evaluar luego.

