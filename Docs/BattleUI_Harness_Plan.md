# Battle UI & Harness V2 Refactor Plan

## Objetivo
- Desacoplar el harness de responsabilidades de ejecución.
- Introducir una UI Alpha que hable con BattleManager/TimedHitService sin lógica de combate en la UI.
- Convertir el harness en herramienta de debug/sniffer post-Alpha.

## Fase 1: Limpieza del Harness (BattleDebugHarnessV2)
- Remover hijack del runner:
  - Eliminar `ClaimRunnerOwnership`, `EnsureTimedRunnerOwnership`, `RestoreRunnerOwnership`.
  - Quitar registro de `ITimedHitRunner` en el harness; dejar runner oficial del `TimedHitService`.
- Remover reflection del input:
  - Eliminar acceso a `inputProvider` privado y reinyección en `Update`.
  - Dejar que el provider oficial (UI) se registre via `BattleManagerV2.SetRuntimeInputProvider`.
- Congelar escrituras de estado:
  - Desactivar/condicionar `Grant 5 CP`, cambios a `pendingContext.MaxCpCharge`, llamadas directas a `AddCP`.
  - Solo habilitar en `#if UNITY_EDITOR || DEBUG` si se requiere.
- Validar entorno post-limpieza:
  - Verificar que `TimedHitService` tiene un runner oficial configurado (por ejemplo `InstantTimedHitRunner`) sin depender del harness.
  - Verificar que existe un `IBattleInputProvider` oficial registrado antes de iniciar el primer turno (ej. `BattleUIRoot` o un provider dummy).
- Resultado: el harness no altera combate; queda como herramienta opcional de debug.

## Fase 2: UI Alpha – Estructura
- Crear `BattleUIRoot` (`Assets/Scripts/BattleV2/UI/`):
  - Referencias a paneles: ActionMenuPanel, TargetSelectionPanel, CPChargePanel, TimedHitPanel, Info/Tooltip (opcional).
  - Expone eventos hacia BattleManager: `OnActionSelected`, `OnTargetSelected`, `OnChargeCommitted`, `OnTimedHitPressed`, `OnCancel`.
  - Gestiona estados básicos: Root, Submenú (Atk/Mag/Item), Target_Select, Confirm_Action, Locked (enemy turn).
  - No contiene lógica de combate ni de timing; solo enruta UI → BattleManager.
- Registrar `BattleUIRoot` como `IBattleInputProvider` oficial usando `BattleManagerV2.SetRuntimeInputProvider` al iniciar escena.

## Fase 3: Paneles
- ActionMenuPanel (usar arrays verticales existentes):
  - Botones: Attack, Magic, Item, Defend, Flee.
  - Eventos: `OnAttack`, `OnMagic`, `OnItem`, `OnDefend`, `OnFlee`, `OnClose`.
- AtkMenuPanel:
  - Lista de ataques físicos (KS1/KS2/KS3 o disponibles).
  - Muestra/oculta `CPChargePanel` (policy: required).
  - Eventos: `OnAttackChosen(actionId)`, `OnBack`.
- MagMenuPanel:
  - Lista de hechizos; cada entrada define política CP (none/optional/required) y target (self/ally/enemy).
  - Muestra/oculta `CPChargePanel` según spell.
  - Eventos: `OnSpellChosen(spellId)`, `OnBack`.
- ItemMenuPanel:
  - Lista de ítems; sin CP.
  - Eventos: `OnItemChosen(itemId)`, `OnBack`.
- CPChargePanel:
  - Slider/stepper CP; emite `OnChargeCommitted(int cp)` o `OnChargeNormalized(float)`.
  - Solo visual; tope y costos los decide BattleManager.
- TargetSelectionPanel:
  - Recibe lista de targets desde BattleManager (self/ally/enemy).
  - Resalta selección (reusar layout vertical si sirve).
  - Eventos: `OnTargetSelected(targetId)`, `OnCancel`.
  - Puede implementar `ITargetSelectionInteractor` real (sin harness).
- TimedHitPanel:
  - Botón/tecla → `OnTimedHitPressed`.
  - Barra simple de ventana (ALPHA) cuando BattleManager/TimedHitService emita window open/close.
- CombatInfoPanel (opcional ALPHA):
  - Texto de estado (acción, target, hints). Sin lógica.

## Fase 4: Integración con BattleManager
- Eventos UI → Manager:
  - Acción/CP/Target → `BattleManager.RequestAction` vía `IBattleInputProvider`.
  - Target → `ITargetSelectionInteractor` real.
  - Timed hit → reenviar input al runner oficial (`TimedHitService`).
  - Cancel → volver a Root sin efectos colaterales.
  - La asignación de CP (`OnChargeCommitted` → `pendingContext` + `ChargeProfile`) vive en `BattleManager` o un helper de CP; ningún panel ni el harness escribe CP directo.
- Respuestas Manager → UI:
  - `OnPlayerActionSelected/Resolved` → actualizar `CombatInfoPanel`/prompts.
  - `TimedHitService` (window open/close/result) → actualizar `TimedHitPanel`.
  - Turno enemigo → `BattleUIRoot` pasa a Locked, deshabilita menús.

## Fase 5: Escena y Prefabs
- Prefab `BattleUIRoot` con hijos:
  - `RootMenu`, `AtkMenu`, `MagMenu`, `ItemMenu`, `CPChargePanel`, `TargetSelectionPanel`, `TimedHitPanel`, `CombatInfoPanel` (opcional).
- Asignar referencias en el inspector a `BattleUIRoot`.
- Instanciar prefab en escena de combate y enlazar con `BattleManagerV2`.

## Fase 6: Harness como Sniffer (post-Alpha)
- Suscribirse a eventos (`OnPlayerActionSelected/Resolved`, `TimedHitService`, StepScheduler).
- Dibujar/loggear en IMGUI sin ser runner ni provider ni tocar CP.
- Práctica de timing: opcional. Si se conserva, debe usar el runner oficial a través de un `TimedHitPracticeService` independiente. Si no aporta valor, se elimina.

## Guardas y Flags
- Tooling intrusivo (grant CP, hijack) detrás de `#if UNITY_EDITOR || DEBUG`.
- En build, el harness no debe registrarse como provider ni runner.

## Validación
- Escena: flujo Attack → CP → Target → Confirm → TimedHit → Resolve sin harness.
- Verificar que `TimedHitService` tiene runner activo sin hijack.
- Confirmar que la UI no modifica estado de combate directamente; solo comunica elecciones.
