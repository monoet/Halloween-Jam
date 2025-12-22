# CP Diagnostics – current findings (2025-##-##)

## Quick summary
- Sintoma reportado: con 1 CP el multiplicador aplica correctamente; con 2+ CP el daño **baja** en vez de subir.
- El scaling teórico siempre es >=1:
  - `ComboPointScaling.DefaultProceduralMultiplier`: 1→1.15, 2→1.40, 3→1.90, 4→2.90, 5→4.90…
  - `ComboPointScalingProfile` (por defecto) trae `explicitMultipliers = [1.15, 1.40, 1.90, 2.90]` y extrapola creciendo (no debería bajar).
- Sospecha inmediata: el CP que llega al scaling no es el que crees (e.g. CP charge está siendo truncado/negativizado o se está usando otra ruta de daño).

## Dónde vive cada pieza CP
- **Selección/entrada**:
  - `BattleManagerV2.RequestPlayerAction` fija `MaxCpCharge = player.CurrentCP`.
  - `BattleUIInputProvider` guarda `pendingCp` y arma `BattleSelection` con `cpCharge = pendingCp` (clamp 0..MaxCpCharge).
  - Estrategias de carga (notched/hold) viven en `BattleV2.Charge.*` y `BattleDebugHarnessV2` (para práctica).
- **Cobro de recursos**:
  - `PlayerActionExecutor.ExecuteAsync` calcula `cpCost = cpBase + cpCharge` y cobra en `ChargeSelectionCosts(...)`.
  - Logs útiles: `PAE.BUITI`, `Thread.debug00`, y warning `[CP/SP] Expected CP charge but none occurred`.
- **Scaling de daño**:
  - `ComboPointScaling.GetDamageMultiplier(cpCharge)` → usa `ActiveProfile` (configurado en `BattleManagerV2` con `BattleConfig.comboPointScaling`) o el procedural default.
  - Usado en acciones: `SimpleAttackAction`, `MagicAttackAction` (single y multi), `CriticalBurstAction`, `LunarChainAction`.
- **Modelo de datos CP**:
  - `CombatantState` guarda `CurrentCP/MaxCP`, `SpendCP/AddCP`, logging en `CombatantState.cs:298+`.
  - `ActionData.cpCost` es el costo base (ActionCatalog).

## Pistas/focos para el bug actual
1) **¿Qué CP llega al multiplicador?**
   - Hay clamps a `Mathf.Max(0, cpCharge)` en las acciones y en el executor; si `pendingCp` fuera negativo o si `MaxCpCharge` se setea a 0 por error, terminarías con `cpCharge=0` (daño menor).
   - `BattleManagerV2` recalcula `MaxCpCharge` en `RequestPlayerAction` como `CurrentCP`; si en el turno se reduce antes de la selección, podrías estar capando el charge.
2) **¿Perfil activo vs. curva rara?**
   - `ComboPointScaling.ActiveProfile` se configura con `BattleConfig.comboPointScaling` en `BattleManagerV2` (líneas ~258, ~557). Si el asset tiene una `AnimationCurve` o `explicitMultipliers` editada (p.ej. 1:1.15, 2:0.8), podrías ver caída. No hay fallback a “no bajar” más allá de `Mathf.Max(0.01, multiplier)`.
3) **Rutas alternativas de daño**:
   - Algunas acciones multi-objetivo (`MagicAttackAction.ExecuteMulti`) recalculan daño por target; validar que `cpCharge` llega igual.
   - Timed hits (`TimedHitMiddleware`, `PhaseDamageMiddleware`) usan `selection.CpCharge` para tiers; revisar que no haya efecto inverso en el tier seleccionado.

## Qué revisar ya (sin código nuevo)
- En runtime, capturar estas líneas:
  - De la acción: el log de daño (`SimpleAttackAction`/`MagicAttackAction`) incluye `Charge X, Mult Y`; verifica que `cpCharge` y `cpMultiplier` sean coherentes cuando usas 2+ CP.
  - De `PAE.BUITI` y `ActionCharge`: ver `cpCharge` y `cpTotal` cobrados para esa selección.
  - Si `ComboPointScaling` está usando perfil: inspeccionar en escena/asset `BattleConfig.comboPointScaling` → `explicitMultipliers`, `multiplierCurve`, `softCap`.
- Abrir el asset del perfil de CP: confirma que `explicitMultipliers` o la `multiplierCurve` no tengan valores <1 en CP 2+ y que el `clampCurveToLastKey` no esté cortando antes de tiempo.

## Diagnóstico general de sistemas CP
- **Flujo**: Selección (UI/intent) → `BattleSelection.CpCharge` → `PlayerActionExecutor` cobra `cpCost` (base+charge) → Acción calcula daño con `cpCharge` → Event/battle flow.
- **Conmutadores**:
  - Perfil de scaling (`BattleConfig.comboPointScaling`), si null usa procedural.
  - `ChargeProfile.MaxCpSpendFactor` (en acciones) limita el CP que se puede gastar en carga (vía estrategia de carga, no en cálculo directo).
- **Puntos de falla probables**:
  - Perfil de scaling mal configurado (valores <1 o curva descendente).
  - `MaxCpCharge` / `pendingCp` calculado/capado en 0 por algún flujo de estado del turno (se vería en logs de cobro).
  - Algún middleware de daño que reinterpreta `cpCharge` (timed hits/tier) y termina usando tier menor.

## Recomendación inmediata
- Log puntual (dev-only) en `ComboPointScaling.GetDamageMultiplier` para una sesión: `cpCharge`, `profile?`, `explicit/curve value`, `final multiplier`, y compararlo con el log de la acción. Esto aclara si la caída viene del perfil o de otro lado.
- Si el perfil es el default y aún así cae, clavar un log en `PlayerActionExecutor` con `cpCharge` y en la acción con el `cpMultiplier` observado para el mismo executionId. That will show si el `cpCharge` cambia en el camino.

