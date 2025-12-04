# MVP Plan: SINGLE/ALL + Ally/Enemy + TimedHit/CP-gated Marks (ActionJudgment)

## Alcance del MVP
- Unificar targeting con Side (Self/Ally/Enemy) + Scope (Single/All). *(Any queda para fase posterior)*
- TargetSet lista-capable con alias Primary (backcompat).
- TargetResolver para Single/All sin romper el pipeline actual.
- ActionJudgment/TargetJudgment: cpSpent + rngSeed + timedGrade (None→Fail/Success/Perfect) inyectados en el contexto de ejecución.
- MarkService + MarkDefinition + MarkRuleEvaluator: Apply/Detonate gateados por CP + TimedHit, sin alterar acciones actuales por defecto.
- UI: si Scope=All, saltar selección y confirmar todos los objetivos; opcional icono Single/All. Single sigue igual.
- Cobro de recursos: CP/SP una sola vez por acción (no por target).
- Snapshot de targets: para All, la lista se congela en commit (inmutable durante la ejecución).

## Core intent (resumen)
- No cambia acciones actuales por defecto.
- Soporta marks de KS y elementales (single + all).
- Prepara “apply luego detonate en la misma acción” sin hacks por personaje.

## Nuevos paradigmas
- Slot único de mark por target (MVP): máximo 1 mark activa; aplicar reemplaza/refresca; feedback puede repetirse.
- Multi-hit sin flags por personaje: cada impacto puede emitir feedback de apply; MarkService sigue con un solo slot.
- Orden por impacto: **Detonate primero (preHitMark) → luego Apply**. Evita perder detonación al sobrescribir.
- Regla emergente: si no quieres detonar, no pegues con elemento distinto + CP; mismo elemento refresca sin detonar.

## Fases y tareas

### Fase 1: Contratos base y compatibilidad
- [ ] Agregar enums TargetSide (Self/Ally/Enemy) y TargetScope (Single/All) con defaults (Enemy+Single para ofensivas actuales).
- [ ] Extender ActionData con side/scope (defaults para backcompat).
- [ ] Actualizar TargetSet: soportar lista (IReadOnlyList<Combatant>), con Primary como alias de Targets[0]. Helpers: FromSingle(primary), FromMany(list).
- [ ] Asegurar que todo el código existente que asume single lea Primary (no romper comportamiento actual).

### Fase 2: Resolver de objetivos (y snapshot)
- [ ] Implementar TargetResolver.GetTargets(source, side, scope, optionalSelected):
  - Self: ignora scope, devuelve solo source.
  - Enemy/Ally: Single usa seleccionado; All devuelve todos vivos de esa facción.
- [ ] Integrar resolver en el flujo actual:
  - Acciones Single se comportan igual.
  - Acciones All crean TargetSet con la lista capturada en commit (snapshot).

### Fase 3: ActionJudgment / TargetJudgment
- [ ] Crear ActionJudgment (cpSpent, timedGrade, scope, side, rngSeed, actionId, sourceActorId) y adjuntarlo al contexto de acción.
- [ ] Crear TargetJudgment (targetIndex, perTargetRng derivado de rngSeed + id estable + index).
- [ ] Set cpSpent en commit (según input CP intent). Set rngSeed determinista (turn seed + actor + acción).
- [ ] timedGrade inicia en None; se actualizará cuando TimedHit termine.

### Fase 4: TimedHit integración (solo gate, sin cambiar daño base)
- [ ] Suscribirse al resultado canónico de TimedHit y actualizar timedGrade en ActionJudgment.
- [ ] Regla: si una acción no produce TimedHit → timedGrade permanece None.
- [ ] No alterar daño base; solo exponer timedGrade para gates.

### Fase 5: Marks (Apply/Detonate) con gates CP+TimedHit
- [ ] Crear MarkService (ApplyMark, HasMark, DetonateMark, ClearOnDeath).
- [ ] Crear MarkDefinition catálogo (SO o diccionario) con efectos de detonate simples al inicio (bonus damage/cc/heal).
- [ ] Añadir MarkRule en ActionData (lista; default vacío para acciones actuales).
- [ ] Implementar MarkRuleEvaluator (única autoridad para detonar):
  - GatePolicy (requiresCp, requiresTimedSuccess, minGrade)
  - ChanceProfile (singleChance, allChance, rollPerTarget)
  - DetonateProfile (requiresMarkPresent, consumeMark, maxDetonationsPerCast default -1, scopeOverride default)
  - Orden fijo (documentado): Evaluar Apply rules primero; luego Detonate rules.
- [ ] Evaluar por target usando ActionJudgment + TargetJudgment; aplicar/detonar según gates. Sin cambios para acciones sin MarkRules.

### Fase 6: UI (All scope) y recursos
- [ ] UI: si scope=All, saltar target selection y llamar ConfirmAllTargets (usa TargetResolver + snapshot).
- [ ] Cobro de CP/SP: asegurar que ocurre una vez por acción antes del loop per-target.
- [ ] Opcional: icono Single/All basado en scope (sin comportamiento extra).

### Fase 7: Verificación y regresión
- [ ] Regresión: acciones single-target siguen idénticas (daño, selección, recursos).
- [ ] ALL ofensivo: golpea todos los enemigos, cobra CP/SP una vez.
- [ ] Marks: solo se aplican/detonan en acciones con MarkRules; detonate requiere gates (CP + timed success si así se define).
- [ ] Determinismo: RNG por target estable dado rngSeed (derivado por target).

## Riesgos y mitigaciones
- Romper single-target → usar TargetSet.Primary y defaults side/scope en Enemy+Single.
- Doble cobro CP/SP en ALL → cobrar antes del loop per-target.
- Detonación en Fail/None → Gate requiresTimedSuccess + minGrade; sin TimedHit, timedGrade=None y gates fallan.
- Aleatoriedad no determinista → Derivar perTargetRng de rngSeed + target id estable + index (sin Random global).

## Instrumentación (debug opcional)
- Log ActionJudgment en commit (cpSpent, scope, seed).
- Log decisiones per-target (apply/detonate + roll) bajo flag de debug.
- Log timedGrade recibido de TimedHit.

## Dependencias mínimas
- TargetSet lista-capable y TargetResolver.
- Eventos de TimedHit disponibles para setear timedGrade.
- Contrato de Damage/ActionContext estable para adjuntar ActionJudgment.

## Fase posterior al MVP (bucket difícil #3)
- Detonación multi-efecto avanzada (MarkDetonatedEvent + primitivas): BonusDamage, HealCaster, ApplyStatus, SpreadMark, GainResource; orden fijo ConsumeMark -> EmitEvent -> ApplyPrimitives; sin recursión ni cadenas en el mismo tick; knobs mínimos (maxDetonationsPerCast, scopeOverride).
- Axis como catalizador (AxisDetonationBonus: None|Pierce|Blunt|Slash): no crea marks, solo detona si hay mark válida y gates pasan; bonus como primitivas; sin mark = golpe normal.
- Ciro 3-wave skill-check (BattleState_CiroSacrifice): 3 timed hits; coro de animaciones de enemigos (solo visual/SFX, sin AI); daño party si falla cada ola; 3 éxitos -> retaliación ALL vía pipeline normal; helper EnemyChorusAnimator.
- Límites: no entangle con AI/enemigos, no matrices RNG complejas, no detonaciones recursivas en mismo tick.
- Cooldown de interacción con Marks por turno: flag por target (`markInteractionUsedThisTurn`) que se activa al detonar una Mark; mientras esté activo, no se pueden aplicar nuevas Marks a ese target en el mismo turno. Se limpia al finalizar el turno del actor que detonó. Permite evitar loops y habilita futuras excepciones/pasivas que ignoren esta regla.
- Multi-efecto avanzada y Axis siguen el mismo orden por impacto: detonate con el mark previo al hit y luego apply/bonus según reglas; sin crear estados fantasma fuera de MarkService.

## Aclaraciones de contrato (para evitar ambigüedad)
- Mark válida para detonar: solo marks registradas en MarkService (persistentes). Axis u otros estados no cuentan como marks.
- Sin mark: no hay detonate ni bonus; se aplica solo el hit normal (aplica también a Axis en fase posterior).
- Orden por target: la lógica se resuelve en el orden estable de la lista Targets; cualquier delay es solo visual y no altera el orden de aplicación.
- Snapshot: no se recalculan targets durante la ejecución aunque mueran o se invoquen unidades nuevas.
- Guardrail de ritmo: todo lo de Marks (apply/detonate/gates) ocurre en el mismo instante del impacto; no agrega fases del turno, ni ventanas extra de input, ni pendientes de detonar, ni modifica el orden de Damage/Death/Cleanup. Las Marks solo modifican el resultado del impacto actual, nunca el flujo de turnos.
