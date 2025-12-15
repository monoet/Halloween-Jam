# Multi-party SP/CP Charging & Actor Identity — Technical Assessment

## Scope
- Player multi-party (2+ members), Single/All actions.
- Visual ambiguity due to StepScheduler spotlight assumptions.
- Focus: who is the acting combatant, who pays SP/CP, and what ActionJudgment records.

## Current Signals (added for this investigation)
- `ActionStartedEvent` logs actor id/name, side, actionId, scope, target count.  
  Source: `Assets/Scripts/BattleV2/Orchestration/Services/BattleEventBus.cs`.
- `ResourceCharge` logs CP/SP spend with before/after + side.  
  Source: `Assets/Scripts/02_Systems/03_Combat/Combat/CombatantState.cs` (`SpendCP`, `SpendSP`).
- `ActionCharge` logs commit snapshot (actor id/name, actionId, cp/sp pre/post, cpCharge).  
  Source: `Assets/Scripts/BattleV2/Orchestration/Services/PlayerActionExecutor.cs`.

> These logs are temporary dev-only instrumentation to trace actor identity and resource drains independent of StepScheduler visuals.

## Hypotheses to Validate
1) Selection builder or input provider pins to `party[0]` instead of the UI-selected actor, so SP/CP charge hits the wrong combatant.  
2) Resource charging path uses a global “player” reference rather than `draft.Actor` / `currentPlayer`.  
3) Visual ambiguity hides correct logic (false alarm), but SP is still correct.  
4) ActionJudgment.SourceActorId may be wrong if built from the wrong actor, affecting marks/timed-hit gating.

## Verification Steps (manual)
1. Create a scene with 2 party members, distinct SP (e.g., A=100, B=7).  
2. Cast a SP-cost action **as B** (Single and All).  
   - Check Unity console:  
     - `ActionStartedEvent … actor=B…`  
     - `ActionCharge … cpPre/SpPre/SpPost`  
     - `ResourceCharge … type=SP delta=-X` should mention B.  
3. Repeat casting **as A**.  
4. Confirm `ActionStartedEvent` actor matches the UI-selected actor and `ResourceCharge` hits the same actor.  
5. Compare `ActionJudgment` (via `ActionCharge` log) to see if `SourceActorId` (implied by actor id/name) aligns with the selected actor.

## Risk Assessment
- **Severity:** High (progression/balance regression, player trust).  
- **Impact:** Wrong actor pays SP/CP; marks/timed-hit gates evaluate on wrong resources; actions may use wrong stats/affinities.  
- **Likelihood:** Medium–High given reports and single-actor charging symptom.
- **Confidence:** Medium pending log capture.

## What to Capture in Logs
- Selection commit: actor id/name, actionId, scope, cpCharge.  
- Resource charge: actor id/name, type (SP/CP), before/after.  
- Action start: actor id/name, targets count, side.

## Expected Outcome
- Each action charges exactly once, on the acting actor.  
- `ActionStartedEvent`, `ActionCharge`, and `ResourceCharge` all reference the same actor per execution.  
- If visuals disagree, logs are the source of truth until StepScheduler gains multi-party support.

## Pass / Fail Criteria
- **Pass (minimum):**
  - `ActionStartedEvent.actor == ActionCharge.actor == ResourceCharge.actor`
  - Exactly **one** `ResourceCharge` per action (Single and All).
- **Fail (minimum):**
  - Any of the three actors differ, **or**
  - `ResourceCharge` fires more than once, **or**
  - `ActionCharge` shows correct `spPostCost` but `ResourceCharge` hits a different actor (desfase).

## Control Test (UI vs Execution vs Catalog)
- **Scripted actor (no UI):** trigger a debug/scripted action for actor B directly using the same executor path (e.g., `ScriptedBattleInputProvider` or a debug button that builds a `BattleSelection` with `actor=B` and sends it to the PlayerActionExecutor). Bypass menu/UI and inspect logs.
  - If scripted charges B correctly → bug lives in UI/selection/active-actor handoff.
  - If scripted also charges wrong → bug lives in execution/actor context/catalog.

## Single Source of Truth (trace map)
- Source of truth: the committed actor (`SelectionDraft.Actor` / `BattleSelection` actor).
- Everything must derive from it:
  - `ActionJudgment.SourceActorId`
  - `SpendSP/SpendCP` receiver
  - `ActionStartedEvent.actor`
- Red flags: any use of `party[0]`, `CurrentPlayer`, `BattleManager.Player`, or other globals instead of the committed actor.

## Checklist of Logs to Capture per Action
- `ActionStartedEvent`: actor id/name/side, targets, **and selection actor if available**.
- `ActionCharge`: actor id/name, `cpPre/SpPre/SpPost`, `cpCharge`, `selection.actorId`, `executor.actorId` (should match).
- `ResourceCharge`: actor id/name, type (SP/CP), before/after.

## Next Steps After Logs
1) If actor mismatch is observed: trace back to selection assembly (`BattleManagerV2.HandleTurnReady` → `RequestPlayerAction` → provider) and ensure `draft.Actor` is used through commit/execution.  
2) If only SP/CP charges wrong: inspect action implementations (`SpendSP/SpendCP` calls) and the actor passed into `ActionRequest`/`ActionContext`.  
3) If ActionJudgment actor is wrong but charges are correct: patch judgment creation to use the committed actor.
