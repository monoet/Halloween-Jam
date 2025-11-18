# TimedHit v2 Progress Log

## Phase 1 - Harden result API (TimedHitResult v1.5)
- [x] Step 1: Extend TimedHitResult struct with new fields + constructor
- [x] Step 2: Update TimedHitService / HUD logging (optional) [commit ba4ea80]
- [ ] Step 3: Validation notes (todo: capture after gameplay test)

## Phase 2 - Parallel KS1 multi-window runner (v2, behind flag)
- [x] Step 1: Introduce Ks1PhaseOutcome type [commit ba4ea80]
- [x] Step 2: Create Ks1MultiWindowTimedHitRunner (experimental)
- [x] Step 3: Wire dual mode (legacy vs v2) with configuration flag (enableExperimentalRunner bool)

## Phase 3 - Integrate PhaseOutcome with HUD/animation (test env)
- [x] Step 1: Ks1AnimationBridge listening to PhaseResolved (Ks1PhaseAnimationBridge.cs)
- [x] Step 2: Optional per-phase HUD feedback (TimedHitHudBridge.cs update)
- [x] Step 3: Audio untouched (confirm)
- Notes:
  - Added runner auto-discovery + terminal recipe guard to Ks1PhaseAnimationBridge.
  - TimedHitHudBridge now clears phase/final labels via sequence events and short terminal hold.

## Phase 4 - BasicTimedHitRunner (single window opt-in)
- [x] Step 1: Introduce BasicTimedHitRunner + profile
- [x] Step 2: Hook basic_attack via opt-in flag
- [x] Step 3: Ensure HUD compatibility
- Notes:
  - Added TimedHitPhaseEvent + TimedHitHooksBridge so HUD/anim/audio can subscribe to Basic runner without special cases.
- Notes:
  - Added BasicTimedHitProfile + runner kind plumbing through selections/requests.
  - SimpleAttackAction now opts into Basic runner and scales damage via timed results.
  - basic_attack selections set `RunnerKind=Basic`, so BasicTimedHitRunner is invoked only when actions opt in.
  - TimedHitHudBridge now listens to TimedHitPhaseEvent/ResultEvent for both KS1 and Basic; no direct runner bindings remain.
  - Normalized KS1/Basic phase indices + HUD to consume TimedHitPhaseEvent/TimedHitResultEvent exclusively.

## Phase 5 - Cleanup and convergence
- [ ] Step 1: Consolidate on v2 runners (KS1)
- [ ] Step 2: Remove/Archive legacy code
- [ ] Step 3: Document TimedHit v2 (CombatEventsImpPlan.md)
