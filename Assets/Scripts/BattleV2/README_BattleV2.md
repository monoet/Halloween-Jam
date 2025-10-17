# BattleV2 Architecture Snapshot

This folder contains the new modular battle core. Components:

- `Core/`
  - `BattleLogger` – centralised logging via `[Battle:<tag>]` (subscribable via `OnLogged`).
  - `BattleStateController` – explicit state machine (`Idle`, `AwaitingAction`, `Resolving`, `Victory`, `Defeat`).
  - `CombatContext` – shared references (player, enemy, services, catalog).
  - `BattleServices` – access to RNG + helpers (`GetAnimatorFor`).
  - `BattleConfig` – ScriptableObject that bundles catalog, input provider and shared services.
- `Actions/`
  - `IAction` / `IActionProvider` – strategy contract for actions.
  - `ActionData` – serialisable data used by catalogs/providers.
  - `ActionCatalog` – builds available lists, resolves `IAction`, provides fallback.
  - `SimpleAttackAction` – example implementation (CP/SP aware).
- `Providers/`
  - `IBattleInputProvider` – contract for player/AI/auto input sources.
  - `AutoBattleInputProvider` – picks first action available (useful for tests).
  - `ManualBattleInputProvider` – placeholder that currently degrades to auto until UI V2 is connected.
- `Orchestration/`
  - `BattleManagerV2` – orchestrates the loop, uses providers + catalog, handles fallback and enemy turn resolution.
  - `BattleBootstrapper` – helper to auto-start battles in test scenes.
- `UI/`
  - `BattleDebugPanel` – optional overlay showing state, last action, CP/SP and recent logs (`BattleLogger.OnLogged`).

## How to wire a test scene

1. Create a GameObject with `BattleStateController` and `BattleManagerV2`.
2. Create/assign a `BattleConfig` asset (catalog + provider + services) and drag it onto the manager.
3. Assign `CombatantState` references for player/enemy.
4. Optionally add `BattleBootstrapper` and enable `autoStart` to trigger the loop on play.
5. (Manual provider) – once the new UI is implemented, replace the placeholder `ManualBattleInputProvider` with a proper implementation that bridges to panels.

## Next steps

- Replace `ManualBattleInputProvider` with real UI logic (connect `BattleActionMenu` + `ActionSelectionUI` via the new API).
- Implement more actions (`Magic`, `Items`, etc.) as individual `IAction` strategies.
- Replace the enemy fallback logic with a dedicated AI provider.

This document should help anyone jumping into V2 understand where code belongs and how to hook it up.
