# Battle UI & Harness V2 Refactor Plan

## Progreso (checklist)
- [x] F1.1 Hijack del runner desactivado por defecto y sujeto a flag (`hijackTimedHitRunner`).
- [x] F1.2 Reflection/reinyeccion de `IBattleInputProvider` eliminada; registro solo si `registerAsInputProvider` esta activo.
- [x] F1.3 Escrituras de estado (CP/MaxCpCharge) protegidas por flag (`allowStateMutation`).
- [x] F1.4 Validar runner oficial activo sin harness. (Log: `TimedHitService runners | KS1=Ks1TimedHitRunner | Basic=(null)`)
- [x] F1.5 Validar `IBattleInputProvider` oficial antes del primer turno.
- [x] F2.1 Skeleton UI Alpha (UGUI) creado: `BattleUIRoot`, `ActionMenuPanel`, `AtkMenuPanel`, `MagMenuPanel`, `ItemMenuPanel`, `CPChargePanel`, `TargetSelectionPanel`, `TimedHitPanel`.
- [x] F2.2 Bridges creados: `BattleUIInputProvider` (IBattleInputProvider), `BattleUITargetInteractor` (ITargetSelectionInteractor).
- [ ] F2.3 Conectar UI a BattleManager en escena (`BattleCore_Playground`): asignar provider/interactor y prefab.
- [ ] F3/F4/F5/F6: Reconversion harness y mejoras posteriores (pendiente).

Notas rapidas:
- Runner por defecto: `AnimationSystemInstaller` configura `TimedHitService.ConfigureRunners(ks1TimedHitRunner, basicTimedHitRunner)` (Assets/Scripts/BattleV2/AnimationSystem/Runtime/AnimationSystemInstaller.cs).
- Input provider: `BattleManagerV2` resuelve primero `inputProviderComponent`/`inputProviderAsset` o `config.inputProvider`; si no hay, muestra warning y espera `SetRuntimeInputProvider` (Assets/Scripts/BattleV2/Orchestration/BattleManagerV2.cs).
- Log para F1.4: en consola busca `[AnimationSystemInstaller] TimedHitService runners | KS1=... | Basic=...` al iniciar la escena.

## Objetivo
- Desacoplar el harness de responsabilidades de ejecucion.
- Introducir una UI Alpha que hable con BattleManager/TimedHitService sin logica de combate en la UI.
- Convertir el harness en herramienta de debug/sniffer post-Alpha.

## Fase 1: Limpieza del Harness (BattleDebugHarnessV2)
- Remover hijack del runner:
  - Eliminar `ClaimRunnerOwnership`, `EnsureTimedRunnerOwnership`, `RestoreRunnerOwnership`.
  - Quitar registro de `ITimedHitRunner` en el harness; dejar runner oficial del `TimedHitService`.
- Remover reflection del input:
  - Eliminar acceso a `inputProvider` privado y reinyeccion en `Update`.
  - Dejar que el provider oficial (UI) se registre via `BattleManagerV2.SetRuntimeInputProvider`.
- Congelar escrituras de estado:
  - Desactivar/condicionar `Grant 5 CP`, cambios a `pendingContext.MaxCpCharge`, llamadas directas a `AddCP`.
  - Solo habilitar en `#if UNITY_EDITOR || DEBUG` si se requiere.
- Validar entorno post-limpieza:
  - Verificar que `TimedHitService` tiene un runner oficial configurado (por ejemplo `InstantTimedHitRunner`) sin depender del harness.
  - Verificar que existe un `IBattleInputProvider` oficial registrado antes de iniciar el primer turno (ej. `BattleUIRoot` o un provider dummy).
- Resultado: el harness no altera combate; queda como herramienta opcional de debug.

## Fase 2: UI Alpha — Estructura
- Crear `BattleUIRoot` (`Assets/Scripts/BattleV2/UI/`):
  - Referencias a paneles: ActionMenuPanel, TargetSelectionPanel, CPChargePanel, TimedHitPanel, Info/Tooltip (opcional).
  - Expone eventos hacia BattleManager: `OnActionSelected`, `OnTargetSelected`, `OnChargeCommitted`, `OnTimedHitPressed`, `OnCancel`.
  - Gestiona estados basicos: Root, Submenu (Atk/Mag/Item), Target_Select, Confirm_Action, Locked (enemy turn).
  - No contiene logica de combate ni de timing; solo enruta UI -> BattleManager.
- Registrar `BattleUIRoot` como `IBattleInputProvider` oficial usando `BattleManagerV2.SetRuntimeInputProvider` al iniciar escena.

## Fase 3: Paneles

### Concluido (MVP)
- Root Menu Panel (Attack/Magic/Item/Defend/Flee; Def/Flee existen pero no conectados).
- Atk Menu Panel: lista y seleccion funcional.
- Mag Menu Panel: poblado dinamico + scroll follow.
+- Item Menu Panel: poblado dinamico + scroll follow.
- Target Selection Panel: single target funcional.
- Timed Hit Panel: slider puntual para test (arte no final).
- CP Charge Panel: listo para produccion como UI; permite CP intent allocation en cualquier fase de seleccion.

### Pendiente / por conectar
- Defend Action: falta integrarlo al flujo.
- Flee Action: falta integrarlo al flujo.
- Target Selection: modos adicionales (multi/otros) si aplica.
- Combat Info / Tooltips: no implementado.
- Timed Hit Panel: arte/UX final cuando se defina.

### Detalle de paneles (referencia)
- ActionMenuPanel:
  - Botones: Attack, Magic, Item, Defend, Flee.
  - Eventos: `OnAttack`, `OnMagic`, `OnItem`, `OnDefend`, `OnFlee`, `OnClose`.
- AtkMenuPanel:
  - Lista de ataques fisicos.
  - Muestra/oculta `CPChargePanel` segun policy.
  - Eventos: `OnAttackChosen(actionId)`, `OnBack`.
- MagMenuPanel:
  - Lista de hechizos; cada entrada define politica CP (none/optional/required) y target (self/ally/enemy).
  - Muestra/oculta `CPChargePanel` segun spell.
  - Eventos: `OnSpellChosen(spellId)`, `OnBack`.
- ItemMenuPanel:
  - Lista de items; sin CP.
  - Eventos: `OnItemChosen(itemId)`, `OnBack`.
- CPChargePanel:
  - Slider/stepper CP; emite `OnChargeCommitted(int cp)` o `OnChargeNormalized(float)`.
  - Tope/costos los decide BattleManager.
- TargetSelectionPanel:
  - Recibe lista de targets desde BattleManager (self/ally/enemy).
  - Resalta seleccion; Eventos: `OnTargetSelected(targetId)`, `OnCancel`.
  - Puede implementar `ITargetSelectionInteractor` real (sin harness).
- TimedHitPanel:
  - `OnTimedHitPressed`; barra ALPHA para pruebas de ventana.

## Fase 4: Integracion con BattleManager
- Eventos UI -> Manager:
  - Accion/CP/Target -> `BattleManager.RequestAction` via `IBattleInputProvider`. Hay que resolver un bug relacionado al CP charge, el sintoma: cuando se utiliza 1cp todo bien, pero 2 o mas y el ataque retorna mermado.(multiplicador bajisimo)
  - Target → `ITargetSelectionInteractor` real.
  - Target -> `ITargetSelectionInteractor` real.
  - Timed hit -> reenviar input al runner oficial (`TimedHitService`).
  - Cancel -> volver a Root sin efectos colaterales.
  - La asignacion de CP (`OnChargeCommitted` -> `pendingContext` + `ChargeProfile`) vive en `BattleManager` o un helper de CP; ningun panel ni el harness escribe CP directo.
- Respuestas Manager -> UI:
  - `OnPlayerActionSelected/Resolved` -> actualizar `CombatInfoPanel`/prompts.
  - `TimedHitService` (window open/close/result) -> actualizar `TimedHitPanel`.
  - Turno enemigo -> `BattleUIRoot` pasa a Locked, deshabilita menus.

## Fase 5: Escena y Prefabs
- Prefab `BattleUIRoot` con hijos:
  - `RootMenu`, `AtkMenu`, `MagMenu`, `ItemMenu`, `CPChargePanel`, `TargetSelectionPanel`, `TimedHitPanel`, `CombatInfoPanel` (opcional).
- Asignar referencias en el inspector a `BattleUIRoot`.
- Instanciar prefab en escena de combate y enlazar con `BattleManagerV2`.

## Fase 6: Harness como Sniffer (post-Alpha)
- Suscribirse a eventos (`OnPlayerActionSelected/Resolved`, `TimedHitService`, StepScheduler).
- Dibujar/loggear en IMGUI sin ser runner ni provider ni tocar CP.
- Practica de timing: opcional. Si se conserva, debe usar el runner oficial a traves de un `TimedHitPracticeService` independiente. Si no aporta valor, se elimina.

## Guardas y Flags
- Tooling intrusivo (grant CP, hijack) detras de `#if UNITY_EDITOR || DEBUG`.
- En build, el harness no debe registrarse como provider ni runner.

## Validacion
- Escena: flujo Attack + CP + Target + Confirm + TimedHit + Resolve sin harness.
- Verificar que `TimedHitService` tiene runner activo sin hijack.
- Confirmar que la UI no modifica estado de combate directamente; solo comunica elecciones.
