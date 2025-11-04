
# Animation System Architecture Guidelines

## Principles
- **Data-first orchestration**: timelines describe intent; runtime resolves bindings via resolvers and recipes.
- **Scheduler decoupling**: `StepScheduler` runs atomic steps; executors stay stateless and reusable.
- **Explicit bindings**: all clips, flipbooks, and tweens live in `CharacterAnimationSet` with hashed lookups.
- **Fail fast, log smart**: guard rails warn through `BattleLogger` and validation tools; never silently swallow missing IDs.

## Flow Overview
1. `NewAnimOrchestratorAdapter` resolves `AnimationRequest` ? `ActionTimeline`.
2. `AnimationSequenceSession` bridges sequencer events with wrapper/scheduler.
3. Timeline payload decides: simple clip (`clip=`) or recipe (`steps=` / `recipeId=`).
4. `StepScheduler` executes recipes, delegating to registered executors.
5. Executors talk to presentation surfaces: wrapper (Animator/Flipbook/Tween) and router bundle (SFX/VFX/UI/Camera).

```
Timeline Event ? AnimationSequenceSession ? [Clip playback] or [StepScheduler]
                                               |                         |
                                               |                         +- Executors ? Wrapper / Routers
                                               +- AnimatorWrapper ? Animator Graph
```

## Anti-Bloat Rules
- Keep executors laser-focused on one medium (Animator, Flipbook, Tween, SFX, VFX, Wait).
- Never stuff payload parsing into executors; only `AnimationSequenceSession` builds recipes.
- No implicit globals: register services and recipes explicitly in `AnimationSystemInstaller`.
- Prefer dictionaries over linear scans for bindings or recipe lookup.

## Extending the System
1. **New Executor**
   - Implement `IActionStepExecutor` in `Execution/Runtime/Executors`.
   - Register in `AnimationSystemInstaller.BuildStepScheduler()`.
   - Document required payload keys.
2. **New Recipe**
   - Define via code (e.g., `SampleActionRecipes`) or inline payload `steps=`.
   - Register with `StepScheduler.RegisterRecipe` for reuse (`recipeId=`).
3. **New Binding Type**
   - Extend `CharacterAnimationSet` with caches and resolver methods.
   - Provide companion executor if playback semantics change.
4. **Telemetry**
   - Implement `IStepSchedulerObserver`; register to capture metrics or emit analytics.

## Validation & Tooling
- Use `Battle/Animation/Validate Recipes and Bindings` to scan for missing assets and recipe registration issues.
- `StepSchedulerMetricsObserver` aggregates runtime stats (`AnimationSystemInstaller.SchedulerMetrics`).

## Timeline Payload Cheatsheet
- Simple clip: `clip=Attack_Slash;loop=false;speed=1.2`
- Inline recipe: `recipe=BasicAttack;steps=animatorClip:Attack_Windup(loop=false)|wait(duration=0.1)|animatorClip:Attack_Slash`
- Registered recipe: `recipeId=UseItem`

## Testing Hooks
- Executors are pure async operations; unit test by providing fake wrappers/router services.
- Recipes are simple data objects; snapshot them in tests to prevent regressions.
- Validation menu command runs outside play mode; integrate into CI via `-executeMethod`.
