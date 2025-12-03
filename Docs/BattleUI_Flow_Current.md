# Battle UI / Targeting / Commit – Flujo Actual (BattleV2)

> Propósito: documentar cómo fluye hoy la selección de acción/target/commit (incluyendo ESC/Back) para preparar un rediseño donde “Attack/Magic/Item” no sea irreversible hasta confirmar target.

## Resumen ejecutivo
- El pipeline actual trata “cancel” desde Target Selection como **cancel duro**: se resuelve la selección con `proposed/none`, el provider ve cancel, `BattleManagerV2` ejecuta fallback/cancel, se cierra turno y puede disparar timelines “fantasma”.
- No hay distinción formal entre “Back a menú” (soft) y “Cancel acción/turno” (hard).
- `CP Intent` inicia en `RequestPlayerAction` (antes de abrir menú) y se cancela/consume al entrar a `ProcessPlayerSelection`, aunque ESC en target selection sólo debería volver a menú.

## Estados UI actuales (BattleUIInputDriver + BattleUIStates)
- **MenuState**
  - Input: navegación (WASD/Arrows), Confirm (Enter/Space/Z), Cancel (Esc/Backspace/X).
  - Confirm: ejecuta submit sobre el botón seleccionado (abre Attack/Magic/Item, etc.).
  - Cancel: `UiRoot.GoBack()`, que burbujea al provider como cancel duro (termina turno).
- **TargetSelectionState**
  - Input: navegación (WASD/Arrows) sobre los candidatos; Cancel (Esc/Backspace/X).
  - Cancel: `UiRoot.GoBack()` → BattleUITargetInteractor.HandleCancel() → resuelve TCS como cancel/empty → provider onCancel → BattleManagerV2 EndTurn/fallback.
  - Confirm: submit en el target (o tecla confirm) → BattleUITargetInteractor.HandleSelected() → resuelve TCS con TargetSet.
- **ExecutionState**
  - Input: sólo Space para timed hit; resto se ignora.
- **LockedState**
  - Sin input.

## Secuencia actual (happy path Attack)
1) BattleManagerV2.RequestPlayerAction():
   - `cpIntent.BeginTurn(player.CurrentCP)` (fase de selección).
   - Llama `inputProvider.RequestAction(context, onSelected, onCancel)`.
2) Provider abre menú (Attack/Magic/Item). Driver en **MenuState**.
3) Jugador elige Attack → provider arma un `BattleSelection` con `Action` (sin targets) y manda a TargetingCoordinator/BattleUITargetInteractor.
4) BattleUITargetInteractor.SelectAsync():
   - Driver `SetState(new TargetSelectionState())` (TargetSelection UI / virtual cycle).
   - Suscribe `OnCancel` y `OnTargetSelected` (TCS pendiente).
5) Confirm target → TCS resuelta con TargetSet → BattleManagerV2.ProcessPlayerSelection(selection):
   - Heurística consume CP intent si costCP>0 o charge/timed profile.
   - `cpIntent.EndTurn("CommittedOutcome")`.
   - Valida selección, resuelve targets, ejecuta acción (StepScheduler/Anim orchestrator), avanza turno.

## Secuencia actual (ESC en Target Selection)
1) En TargetSelectionState: Esc → `UiRoot.GoBack()`.
2) BattleUITargetInteractor.HandleCancel():
   - Limpia highlights, **resuelve TCS** con `proposed` (o `TargetSet.None`).
   - Driver `SetState(new MenuState())`.
3) Provider ve “cancel/none” → llama `onCancel` → BattleManagerV2:
   - `cpIntent.Cancel("SelectionCanceled"); cpIntent.EndTurn("SelectionCanceled");`
   - Fallback/“timeline fantasma” si no hay selección válida.
   - Turno termina.

## Choke points actuales
- **Commit real**: `BattleManagerV2.ProcessPlayerSelection(selection)` (único punto que ejecuta acción y avanza turno).
- **Creación de BattleSelection**: en el provider (ManualBattleInputProviderUI/V2) al elegir Attack/Magic/Item (antes de target).
- **Cancel duro**:
  - Provider onCancel (p.ej. ESC en MenuState o TargetSelectionState).
  - Validation/targets vacíos → fallback.
- **CP Intent**:
  - BeginTurn: justo antes de `DispatchToInputProvider` (RequestPlayerAction).
  - ConsumeOnce/Cancel: en `ProcessPlayerSelection` (heurística costCP/profile).
  - EndTurn: en commit o cancel provider.

## Comportamiento ESC/Back (actual)
- **MenuState**: Esc → UiRoot.GoBack → provider onCancel → BattleManager EndTurn + fallback.
- **TargetSelectionState**: Esc → UiRoot.GoBack → BattleUITargetInteractor.HandleCancel → provider onCancel → BattleManager EndTurn + fallback (no regresa al menú de Attack, pierde intención).
- **ExecutionState**: Esc ignorado (no definido).

## Dónde nace la “timeline fantasma”
- Cuando `onCancel` o selección vacía llega a `BattleManagerV2.ProcessPlayerSelection`/fallback:
  - Sin targets/acción válida → `ExecuteFallback()` o equivalente.
  - Se cierra el turno (EndTurn ya llamado en onCancel).
  - Se percibe como timeline sin input / “fantasma”.

## Tabla de estados (simplificada)
| Estado UI | Input | Efecto | Cambio de estado | Side-effects |
|-----------|-------|--------|------------------|--------------|
| MenuState | Confirm (Enter/Space/Z) | Submit botón (Attack/Magic/Item) | TargetSelectionState (si requiere targets) o sigue en menú | None |
| MenuState | Cancel (Esc/Backspace/X) | UiRoot.GoBack → provider onCancel | — | cpIntent.Cancel/EndTurn, fallback |
| TargetSelectionState | Confirm | BattleUITargetInteractor resolves TCS with target | ExecutionState (sched) | cpIntent ConsumeOnce/EndTurn en commit |
| TargetSelectionState | Cancel | UiRoot.GoBack → HandleCancel resolves TCS with proposed/none | MenuState (driver) pero provider onCancel termina turno | cpIntent.Cancel/EndTurn, fallback |
| ExecutionState | Space | TimedHitService.RegisterInput | — | — |

## Recomendaciones para rediseño (fuera de alcance actual)
- Separar “Back a menú” (soft) de “Cancel turno” (hard).
- No resolver TCS ni llamar onCancel en ESC de TargetSelection; sólo volver a MenuState y mantener intent/turno abierto.
- EndTurn sólo en commit o cancel explícito de acción (no por back en targeting).
- Considerar flag/enum en provider para distinguir “back” vs “cancel”.

---

## Anexos
- Diagrama PlantUML del flujo actual: ver `Docs/BattleUI_Flow_Current.puml`.
