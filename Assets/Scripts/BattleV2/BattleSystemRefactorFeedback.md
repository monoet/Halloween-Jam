# Battle System Refactor – Review Notes (needs Gemini double-check)

Context: Refactor claims (fail-fast, config integrity, centralized UI state, flight recorder) were applied. Below are areas that need verification/alignment with actual code.

## High-priority concerns
- TimedHitService no longer performs retention/profile validation or diagnostics. Only input-provider methods were added; no warning if window > buffer, no BattleDiagnostics logging. Actions depending on that warning will still fail silently.
- Input provider registration is required: SetInputProvider is exposed but not auto-wired anywhere. If nobody calls it, TryConsumeInput/consumption paths always return false.
- SceneFixer re-enables simulateTimelineEvents on Ks1TimedHitRunner. This can reintroduce duplicate window events alongside timeline windows.

## UI State machine gaps
- MenuState does not show UI; ExecutionState hides all; TargetSelectionState shows target UI but does not return to Menu on cancel/resolve. Legacy callers of SetMode may not see expected UI toggles.
- States are instantiated on each transition; if future states subscribe to events, there is no unsubscribe/reuse strategy documented.
- Awake in BattleUIInputDriver sets MenuState before Initialize sets TimedHitService/ActiveActor; any state needing those will see nulls.

## Diagnostics (Flight Recorder)
- BattleDiagnostics exists but components do not log to it (TimedHitService, UI states, TargetInteractor, BattleManagerV2). The advertised “full timeline” isn’t implemented.

## Configuration integrity
- AnimationSystemInstaller exposes inputBufferRetention, but there is no runtime/OnValidate check to compare retention vs. KS1 timelines or tolerance profiles.

## Targeting flow
- BattleUITargetInteractor switches to TargetSelectionState but leaves state reset commented out; relies on orchestrator to exit the state. Verify cancellation path doesn’t leave UI in TargetSelection.

## Suggested next steps
1) Reintroduce buffer vs. profile validation in TimedHitService (or installer) and log/auto-extend in dev builds.
2) Wire SetInputProvider in bootstrap (e.g., BattleManagerV2) so TryConsumeInput works.
3) Decide if simulateTimelineEvents should stay disabled by default; adjust SceneFixer accordingly.
4) Align UI states with legacy expectations: MenuState should show menu/root; TargetSelectionState should return to Menu on cancel/resolve; ensure state initialization happens after Initialize.
5) Actually instrument BattleDiagnostics in the components mentioned if the “flight recorder” is required.
