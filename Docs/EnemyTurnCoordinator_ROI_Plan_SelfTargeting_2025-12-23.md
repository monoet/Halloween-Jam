# Progreso / Checklist (ROI vs Riesgo)
- [x] P0 Airbag (NOOP si sin target, deny self-target, pick solo ofensivas single) � ROI Max, Riesgo Bajo
- [x] P1 Claridad listas (opponents/sameSide + log TARGET_LISTS) � ROI Alto, Riesgo Bajo
- [ ] P2 Helpers internos (Pick/Resolve/Execute) � ROI Medio, Riesgo Bajo
- [ ] P3 Centralizar logs � ROI Medio/Bajo, Riesgo Bajo
- [ ] P4 Revisar TargetingCoordinator (heuristicas/fallback) � ROI Alto, Riesgo Medio

# BattleV2: EnemyTurnCoordinator ROI Plan (Self-Targeting First) (2025-12-23)

## Context
`EnemyTurnCoordinator.cs` mezcla demasiadas responsabilidades:
- selección de acción (AI)
- targeting (policy + seed + logs)
- resolución de targets (TargetingCoordinator + resolver legacy)
- ejecución (pipeline + anim + triggered effects)
- lifecycle del turno (advance + pacing + battle end)

Síntoma prioritario: **enemigos se auto-targetean y se dañan a sí mismos por defecto**.
Esto solo debería ocurrir bajo una estrategia/estado explícito (ej. Confused), no como comportamiento estándar.

Restricción operativa:
- Todo trazado debe usar un solo tag: `BATTLEFLOW`.
- Cambios de máximo retorno/menor esfuerzo primero (KISS).

---

## Root cause (observado)
El resolver legacy (`SingleTargetResolver`) elige **first alive** de la lista según `TargetAudience`:
- `EnemiesSingle` => `FindFirstAlive(context.Enemies)`
- `AlliesSingle`  => `FindFirstAlive(context.Allies)`

Cuando el “enemigo final” es el único vivo en su bando, la heurística de `TargetingCoordinator.ResolveQuery` puede caer en `TargetAudience.Enemies` y terminar usando una lista que incluye al propio atacante (por orientación ambigua / lista incorrecta). Resultado: `targets=[attacker]`.

**Conclusión:** no basta con “pick + reorder” si el input al resolver es ambiguo o si el resolver puede devolver self; hace falta un **airbag** (guardrail) post-resolve y un contrato de “no daño si no hay target válido”.

---

## Objetivo (contrato)
1) Acciones ofensivas (`TargetAudience.Enemies`) **no pueden** resolverse a self por defecto.
2) Si no hay un target válido, el resultado estándar es **NO-OP** (sin daño) + avance del turno.
3) “Self-hit” se encapsula como strategy/estado explícito (futuro).

---

## Plan por ROI (menor esfuerzo → mayor retorno)

### P0 (hotfix / airbag) — Máximo ROI, mínimo cambio
**Meta:** matar self-damage por default aunque el resolver legacy sea imperfecto.

1) **Orientación explícita de listas antes de ResolveAsync**
   - Definir localmente en `RunEnemyActionAsync`:
     - `sameSide`  (aliados del atacante)
     - `opponents` (enemigos del atacante)
   - Pasar a `TargetingCoordinator.ResolveAsync(origin, ..., allies=sameSide, enemies=opponents)`.
   - Evita que `context.Enemies` (si contiene al atacante) sea usado como lista de víctimas.

2) **NO-OP cuando no hay target válido (no fallbacks implícitos)**
   - Para `TargetAudience.Enemies` + `TargetShape.Single`:
     - si `filteredCandidates.Count == 0` => log `BATTLEFLOW NOOP_NO_VALID_TARGET ...` y terminar el turno sin ejecutar pipeline.
   - Importante: no dejar que `EnsureFallbackSet` “invente” targets en Auto si el set estaba vacío.

3) **Post-resolve deny**
   - Después de `ResolveAsync`, si:
     - `action.targetAudience == Enemies` y `resolution.Targets` contiene `attacker`
   - Entonces: log `BATTLEFLOW NOOP_SELF_TARGET_DENIED ...` y terminar el turno sin ejecutar pipeline.
   - Esto es el “airbag”: aunque mañana se pase una lista incorrecta, no hay self-damage por default.

4) **Limitar el AI pick solo a ofensivas single-target**
   - El random pick debe correr solo para:
     - `TargetAudience.Enemies` + `TargetShape.Single` + `opponents.Count > 1`
   - Evita afectar skills de buff/heal (audience Allies/Self).

**Logs `BATTLEFLOW` (mínimos y correlacionables):**
- `AI_TARGET_CANDIDATES ...`
- `AI_TARGET_FILTER ...`
- `AI_TARGET_PICK ...`
- `NOOP_NO_VALID_TARGET ...`
- `NOOP_SELF_TARGET_DENIED ...`

**Criterio de aceptación P0:**
- Nunca aparece `TARGET_RESOLVE ... targets=[attacker]` para `TargetAudience.Enemies` en combate normal.
- Si el sistema llega a resolverse a self, se ve `NOOP_SELF_TARGET_DENIED` y no hay daño infligido.

---

### P1 (claridad semántica) — ROI alto, bajo riesgo
**Meta:** evitar “swap de listas” en el futuro por ambigüedad del naming.

1) Renombrar en `EnemyTurnContext` (o crear alias locales claros):
   - `context.Allies` / `context.Enemies` ⇒ `Opponents` / `SameSide`
   - Si no se quiere romper API ya, al menos:
     - `var opponents = context.Allies;`
     - `var sameSide = context.Enemies;`

2) Agregar un log de una sola línea (solo DevFlowTrace) para sanity:
   - `BATTLEFLOW TARGET_LISTS exec=... attacker=... sameSideN=... opponentsN=...`
   - y opcional: `containsSelfSameSide=true/false`, `containsSelfOpponents=true/false`.

**Criterio de aceptación P1:**
- No hay más cambios de “reorder la lista equivocada” por confusión semántica.

---

### P2 (extraer helper interno) — ROI medio, reduce deuda sin re-arquitectura
**Meta:** reducir el tamaño de `RunEnemyActionAsync` sin cambiar comportamiento.

Extraer 3 helpers (preferible como `private` methods en el mismo archivo para minimizar superficie):

1) `TryPickEnemySingleTarget(...)`
   - Inputs: `attacker`, `action`, `opponents`, `battleSeed`, `turnIdx`
   - Outputs: `pickedTarget` + `reorderedOpponents` + `debugInfo`

2) `TryResolveTargets(...)`
   - Encapsula `ResolveAsync` + guardrails post-resolve
   - Output: `TargetResolutionResult` o “no-op”

3) `ExecuteEnemyActionAsync(...)`
   - Pipeline + triggered + await playback + close turn

**Criterio de aceptación P2:**
- `RunEnemyActionAsync` queda legible (orquestador), y los bugs de targeting se corrigen en un solo lugar.

---

### P3 (centralizar logs) — ROI medio/bajo, evita errores de strings
**Meta:** dejar de repetir bloques gigantes de formateo (y evitar errores de compilación por strings).

Crear `BattleFlowLog` (estático) con helpers:
- `LogAiTargetCandidates(...)`
- `LogAiTargetPick(...)`
- `LogTargetResolve(...)`
- `LogNoopSelfTargetDenied(...)`

**Criterio de aceptación P3:**
- El archivo principal baja en tamaño y el logging queda consistente.

---

### P4 (mejora estructural del TargetingCoordinator) — ROI alto pero más riesgo (después)
**Meta:** quitar heurísticas frágiles y hacer la orientación determinística.

1) `ResolveQuery` hoy usa heurística basada en listas y `sideService`.
   - Se recomienda revisar:
     - detección de orientación cuando `sameSide` solo contiene self
     - evitar depender de “self relation” en `IsInRelationList`

2) `EnsureFallbackSet` para Auto hoy usa `FirstAlive(allies)` si fallback inválido.
   - Para acciones ofensivas debería caer en `FirstAlive(enemies)` (opponents), no en `allies`.
   - Idealmente: fallback depende del `TargetQuery.Audience`.

**Criterio de aceptación P4:**
- Se elimina la posibilidad de self-target por “heurística” incluso sin guardrails.

---

## Rollback / Safety
Cada fase debe ser reversible:
- P0/P1: cambios localizados; rollback = revertir `EnemyTurnCoordinator.cs`.
- P2/P3: solo extracción/organización; no debe cambiar runtime.
- P4: requiere validación más cuidadosa (mayor riesgo).

---

## Validación rápida (manual, en 1 corrida)
Con `DevFlowTrace=true`, filtrar consola por `BATTLEFLOW`:
1) 2 party vivos + 2+ enemigos.
2) Dejar que ataque el “último enemigo vivo”.
3) Confirmar:
   - `TARGET_RESOLVE ... targets=[partyMember]`
   - y nunca `targets=[attacker]`
   - si aparece self, debe aparecer `NOOP_SELF_TARGET_DENIED` y no ejecutarse pipeline.
