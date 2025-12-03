```json
{
  "title": "CP Intent (Turn-Scoped) – Implementation Plan (no code) [Adjusted to your turn model + KISS guards]",
  "goal": "Reintroduce HarnessV2-style CP allocation during the PLAYER-CHOOSING-ACTION window via hotkeys (L/R), with a single source of truth for intent, simple HUD feedback, and deterministic consume/cancel at ACTION COMMIT. No menu dependency; intent is purely turn-scoped UI state.",
  "core_decisions": {
    "turn_definition": "A 'turn' here means ONLY the time the player is choosing/confirming an outcome (selection phase). Once the outcome is committed, the CP intent phase ENDS immediately. The following animations/timed-hit execution are considered 'in-between turns' for CP intent purposes.",
    "single_source_of_truth": "RuntimeCPIntent owns Current/Max/IsActiveTurn and raises events. No other system infers intent from UI, logs, or runner state.",
    "consumption_authority": "Only the action-commit site decides whether CP intent is consumed or canceled, based on ActionData metadata (not a hardcoded list).",
    "no_runtime_cp_changes_assumed": "For your game: CP available does not change during the selection phase, so Max can be fixed at BeginTurn. (Still safe to clamp again at commit.)"
  },
  "architecture": {
    "runtime_state": {
      "name": "RuntimeCPIntent",
      "scope": "Selection-phase scoped (player choosing action).",
      "fields": [
        "int Current",
        "int Max",
        "bool IsActiveTurn"
      ],
      "events": [
        "TurnStarted(max)",
        "TurnEnded(reason)",
        "Changed(current,max,reason)",
        "Consumed(amount,reason,selectionId)",
        "Canceled(reason)"
      ],
      "notes": "No timing, no Update loops, no combat logic. Just state + events."
    },
    "interfaces": {
      "ICpIntentSource": "Read-only access to Current/Max/IsActiveTurn + events.",
      "ICpIntentSink": "Write access: BeginTurn(max), EndTurn(reason), Set/Add(reason), ConsumeOnce(selectionId, reason), Cancel(reason)."
    },
    "drivers": {
      "hotkeys_driver": {
        "name": "CpIntentHotkeysDriver",
        "behavior": "Listens to L/R (configurable). Only modifies intent when cpIntent.IsActiveTurn==true.",
        "input_gating": "Optional: if you have a signal like 'isCommittingOutcome' then disable hotkeys from the moment commit begins."
      },
      "hud_text": {
        "name": "CpIntentHudText",
        "behavior": "Shows 'CP: {Current}/{Max}' only while IsActiveTurn. Subscribes to events (no polling)."
      },
      "vfx_driver_mvp": {
        "name": "CpIntentVfxDriver",
        "behavior": "Optional stub: on Changed/Consumed/Canceled, trigger subtle aura/intensity or just log for now."
      }
    }
  },
  "action_metadata_requirement": {
    "problem": "Do NOT use a hardcoded list of non-consuming actions (items/defend/flee/etc). It will rot.",
    "solution": {
      "preferred": "Add an explicit field to ActionData/ActionDefinition: CpUsageMode { None, BaseOnly, BasePlusIntent, IntentOnly }",
      "minimal_kiss_alt": "If you want ultra-minimal: bool ConsumesCpIntent (and assume BasePlusIntent for most actions)."
    },
    "notes": "This keeps rules data-driven and avoids future edge-case hacks (marks, reactions, special skills)."
  },
  "integration_points": {
    "begin_turn_selection_phase": {
      "where": "Wherever your player selection phase begins (the moment you previously had pendingContext != null in HarnessV2 terms). Typically BattleManagerV2 / TurnManager state enter.",
      "calls": [
        "cpIntent.BeginTurn(maxCp = Player.CurrentCP)",
        "cpIntent.Set(0, reason='TurnStartReset')"
      ]
    },
    "action_commit_point": {
      "where": "Single place where a BattleSelection/request becomes final (e.g., PlayerActionExecutor, SelectionCommitHandler, or equivalent). MUST be a single choke point.",
      "steps": [
        "selectionId = stable id for this commit (can be incrementing int, GUID, or hash).",
        "Determine cpUsage from ActionData.",
        "If cpUsage consumes intent: extra = cpIntent.ConsumeOnce(selectionId, 'ActionCommit'); else: cpIntent.Cancel('NonConsumingOutcome').",
        "Compute final CP cost using baseCost + extra only if cpUsage says so.",
        "Immediately end the intent phase: cpIntent.EndTurn('CommittedOutcome') (so hotkeys stop right away)."
      ],
      "double_commit_guard": "ConsumeOnce(selectionId) prevents double-consumption if commit fires twice (UI spam, retries)."
    },
    "turn_end_fallback": {
      "where": "If the player leaves selection without committing (cancel/back out/forced end).",
      "calls": [
        "cpIntent.Cancel('SelectionAborted')",
        "cpIntent.EndTurn('SelectionAborted')"
      ]
    }
  },
  "behavior_rules_runtime_cp_intent": {
    "BeginTurn(max)": "Sets Max=max; Current=0; IsActiveTurn=true; raises TurnStarted + Changed(reason='BeginTurn').",
    "Add(delta)": "If !IsActiveTurn => ignore (optional log once). Else clamp Current to 0..Max; raise Changed(reason).",
    "Set(value)": "Same clamp rules; raise Changed.",
    "ConsumeOnce(selectionId, reason)": "If !IsActiveTurn => return 0. If already consumed for same selectionId => return 0. Else capture Current, set Current=0, raise Consumed + Changed, return captured.",
    "Cancel(reason)": "Sets Current=0 (if not already), raises Canceled + Changed.",
    "EndTurn(reason)": "Sets IsActiveTurn=false; raises TurnEnded. (Do NOT automatically Cancel here; commit site decides Cancel vs Consume.)"
  },
  "files_to_add": [
    "Assets/Scripts/BattleV2/Charge/RuntimeCPIntent.cs",
    "Assets/Scripts/BattleV2/Charge/ICpIntentSource.cs",
    "Assets/Scripts/BattleV2/Charge/ICpIntentSink.cs",
    "Assets/Scripts/BattleV2/Charge/CpIntentEvents.cs",
    "Assets/Scripts/BattleV2/Input/CpIntentHotkeysDriver.cs",
    "Assets/Scripts/BattleV2/UI/CpIntentHudText.cs",
    "Assets/Scripts/BattleV2/VFX/CpIntentVfxDriver.cs"
  ],
  "acceptance_manual_QA": [
    "During selection phase: L/R adjusts intent; HUD updates instantly; value is clamped 0..Player.CurrentCP (Max fixed at BeginTurn).",
    "On committing a consuming action: intent is consumed exactly once (even if commit spam happens); HUD stops (IsActiveTurn=false) immediately after commit; cost includes extra as per CpUsageMode.",
    "On committing a non-consuming action: intent is canceled (returns to 0); HUD stops immediately after commit; no CP is spent.",
    "On cancel/back-out: intent resets to 0 and HUD hides.",
    "No StepScheduler changes required for correctness; scheduler just receives already-committed request with computed CP usage."
  ],
  "known_shortcomings_and_ok_for_MVP": [
    "No handling for CP changing mid-selection (you stated it won't happen). If that assumption changes later, add UpdateMax(newMax, clamp=true) or re-check available CP at commit.",
    "VFX is optional and can be a stub; keep it purely event-driven so it never affects battle logic."
  ],
  "message_to_codex": {
    "instruction": "Implement CP Intent as a selection-phase scoped state (not animation/timed-hit scoped). Create RuntimeCPIntent + interfaces + event-driven HUD + hotkeys driver. Integrate at player selection start (BeginTurn with max=Player.CurrentCP) and at the single action commit choke point: read ActionData CpUsageMode (or ConsumesCpIntent), then either ConsumeOnce(selectionId) or Cancel(), compute final CP cost, and immediately EndTurn('CommittedOutcome') so hotkeys stop right away. Do NOT use hardcoded lists of action types; add CpUsageMode/ConsumesCpIntent to ActionData. Add a ConsumeOnce guard to prevent double-consumption on commit spam. Keep StepScheduler untouched."
  }
}
```
Open questions remain: exact turn start/end signals, single commit choke point, ActionData flag for CP usage, how to expose RuntimeCPIntent to UI/VFX.
Expose RuntimeCPIntent to UI/VFX

Instanciarlo en BattleInstaller

Exponerlo como singleton de combate vía interfaz ICpIntentSource

HUD y VFX lo obtienen por inyección, no por Find()

esto te responde ?Expose RuntimeCPIntent to UI/VFX

Instanciarlo en BattleInstaller

Exponerlo como singleton de combate vía interfaz ICpIntentSource

HUD y VFX lo obtienen por inyección, no por Find()