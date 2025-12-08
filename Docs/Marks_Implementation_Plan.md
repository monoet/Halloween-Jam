# Marks System Spec — BattleV2

> Diseño “congelado” para guiar implementación y validación de Marks. Todo lo que no esté aquí se considera fuera de alcance del MVP.

---

## Checklist de implementación (progreso)

- [x] Funciones puras listas (`QualifiesForMarks_Single/AoE_Target`, `ResolveInteraction` con `canBeAppliedAsMark`)
- [x] Applier/middleware único conectado al pipeline (solo enemigos, gates + interacción + reacción) con orden: pipeline → triggered effects → playback → marks → refresh context
- [x] Hook de expiración al inicio del turno de `AppliedBy` (MVP RemainingTurns=1) usando turn counter real (prune counters en roster activo; listener OnTurnReady llama TryExpireMarkForOwnerTurn)
- [x] `CombatantState.ActiveMark` como única fuente de verdad; `MarkService` solo como hub de eventos (sin estado duplicado)
- [x] ReactionResolver recibe `ReactionKey` (+ `axisSubtype` desde AttackContext si Axis) con TryResolveId/Execute (NoOp por ahora)
- [ ] RNG AoE con seed (opcional) y gatea toda interacción por target
- [ ] UI/FX escucha `OnMarkChanged` y refleja Apply/Refresh/BlowUp/Expire/Clear
- [ ] Tests manuales: Single KS/Magic, AoE con RNG, Axis detona pero no aplica, None no interactúa

---

## 0) Propósito

Los **Marks** son un **minijuego táctico** dentro del combate: *cues visuales* y *banderas lógicas* que habilitan reacciones elementales solo cuando el ataque **califica** (pasa los gates). No son status effects, no stackean, no hacen daño por sí mismos y existen solo en combate.

---

## 1) Alcance / restricciones

- Battle-only: se descartan al terminar el combate.
- v1: solo enemigos pueden tener marks (la party no recibe marks).
- No persistencia fuera del combate.

---

## 2) Modelo (slot único)

- Cada enemigo tiene **un único slot** de mark.
- Slot ocupado → solo dos caminos: **Refresh** (mismo elemento) o **BlowUp/Detonate** (elemento distinto). Nunca conviven dos marks.
- Estado sugerido en `CombatantState`: `ActiveMark` (elemento/definición), `RemainingTurns`, `AppliedBy`.

---

## 3) Elementos (MarkElement)

- Los elementos se representan por un **ElementId opaco** (int/string) definido vía `ElementDefinition` (color, displayName, tags/flags). El core no se casa con un enum fijo.
- Iniciales (extensible): Solar, Moon, Psychic, Axis, Machine (pero pueden renombrarse/añadirse en data).
- Axis puede tener subtipos (pierce/blunt/etc.) que entran en la ReactionKey.
- Flag clave en `ElementDefinition`:
  - `canBeAppliedAsMark` (Axis = false → no aplica marks en slot vacío; sí puede detonar como elemento distinto).
  - `canDetonateMarks` (solo interactúa con marks si es true; `None` no detona).
- MVP: todos los elementos salvo `None` tienen `canDetonateMarks = true` (incluido Axis). `None` nunca detona/aplica. El flag se deja para future-proof si algún elemento deja de detonar.

---

## 4) Vocabulario

- **Apply**: colocar mark en slot vacío.
- **Refresh**: mismo elemento sobre slot ocupado → resetea duración + replay de FX/UI.
- **BlowUp/Detonate**: elemento distinto sobre slot ocupado → consume mark y dispara reacción.
- **Expire**: se borra por fin de duración.
- **Clear**: limpieza manual (muerte/fin combate/forzado).

---

## 5) Gates (cuándo un hit “califica”)

Si el hit **no califica**, Marks **no** se tocan (sin Apply/Refresh/BlowUp).

`cpSpent` = CP realmente consumido (post-commit/Judgment), no la intención de carga.

### 5.1 Single KS (timeline) y Single Magic
- Requiere **CP ≥ 1** (cpSpent == 0 descalifica).
- Requiere **timed hit** `Good` o `Perfect` (Fail/None descalifica).
- Requiere `incomingElement != None` (**Axis cuenta como elemento**) y `canDetonateMarks == true` (MVP: siempre true salvo None; flag se deja para future-proof). Si no hay elemento o no detona, no hay interacción de marks.

**Resumen Single:** califica si `cpSpent >= 1` AND `timedHit ∈ {Good, Perfect}` AND `incomingElement != None` AND `incomingElement.canDetonateMarks == true`.

### 5.2 All / AoE Magic
- Requiere **CP ≥ 1**.
- Requiere `incomingElement != None` y `incomingElement.canDetonateMarks == true` (MVP: siempre true salvo None; flag se deja para future-proof). Si no hay elemento o no detona, no hay interacción de marks.
- Sin skill check; usa **RNG por target** independiente:
  - `chance = clamp01(0.25 + cpSpent * perCpBonus)`
  - Si RNG pasa → ese target sí interactúa con marks (Apply/Refresh/BlowUp).
  - Si RNG falla → ese target se ignora (no interactúa en absoluto).
- No es “25% para todos”, es 25% por target.

---

## 6) Interacción por target (solo si califica)

- v1: solo enemigos pueden recibir marks. Si el `target` no es enemigo → no interactúa con marks.

### 6.1 Target SIN mark
- **Apply** del `incomingElement` si `canBeAppliedAsMark == true`. Si es `None` o `canBeAppliedAsMark == false` (ej. Axis), no aplica nada y termina.

### 6.2 Target CON mark
- Si `incomingElement == activeMarkElement` → **Refresh** (resetea duración, replay FX/UI).
- Si `incomingElement != activeMarkElement` → **BlowUp**:
  - Consume el mark (slot queda vacío).
  - Reacción con `ReactionKey = (activeMarkElement, incomingElement[, axisSubtype])`.
  - BlowUp **NO** aplica automáticamente el incoming mark después.

> Conclusión: slot ocupado → o refresca o truena; jamás deja otro mark al final del mismo hit.

---

## 7) Duración / expiración

- MVP: expira al **inicio del siguiente turno de `AppliedBy`** (owner-turn boundary). Con duración base 1, basta con invalidar en ese hook; si se sube a 2 turnos, el mismo hook puede decrementar `RemainingTurns`.
- Duración base: **1 turno** (ajustable a 2 si se decide).
- Refresh resetea duración a la base.
- Datos a guardar al Apply: `AppliedByCombatantId`, `AppliedAtTurnIndex/OwnerTurnCounter`, `RemainingTurns`.

---

## 8) Reacciones

- Definidas por `ReactionKey = (ActiveMarkElement, IncomingElement[, AxisSubtype])`; `axisSubtype` solo se incluye cuando el ataque es Axis (`incomingElement == Axis`) y proviene del AttackContext del golpe.
- El resultado concreto (daño/FX) lo resuelve un ReactionResolver; este spec solo fija la selección de reacción.

---

## 9) Eventos / idempotencia

- `MarkService.OnMarkChanged` debe disparar exactamente un evento por:
  - Applied
  - Refreshed
  - Detonated (BlowUp)
  - Expired
  - Cleared
- Si el hit no califica → no hay eventos.
- Opcional trazabilidad: adjuntar `ExecutionId` si se desea correlacionar con acciones.

---

## 10) Implementación recomendada (estructural)

1) **Funciones puras**:
   - `QualifiesForMarks_Single(ctx)`
   - `QualifiesForMarks_AoE_Target(ctx, target)`
   - `ResolveInteraction(hasMark, activeElement, incomingElement, canBeAppliedAsMark)` → {Apply, Refresh, BlowUp, None}

2) **Applier/Middleware único**:
   - si no califica → return
   - si califica → resolver interacción
   - llamar `MarkService` para Apply/Refresh/Detonate
   - si BlowUp → llamar `MarkReactionResolver` con ReactionKey

3) **Expiration hook**:
   - punto central (turn manager) para decrementar y expirar/limpiar.

---

## 11) RNG determinista (opcional)

Para debug reproducible:
- Sembrar RNG por target con `ExecutionId + targetId (+ spellId)`.
- Así, mismo execution + mismo target → misma decisión de calificación AoE.

Si no se requiere reproducibilidad, RNG normal está OK, pero los repros serán más difíciles.
