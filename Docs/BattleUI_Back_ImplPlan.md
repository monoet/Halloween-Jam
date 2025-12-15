# Plan: Back ≠ Cancel en Target Selection (BattleV2)

## Objetivo
- ESC/Back en Target Selection vuelve al submenú previo (Attack/Magic/Item) sin terminar el turno ni disparar fallback/timeline.
- Commit solo al confirmar target: consume CP intent y EndTurn (una sola vez).
- Cancel duro solo desde menú raíz.

## Alcance
- Afecta Attack, Spells y Items (cualquier acción que abre Target Selection).
- Cambios en UI states, Target Interactor, Provider UI, BattleManagerV2 (split Build/Commit).
- No tocar StepScheduler ni contratos de targeting (TargetSet intacto).

## Tareas con seguimiento
- [ ] Preparar draft en BattleManagerV2 (Build vs Commit, sin side-effects al entrar a targeting).
- [ ] UI TargetSelection: ESC = Back suave (no cancel duro).
- [ ] BattleUITargetInteractor: manejar Back sin resolver cancel/empty.
- [ ] Provider UI (ManualBattleInputProviderUI/V2): consumir Back/Confirm/Cancel adecuadamente.
- [ ] BattleManagerV2 Commit: consumir CP intent una sola vez, EndTurn una sola vez.
- [ ] Validar CP intent (BeginTurn/Back/Commit/Cancel) sin regresiones.
- [ ] Pruebas manuales (Attack/Magic/Item): Back, Confirm, Cancel duro en raíz.
- [ ] Añadir invariantes/guards en BattleManagerV2 y draft (idempotencia, no side-effects fuera de commit).

## Detalle por área

### 1) BattleManagerV2 – Draft y Commit
- [ ] Introducir draft interno pequeño (struct o clase interna): actor, acción, cpIntent, targets opcional, stage, version.
- [ ] Invariantes (comentarios/guards):
  - Solo `CommitSelectionDraft()` puede: consumir CP, encolar/ejecutar, EndTurn.
  - Back nunca llama onCancel, nunca EndTurn.
  - Draft válido = hasAction; Draft listo = hasAction && targetsReady (o no requiere target).
  - Commit idempotente: si entra dos veces, no hace nada o loguea error (usar version/_commitInProgress).
- [ ] Al iniciar fase de selección: `BeginTurn(cpIntent)` y guardar acción en draft antes de TargetSelection. No consumir CP ni EndTurn.
- [ ] Nueva función `CommitSelectionDraft()`:
  - Valida acción + targets presentes.
  - Consume CP intent (ConsumeOnce) una sola vez.
  - Encola/ejecuta acción (pasos existentes).
  - Llama EndTurn una sola vez.
- [ ] Ruta Back: limpia targets del draft; mantiene acción o reabre menú según UX; no consume/cancela CP intent; no EndTurn.
- [ ] Cancel duro (menú raíz): Cancel cpIntent + EndTurn (comportamiento actual).

### 2) TargetSelectionState (UI state)
- [ ] En `HandleInput`, ESC/Back emite `TargetPickResult.Back` (o evento Back) y **no** cambia el driver directamente; el Provider decide navegación (Attack/Magic/Item). No llamar onCancel.

### 3) BattleUITargetInteractor
- [ ] `SelectAsync`: si ESC, resolver como Back (no cancelar). Limpia highlights; evita late confirm (cancelar coroutine/token y limpiar siempre en finally).
- [ ] Confirm target: outcome confirmado con TargetSet; Trigger commit más adelante.
- [ ] (Opcional) CancelTurn duro separado si se necesita desde raíz (no en targeting).

### 4) Provider UI (ManualBattleInputProviderUI/V2)
- [ ] Consumir resultado de targeting (adaptar con wrapper si hace falta para distinguir Back):
  - Confirm: asignar TargetSet al draft y llamar `CommitSelectionDraft()`.
  - Back: reabrir submenú Attack/Magic/Item, mantener draft de acción, sin llamar onCancel.
  - CancelTurn (solo raíz): onCancel → EndTurn.
- [ ] Mantener acción en “draft” hasta Confirmed; no enviar selección irreversible antes de target.
- [ ] La decisión de a qué submenú volver vive en el provider/state machine (no en el interactor); usar categoría de la acción para reabrir Attack/Magic/Item.
- [ ] Guardar en el draft la categoría/menú de origen (returnMenu o ActionCategory) al entrar a targeting; no derivarlo tarde.

### 5) CP Intent
- [ ] BeginTurn: igual (al abrir fase de selección).
- [ ] Back: no cancela ni consume; estado permanece.
- [ ] Commit: ConsumeOnce y EndTurn.
- [ ] Cancel duro: Cancel + EndTurn (menú raíz).

### 6) Audio/SFX
- [ ] Sin cambios directos; al evitar fallback/timeline en Back no se disparan flags extra.

### 7) Pruebas manuales
- [ ] Attack → TargetSelection → ESC: regresa a Attack menu, turno sigue, sin timeline ni EndTurn.
- [ ] Attack → TargetSelection → Confirm target: ejecuta una vez, EndTurn una vez.
- [ ] Menu raíz → ESC (cancel duro): termina turno, cpIntent Cancel + EndTurn (comportamiento actual).
- [ ] Items/Magic: mismo flujo (Back suave, Confirm commit, Cancel duro desde raíz).
- [ ] Anti-spam: doble Enter/Confirm no debe ejecutar dos veces (commit idempotente/version check).
- [ ] Targets dinámicos: si target muere o invalida durante targeting, commit debe revalidar y, si falla, volver a targeting sin consumir CP.
- [ ] Acciones sin target: draft listo debe permitir no abrir targeting o permitir TargetSet vacío si la acción no requiere target.

## Riesgos y mitigaciones
- Riesgo: doble consumo/doble EndTurn. Mitigación: centralizar en `CommitSelectionDraft()` y guard log/assert si se llama dos veces.
- Riesgo: provider no distingue Back/Cancel. Mitigación: wrapper mínimo (resultado con enum) solo en provider UI + interactor.
- Riesgo: regresión en items/spells. Mitigación: probar manualmente las tres rutas (Attack/Magic/Item).
- Riesgo: confirm tardío después de Back. Mitigación: cancelar SelectAsync en Back/Cancel y limpiar siempre (finally).
- Riesgo: navegación UI filtrada al core. Mitigación: TargetSelectionState emite Back/Confirm y el Provider decide navegación; no cambiar driver directo.
- Riesgo: TargetSet vacío confundido con “listo”. Mitigación: usar requiresTarget/TargetingSpec en el draft para decidir si se exige TargetSet en commit y si se abre TargetSelection.

## Spike pendiente (fuera de Assets)
- [ ] Explorar cambio de contrato (Status/Outcome en TargetResolutionResult o TargetSet.Back) en un spike fuera de producción (package/carpeta externa). Objetivo: validar Back/Cancel/Confirm con guards sin romper legacy; una vez validado, cherry-pick a Assets.
