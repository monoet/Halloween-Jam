# AnimatorWrapper / Variant Resolver Audit — 2025-12-09

## Context
- `AnimatorWrapper` now consumes commands → resolves variants → looks up `CharacterAnimationSet` → plays via PlayableGraph.
- Recent fixes: main-thread guard, serialized `ConsumeCommand`, reentrancy-safe `CancelPlayback`, no CTS revival, optional global resolver registration.

## Findings
- **Thread safety / ordering**: `ConsumeCommand` switches to main thread and serializes with `SemaphoreSlim` (good). `PlayAsync` also switches, so ordering is deterministic per actor.
- **Cancellation hygiene**: `CancelPlayback` snapshots CTS and nulls the field before cancel; `GetDestroyCancellationToken` no longer revives after `OnDestroy`. `destroyCts` now nulled in `OnDestroy`.
- **Variant resolution**: Per-actor configs (`commandVariants`) with strategies (Cycle/SequenceOnce/etc.), same-frame guard, fallback-to-base logging. Variant advancement happens before clip lookup; a missing variant still advances index (acceptable for now).
- **Clip resolution scope**: Default `registerToGlobalResolver=false` prevents cross-character ID bleed; local `CharacterAnimationSet` lookup first. Global registration is opt-in.
- **Logging**: Debug logs gated by `debugAw01Enabled` and `logPlayback`; missing variant fallback and missing clip are reported.

## Residual Risks / TODO
- **ExecKey stability**: Uses `owner.GetHashCode()`; consider using `CombatantId`/stable ID if de-spam ever depends on persistence across runs.
- **Variant advance on missing clips**: If a variant is missing and fallback disabled, index still advances; only revisit if authoring expects “consume only on success.”
- **Global resolver contamination**: Keep `registerToGlobalResolver` off unless needed; overlapping IDs across sets will collide if enabled.

## Severity by Component (1–5)
- `AnimatorWrapper`: 2/5 — lean after safeguards; main thread + gate + CTS hygiene in place.
- `RecipeTweenObserver`: 4/5 — varias ramas (spotlight/anchors/windup); riesgo de seguir creciendo y ser frágil.
- `AnimationSystemInstaller` + `AnimatorRegistry`: 4/5 — registro global opcional puede colisionar IDs si se habilita; auto-scan puede inflar resolver.
- `CombatEventDispatcher/Router`: 3/5 — puede spamear missing presets y crecer en triggers.
- `QA/Diagnostics Harnesses`: 2/5 — bajo riesgo, salvo registros duplicados si conviven con installer.
## Severity (1–5)
- Current wrapper bloat/risk: **2/5** (lean enough after recent safeguards).
