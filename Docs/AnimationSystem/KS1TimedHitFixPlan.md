# KS1 Timed Hit – Single-Emitter Plan (No Scene Changes)

## Meta
- **Owner of windows:** `Ks1TimedHitRunner` (profile-driven, uses `Ks1TimedHitProfile` tier timeline duration + normalized windows).
- **What stops emitting:** `ActionSequencerEventDispatcher` / `SystemStepRunner` must NOT publish `AnimationWindowEvent` for KS1 timed hits.
- **Scope guard:** Only KS1; Basic/None paths stay untouched.
- **Scene changes:** None expected. If a scene tweak becomes mandatory, log it explicitly in code with a high-signal message.

## Steps to Implement
1) **Detect KS1 explicitly** — ✅ Done in code: runner selection uses profile/kind.
   - Use the action’s timed-hit profile (`Ks1TimedHitProfile`) or `RunnerKind.Ks1` to branch. No tag heuristics.
   - ⚠️ Asegurar señal disponible en dispatcher/step runner: si ahí no llega el profile, pasar `RunnerKind`/profile por el contexto del step (no inferir por tags).
   - Sugerido en dispatcher/runner: `isKs1 = (request.RunnerKind == TimedHitRunnerKind.Default) || (request.Profile is Ks1TimedHitProfile);`

2) **Gate dispatcher emission (KS1 only)** — ✅ Implemented
   - In `ActionSequencerEventDispatcher` + `SystemStepRunner`, when runner kind is KS1, skip publishing `AnimationWindowEvent` open/close. Basic keeps current behavior.
   - ⚠️ Gate con condición explícita KS1; no afectar Basic.
   - Orden recomendado: implementar este gate primero, con log `[KS1] Dispatcher windows suppressed action={id}`.

3) **KS1 emits real windows** — ✅ Implemented
   - In `Ks1TimedHitRunner`, emit open/close using `tier.TimelineDuration` and tier start/end normalized → seconds.
   - Use `WaitForSeconds` (or the combat clock equivalent) to hold the window; rename the old “simulate” flag to reflect it is the *authoritative* KS1 timeline.
   - ⚠️ Si existe reloj/pacing de combate (pausas/timeScale), usarlo en vez de `WaitForSeconds` para no des-sincronizar.
   - Añadir logs de ventana: `[KS1] Runner window open/close dur={sec} tag={tag} idx={i}/{count}`.
   - Ventana forzada a 0-1 (start=0, end=1); el center del perfil se usa sólo para payload.

4) **Kill duplicates** — ☐ Pending (verify single-emitter in logs)
   - After KS1 runner emits valid windows, disable/remove any dispatcher-based or legacy simulate paths that would double-emit for KS1.
   - Sanity check: solo un emisor para KS1; validar en consola que no haya doble open/close.

5) **Dependency check** — ☐ Pending
   - Confirm listeners (`TimedHitOverlay`, audio, routers) are fine consuming the runner’s windows/results. No other step should depend on dispatcher close events; timed_hit completion task remains the gating point for StepScheduler.
   - Buscar referencias a `AnimationWindowEvent`; si alguna lógica avanza por el close del dispatcher, migrarla a esperar el Task de timed_hit o `TimedHitResultEvent`.

6) **Temporary verification logs** — ✅ Added (PhasEvInput/KS1 logs en servicio/UI; KS1 runner logs en ventana)
   - `[KS1] Dispatcher windows suppressed action={id}`
   - `[KS1] Runner window open/close dur={sec} tag={tag} idx={i}/{count}`
   - `[KS1] TimedHit result action={id} judgment={judgment} offsetMs={x}`
   - Remove/disable after validation.

7) **Input guards** — ✅ Implemented
   - ExecutionState y TimedHitInputRelay sólo registran input si `HasActiveWindow(actor)` retorna true, para evitar pulsos fantasma antes de abrir ventana.

## Acceptance
- KS1 actions: exactly one open+close per window, `Open < Close`, finite offset, judgments match input; no “Infinity” unless truly no input.
- Basic actions: unchanged.
- Cancel mid-KS1: timed_hit task returns and StepScheduler advances; no deadlocks.

## Notes
- Duration source is the profile (tier timeline), not the animation clip/recipe.
- If a scene change becomes necessary, add an explicit high-signal log entry (e.g., `CRITICAL SceneConfig: Needed X in scene`) before requiring it.
