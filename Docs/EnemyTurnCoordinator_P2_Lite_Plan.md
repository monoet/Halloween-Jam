# P2-lite: helpers puros en shadow-mode (plan seguro)

**Objetivo:** introducir helpers de targeting y resolve sin tocar timings ni pipeline, con pasos pequenos, reversibles y con criterios de exito medibles.

## Reglas P2-lite (candados)
1. Cero cambios de timing: no tocar `await`, ni `Advance/RequestNextTurn`, ni el orden de llamadas.
2. Helpers 100% puros: input -> output, sin mutar listas ni estado, sin side effects.
3. Snapshot obligatorio en los boundaries: toda lista que cruza a Execute va como array nuevo.
4. Shadow-mode primero: el helper corre y solo loguea diffs **contra snapshots del camino viejo**. El flip a "use helper" es posterior y acotado.
5. Rollback por chunk: cada hook/log tiene bandera dev-only para apagarlo en 10 segundos.
6. GC safety: con DevFlowTrace OFF, no hay interpolacion ni `ToString`; sin allocs nuevos.

### Smoke scenarios (DevFlowTrace ON, N turns)
1) Enemy single-offensive -> 1 target party.  
2) Enemy sin targets validos -> NOOP.  
3) Enemy confusion/self-target (si aplica) -> deny (P0).  
4) Player single-offensive -> 1 target enemy.  
5) AoE (si aplica) -> se loggea `SKIP` (NotOffensiveSingle), no se toca.

**Criterio Done (Chunks 1-3 en shadow):** en 50 turns de combate, `P2L_*_DIFF` = 0.

---

## Chunks pequenos (cada uno PR corto y reversible)

### Chunk 0 — Infra minima de "Reason Codes" + snapshot (scope fence)
**Archivos:** `Execution/TargetSnapshot.cs`, `Execution/TargetResolveFailReason.cs`.  
**Que metes:**
- `enum TargetResolveFailReason` (Ok, NoTargets, SelfOnly, NotOffensiveSingle, InvalidShape, etc.).
- `static class TargetSnapshot`:
  - `IBattler[] Snapshot(IReadOnlyList<IBattler> src)` (clona).
  - `string StableIds(...)` (solo DevFlowTrace).
- (Opcional) `Debug.Assert` en dev: sin duplicados, sin nulls.  
**Done:** compila, no cambia gameplay (aun no se usa).  
**Rollback:** flag `EnableP2LiteSnapshotLog` (dev).

### Chunk 1 — Helper de listas orientadas (formaliza P1 sin cambiar logica)
**Archivos:** `Execution/TargetLists.cs`, 1 callsite en `EnemyTurnCoordinator` (solo log).  
**Que metes:** `TargetLists BuildListsFromAttacker(attacker, party, enemies)` -> `sameSide[]`, `opponents[]`, `bool selfInOpponents`.  
**Integracion (solo shadow):**
- Sigues usando las listas viejas para decidir.
- El helper genera listas y loguea si hay diferencia.  
**Logs canonicos:**  
`P2L|LISTS|exec=<id>|att=<id>|act=<id>|same=[...]|opp=[...]|selfInOpp=<0/1>`  
**Done:** `TARGET_LISTS` y `P2L_LISTS` coinciden en smoke tests (mismatches=0).  
**Rollback:** flag `EnableP2LiteListsShadow`.

### Chunk 2 — Helper "TryResolveTargetsLite" (valida + snapshot, sin ejecutar)
**Archivos:** `Execution/TryResolveTargetsLite.cs` + callsite shadow en `EnemyTurnCoordinator`.  
**Que hace:** Entrada: attacker, actionId/shape, candidateTargets, sameSide/opponents orientados.  
Salida: `ResolvedTargetsLite { bool HasRecipients; TargetResolveFailReason FailReason; IBattler[] Recipients; }`  
- Regla: `HasRecipients == true` implica `FailReason == Ok`; si `FailReason != Ok`, entonces `Recipients.Length == 0`.  
**Shadow-mode:** snapshot del flujo viejo vs `Recipients`; si difieren, log `P2L|DIFF|exec=..|where=RESOLVE|old=[..]|new=[..]`.  
**Logs canonicos:**  
`P2L|RESOLVE|exec=<id>|att=<id>|act=<id>|ok=<0/1>|reason=<Reason>|rec=[...]`  
**Done:** en 50 turns de smoke tests, `P2L_*_DIFF = 0`.  
**Rollback:** flag `EnableP2LiteResolveShadow`.  
**Skip:** acciones no single/offensive -> `P2L|SKIP|exec=..|act=..|why=NotOffensiveSingle`.

### Chunk 3 — Helper "BuildExecutionRequestLite" (empaquetar datos, sin pipeline)
**Archivos:** `Execution/ExecutionRequestLite.cs` + hook de log.  
**Que hace:** DTO inmutable con `execId`, `attackerId`, `actionId`, `recipients[]`, `sameSide[]`, `opponents[]`, flags (failReason).  
**Logs canonicos:**  
`P2L|REQ|exec=<id>|att=<id>|act=<id>|rec=[...]|reason=<Reason>`  
**Uso:** solo logging/correlacion (trazabilidad por execId).  
**Done:** logs se emiten en dev, sin tocar pipeline.  
**Rollback:** flag `EnableP2LiteReqLog`.

### Chunk 4 — Flip controlado: `UseP2LiteResolve` (solo si shadow limpio)
**Archivos:** `BattleDiagnostics.cs` + 1 if-flag en callsite.  
**Bandera dev:** `UseP2LiteResolve` **y** filtro opcional (ej. `P2LiteOnlyForEnemies` o `P2LiteFilterAttackerId`).  
- Si ON: usas `Recipients` del helper para execute; sigues logueando diffs.  
**Done:** gameplay identico en smoke tests; si algo falla, el log indica LISTS o RESOLVE.  
**Rollback:** apagar flag; vuelve al camino viejo.

### Chunk 5 — Replicar al flujo de Player (opcional)
- Mismo patron, mismos helpers. Puede quedar en shadow permanente.  
**Rollback:** mismos flags por chunk.

---

## Que ganas
- Evitas aliasing/mutacion de listas (snapshot en boundary).
- Caja negra clara: si se rompe algo, sabes si fue listas, resolve/filtrado o downstream (pipeline/middleware).

## Formato de log (canonico, grepeable)
Prefijo fijo: `P2L|<TAG>|exec=<id>|att=<id>|act=<id>|...`  
Orden fijo de campos; ids estables (`E3`, `P1`, etc).  
Ejemplos:
- `P2L|LISTS|exec=12|att=E3|act=Slash|same=[E1,E2,E3]|opp=[P1,P2,P3]|selfInOpp=0`
- `P2L|RESOLVE|exec=12|att=E3|act=Slash|ok=1|reason=Ok|rec=[P1]`
- `P2L|DIFF|exec=12|where=RESOLVE|old=[P1]|new=[P1,P2]`
- `P2L|SKIP|exec=12|act=Roar|why=NotOffensiveSingle`

## Que NO entra en P2-lite
- No unificar Execute.
- No tocar ActionPipeline, PhaseDamageMiddleware, AdvanceTurn.
- No meter planner models (ActionPlan/RecipientPlan) aun.

## Checklist rapida
- [x] Chunk 0: enums + snapshots listos (sin uso). Flag rollback: `EnableP2LiteSnapshotLog`.
- [x] Chunk 1: helper de listas en shadow, logs sin diffs. Flag rollback: `EnableP2LiteListsShadow`.
- [x] Chunk 2: resolve lite en shadow, logs sin diffs. Flag rollback: `EnableP2LiteResolveShadow`. Skip tag para no-single.
- [x] Chunk 3: request lite para logging, sin pipeline. Flag rollback: `EnableP2LiteReqLog`.
- [ ] Chunk 4: flag `UseP2LiteResolve` + filtros por attacker/action; diffs vigilados. Rollback: apagar flag.
- [ ] Chunk 5: replicar a Player (opcional) con mismos flags.
