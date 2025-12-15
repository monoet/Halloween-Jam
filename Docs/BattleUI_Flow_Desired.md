# BattleV2 – Plan de Implementación (Back ≠ Cancel en Target Selection)

## Objetivo
- ESC/Back durante Target Selection vuelve al submenú previo (Attack/Magic/Item) sin terminar el turno ni disparar fallback/timeline.
- Commit sólo ocurre al confirmar target (Outcome Confirmed). Cancel duro sólo desde menú raíz.
- CP intent permanece activo durante Back; sólo se consume en commit o se cancela en cancel duro.

## Alcance
- Aplica a Attack, Spells y Items (cualquier acción que abra Target Selection).
- No toca StepScheduler ni installer; sólo UI/provider/manager.

## Pasos de implementación
### 1) Nuevos contratos de Targeting
- Crear `TargetSelectionOutcome { Confirmed, Back, CancelTurn }`.
- Crear `TargetSelectionResult { Outcome, TargetSet }`.
- Mantener overload legacy si es necesario (TargetSet) pero migrar provider al nuevo contrato.

### 2) TargetSelectionState (UI state)
- En `TargetSelectionState.HandleInput`, ESC/Back debe invocar un método “Back” (no `UiRoot.GoBack` que llama cancel duro).
- Cambiar el estado del driver a `MenuState` (o submenú correspondiente) y no resolver el provider como cancel.

### 3) BattleUITargetInteractor
- `SelectAsync` debe devolver `TargetSelectionResult` (o wrapper).
- Handlers:
  - `HandleSelected` → Outcome.Confirmed + TargetSet.
  - `HandleBack` (ESC) → Outcome.Back. Limpia highlights, cambia driver a MenuState, NO llama onCancel, NO EndTurn.
  - `HandleCancelTurn` (opcional cancel duro) → Outcome.CancelTurn (para menú raíz si se desea).
- No resolver TCS como cancel/empty en Back.

### 4) Provider (ManualBattleInputProviderUI/V2)
- Consumir `TargetSelectionResult`:
  - Confirmed → armar `BattleSelection` final (action + targets + cpCharge) y llamar `onSelected`.
  - Back → reabrir submenú (Attack/Magic/Item) y continuar loop; no llamar `onCancel`, mantener cpIntent vivo.
  - CancelTurn → `onCancel` y salir (EndTurn).
- Mantener acción en “draft” hasta Confirmed; no enviar selección irreversible antes de target.

### 5) BattleManagerV2
- Sin cambios lógicos si el provider deja de llamar onCancel en Back.
- Asegurar que EndTurn sólo se dispara en commit o cancel duro real; eliminar rutas donde Back genere selección vacía/fallback.

### 6) CP Intent
- BeginTurn: igual (al abrir fase de selección).
- Back: no cancela ni consume; estado permanece.
- Commit: ConsumeOnce y EndTurn.
- Cancel duro: Cancel + EndTurn (menú raíz).

## Diagramar el flujo deseado
- Actualizar/añadir PlantUML (nuevo archivo o sección) mostrando:
  - Menu → TargetSelection → (Confirm → onSelected) / (Back → reabrir menú, no onCancel) / (CancelTurn → onCancel).
  - CP intent permanece activo en Back.

## Pruebas manuales
- Attack/Magic/Item → TargetSelection → ESC: regresa al submenú, turno sigue, sin timeline.
- Confirm target: commit único, EndTurn.
- Cancel duro en menú raíz: termina turno, cpIntent Cancel/EndTurn.

## Notas de compatibilidad
- Si cambiar firmas rompe, añadir overloads temporales (SelectAsyncEx) y migrar provider primero.
- Definir claramente a qué submenú regresa Back (stack de menús o estado previo del provider).

