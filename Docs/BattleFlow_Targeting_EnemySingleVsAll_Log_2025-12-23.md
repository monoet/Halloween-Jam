# BattleFlow + Targeting (Enemy Single vs All) — Diagnostic Log (2025-12-23)

## Objetivo
Dejar un rastro **determinístico** (con un solo tag de consola: `BATTLEFLOW`) para investigar y corregir:

- Enemigos que “parecen” pegar a todo el party (como si fuera `ALL`) incluso en ataques básicos.
- Asegurar contrato: **Single-target solo daña al target resuelto**, `All` solo cuando el action lo declara.

## Resumen ejecutivo
- El sistema ya tiene `TargetSet`/`Targets` en runtime (via `TargetingCoordinator`).
- La expansión a multi-target ocurre **solo** si el `IAction` implementa `IActionMultiTarget`.
- Se añadió instrumentación `BATTLEFLOW` para correlacionar en un solo vistazo:
  - `TARGET_RESOLVE` (qué targets se resolvieron)
  - `PIPELINE_ENTER` (qué pipeline se ejecutó)
  - `PIPE_MULTI_TARGET` vs `PIPE_LEGACY` (si se usó multi-target o legacy single-target)
  - Guardrail: `WARN_TARGET_MISMATCH` (si data dice Single pero se intentó aplicar a varios)

## Hipótesis (root causes candidatas)
1) **Data**: el action del enemigo está configurado con `targetShape=All` (AoE real).
2) **Pipeline**: el action del enemigo usa un `IActionMultiTarget` y por eso se está expandiendo a la lista de targets.
3) **Triggered effects**: el daño extra viene de `TriggeredEffectsService` y “se siente” como AoE aunque el golpe primario sea single.

## Instrumentación añadida (tag único)
Todo lo de esta investigación se imprime bajo `BATTLEFLOW` cuando `BattleDiagnostics.DevFlowTrace=true`.

### 1) Targeting del enemigo (origen del “quién fue target”)
Archivo:
- `Assets/Scripts/BattleV2/Orchestration/Services/EnemyTurnCoordinator.cs`

Log:
- `BATTLEFLOW TARGET_RESOLVE exec=... actor=... action=... shape=... setGroup=... setIds=[...] targets=[...]`

Interpretación:
- Si `shape=Single` y `targets=[X]` pero el daño termina aplicándose a varios, el bug **no** está en targeting.
- Si `shape=All` y `targets=[X,Y,...]`, es AoE “por diseño” (data).

### 2) Pipeline seleccionado + recipients (dónde se pierde / se expande)
Archivo:
- `Assets/Scripts/BattleV2/Orchestration/Services/ActionPipeline.cs`

Logs:
- `BATTLEFLOW PIPELINE_ENTER exec=... actor=... action=... shape=... targets=[...]`
- `BATTLEFLOW PIPE_MULTI_TARGET exec=... ... recipients=[...]` (cuando `IActionMultiTarget`)
- `BATTLEFLOW PIPE_LEGACY exec=... ... target=...` (cuando legacy single-target)

Interpretación:
- Si aparece `PIPE_MULTI_TARGET`, la acción está corriendo por la vía multi-target (por interface).
- Si aparece `PIPE_LEGACY`, la acción corre como single-target y el “recipient” real es `target=...`.

### 3) Triggered effects (para separar “daño base” vs “daño extra”)
Archivo:
- `Assets/Scripts/BattleV2/Orchestration/Services/TriggeredEffectsService.cs`

Logs:
- `BATTLEFLOW TRIGGER_BEGIN exec=... origin=... action=... targets=N`
- `BATTLEFLOW TRIGGER_END exec=... origin=... action=... targets=N`

Interpretación:
- Si el “golpe primario” fue single pero luego hay `TRIGGER_BEGIN` con `targets>1`, el AoE puede venir de efectos secundarios.

## Guardrail implementado (fix mínimo y seguro)
Archivo:
- `Assets/Scripts/BattleV2/Orchestration/Services/ActionPipeline.cs`

Comportamiento:
- En acciones **multi-target** (`IActionMultiTarget`) donde el `BattleActionData.targetShape == Single` pero la lista de `targets` llega con `Count > 1`:
  - Log: `BATTLEFLOW WARN_TARGET_MISMATCH ...`
  - Se clampa a 1 receptor (el primero):
    - Log: `BATTLEFLOW TARGET_MISMATCH_CLAMP ...`

Motivo:
- Evitar que un error de wiring/config haga que una skill single-target “pegue a todos”.
- Mantener el combate jugable mientras se identifica el origen real.

> Nota: este guardrail no “arregla” un AoE real (cuando `targetShape=All`); solo protege el caso “Single declarado, multi aplicado”.

## Estado de la data (lo que ya se ve en assets)
Archivo:
- `Assets/Scripts/01_Data/Scriptable Objects/Battle/Config/Action Catalog.asset`

Observaciones:
- `basic_attack_enemy` actualmente está como:
  - `requiresTarget: 1`
  - `targetAudience: Enemies`
  - `targetShape: Single`
- `magic_bolt` está como `targetShape: All` (esto sí pega a todos por diseño).

Implicación:
- Si “el enemigo pega a todo”, confirmar que realmente el enemigo está usando `magic_bolt` y no `basic_attack_enemy`.

## Protocolo de repro (1 corrida)
1) Activar `DevFlowTrace` en `BattleDebugTogglesBehaviour` (Inspector).
2) Filtrar consola por: `BATTLEFLOW`
3) Repro mínimo: 2 party members vivos + 1 enemigo.
4) Dejar que el enemigo ataque (idealmente 3 veces).

Qué buscar:
- Secuencia típica del enemigo:
  - `BATTLEFLOW TARGET_RESOLVE ... shape=<Single|All> targets=[...]`
  - `BATTLEFLOW PIPELINE_ENTER ... shape=<Single|All> targets=[...]`
  - `BATTLEFLOW PIPE_MULTI_TARGET ... recipients=[...]` **o** `PIPE_LEGACY ... target=...`

Diagnóstico rápido:
- `shape=All` y `targets=[...]` => AoE real (data/acción).
- `shape=Single`, `targets=[1]`, pero `PIPE_MULTI_TARGET recipients=[2+]` => mismatch (guardrail lo clampea; revisar acción/implementation).
- `shape=Single`, `PIPE_LEGACY target=...`, pero ves daño múltiple => probablemente triggered effects o aplicación de daño fuera del pipeline (revisar `TRIGGER_BEGIN`).

## Siguiente paso (si persiste)
- Si `TARGET_RESOLVE` confirma `shape=Single` y aun así hay AoE:
  - Revisar qué `IAction` está corriendo para esa acción (si implementa `IActionMultiTarget`).
  - Revisar si hay efectos/marks que disparen daño adicional con lista completa de targets (`TriggeredEffectsService`).

