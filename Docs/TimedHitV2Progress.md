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
- [ ] Step 1: Ks1AnimationBridge listening to PhaseResolved
- [ ] Step 2: Optional per-phase HUD feedback
- [ ] Step 3: Audio untouched (confirm)

## Phase 4 - BasicTimedHitRunner (single window opt-in)
- [ ] Step 1: Introduce BasicTimedHitRunner + profile
- [ ] Step 2: Hook basic_attack via opt-in flag
- [ ] Step 3: Ensure HUD compatibility

## Phase 5 - Cleanup and convergence
- [ ] Step 1: Consolidate on v2 runners (KS1)
- [ ] Step 2: Remove/Archive legacy code
- [ ] Step 3: Document TimedHit v2 (CombatEventsImpPlan.md)
