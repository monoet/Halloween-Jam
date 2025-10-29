# Timed Hit Pipeline – Contratos Hard, Validaciones y Directrices (v1.0)

**Propósito**: Definir los contratos inquebrantables, validaciones de datos, política de elección de runner, pruebas de paridad y guías de QA/implementación para el ecosistema de Timed Hits.

---

## 1. Contrato de Datos

### 1.1 `TimedHitResult`
```json
{
  "type": "object",
  "required": ["totalHits", "hitsSucceeded", "cpRefund", "damageMultiplier"],
  "properties": {
    "totalHits": { "type": "integer", "minimum": 1 },
    "hitsSucceeded": { "type": "integer", "minimum": 0 },
    "cpRefund": { "type": "integer", "minimum": 0 },
    "damageMultiplier": { "type": "number", "minimum": 0 },
    "cancelled": { "type": "boolean" },
    "successStreak": { "type": "integer", "minimum": 0 }
  }
}
```

### 1.2 `TimedHitPhaseEvent`
```json
{
  "type": "object",
  "required": ["phaseIndex", "totalPhases", "outcome"],
  "properties": {
    "phaseIndex": { "type": "integer", "minimum": 1 },
    "totalPhases": { "type": "integer", "minimum": 1 },
    "normalizedTime": { "type": "number", "minimum": 0, "maximum": 1 },
    "perfectWindowStart": { "type": "number", "minimum": 0, "maximum": 1 },
    "perfectWindowEnd": { "type": "number", "minimum": 0, "maximum": 1 },
    "outcome": { "type": "string", "enum": ["perfect", "good", "miss"] },
    "timestamp": { "type": "string", "format": "date-time" }
  }
}
```

### 1.3 Invariantes obligatorios
- `selection.CpCharge >= action.costCP`.
- `selection.CpCharge <= actor.CurrentCP` en el momento de ejecutar.
- `0 <= PerfectWindowCenter <= 1`.
- `PerfectWindowRadius >= 0`.
- `SuccessWindowRadius >= 0`.

---

## 2. Matriz de Cancelacion

| Causa                | Momento               | Evento emitido         | Resultado        | Mensaje HUD              | Emisor         |
|----------------------|-----------------------|------------------------|------------------|--------------------------|----------------|
| Cambio de escena     | Antes o durante fase  | OnSequenceCompleted    | Cancelled = true | Accion cancelada (escena) | BattleManager  |
| Muerte del actor     | Cualquier fase        | OnSequenceCompleted    | Cancelled = true | Actor KO                 | Middleware     |
| Token cancelado      | Durante fase u hold   | OnSequenceCompleted    | Cancelled = true | Timing interrumpido       | Runner         |
| Hot-swap runner      | Durante ejecucion     | OnSequenceCompleted    | Cancelled = true | Runner reemplazado        | BattleManager  |
| Cutscene / pausa     | Durante hold          | OnPhaseStarted (al reanudar) | Sin cambio   | Pausa temporal           | Runner         |

Semantica universal: nunca se generan misses artificiales tras la cancelacion; el resultado final mantiene `Cancelled=true`.

---

## 3. Paridad Instant vs KS1

### 3.1 Golden profiles de referencia
| Nombre | Hits | Duracion (s) | Hold (s) |
|--------|------|--------------|----------|
| Slow   | 3    | 2.5          | 0.3      |
| Medium | 10   | 1.8          | 0.2      |
| Fast   | 30   | 0.8          | 0.1      |

Estos perfiles viven en `ReferenceCode/Profiles/TimedHitProfile_{Slow,Medium,Fast}.asset`.

### 3.2 Test de snapshot
- **Descripcion**: Comparar `cpRefund` y `damageMultiplier` entre `Ks1TimedHitRunner` y `InstantTimedHitRunner` usando el mismo perfil/tier/CP.
- **Criterio de aceptacion**: Diferencia maxima ±1% en `damageMultiplier` y valores identicos en `cpRefund`.
- **Pseudo test**:
```csharp
var r1 = await ks1.RunAsync(request);
var r2 = await instant.RunAsync(request);
Assert.AreEqual(r1.CpRefund, r2.CpRefund);
Assert.AreApproximatelyEqual(r1.DamageMultiplier, r2.DamageMultiplier, 0.01f);
```

---

## 4. Politica de Eleccion de Runner

```
if selection.Mode == Auto:
    return InstantRunner

if actor.IsPlayer:
    if manager.TimedHitRunner != null AND manager.TimedHitRunner.enabled:
        return manager.TimedHitRunner
    else:
        log warning
        return InstantRunner

return InstantRunner
```

- Si el runner cambia durante ejecucion se cancela la secuencia actual (`Cancelled=true`) y se emite warning.
- El fallback nunca debe bloquear el turno ni colgar el pipeline.

---

## 5. Checklist de PR
1. Perfiles validados (`TimelineDuration > 0`, `Hits >= 1`, ventanas en `[0..1]`).
2. Eventos `OnSequenceStarted` y `OnSequenceCompleted` presentes en todos los runners.
3. `ActionContext.TimedResult` nunca null cuando la accion implementa `ITimedHitAction`.
4. Cancelaciones publican log amigable y mensaje HUD.
5. Friendly logger habilitado en builds QA.
6. `BattleDebugHarnessV2` limpia suscripciones en `OnDestroy`/`OnDisable`.
7. Instant y KS1 devuelven refunds equivalentes (tolerancia ±1%).
8. CI valida perfiles y ejecuta tests de snapshot.
9. HUD soporta fase sintetica del modo instantaneo.
10. Documentacion de escena ↔ runner actualizada (README de combate).

---

## 6. Contrato HUD Overlay
- `enum TimedHitUIState { Ready, Window, Result, Cancelled, Completed }`
- `struct TimedHitUIEvent`
  - `state : TimedHitUIState`
  - `normalizedTime : float (0..1)`
  - `phaseIndex : int`
  - `totalPhases : int`
  - `outcome : string ("perfect","good","miss")`

**Reglas**:
1. Modo instantaneo emite una fase sintetica (1/1).
2. HUD debe manejar todos los estados listados.
3. Overlay y HUD consumen los mismos eventos; el harness solo escucha cuando `hijackTimedHitRunner == false`.

---

## 7. Extensiones de habilidades y CP

### 7.1 Stacks, cargas adicionales y nuevas habilidades
- Toda habilidad que consuma CP adicionales debe seguir la formula oficial (`costCP + CpCharge`). Si introduce stacks o efectos recurrentes, registrar explícitamente qué parte consume CP y qué parte genera refund.
- Nuevas habilidades con timed hits (ej. futuras KS2/KS3, magia, ítems) deben:
  1. Reusar el pipeline actual (`TimedHitMiddleware`) y proveer un `Ks1TimedHitProfile` propio o derivado de los golden profiles.
  2. Documentar en el PR qué `Tier` usan y cómo escalan con CP.
  3. Incluir pruebas con los perfiles de referencia (Slow/Medium/Fast) o agregar un nuevo perfil acompañado de validación.
- Si la habilidad requiere pasos adicionales (p.ej. mini-juego diferente), se propone nuevo middleware/pipeline antes de tocar `BattleManagerV2`.

### 7.2 Guardrails de CP
- Cualquier cambio en la semántica de CP (stacking, reservas, refunds diferidos) debe:
  1. Mantener invariantes de la sección 1.
  2. Ajustar las pruebas de snapshot para asegurar equivalencia instant/KS1.
  3. Notificar explícitamente al lead para confirmar que no rompe estrategias actuales de pacing.

### 7.3 Reporte de extensiones
- Si se necesita ampliar el pipeline (nuevo `IActionMiddleware`, nueva `strategy` para resultados, etc.), se abre propuesta antes de PR describiendo:
  - Motivo (nueva habilidad/estado).
  - Impacto en contratos existentes.
  - Pruebas necesarias (unit/playmode/CI).

---

## 8. Guardrails de orquestación (BattleManagerV2)
- **Norma principal**: `BattleManagerV2` permanece como orquestador. No se permiten nuevas responsabilidades de lógica de combate dentro del manager.
- **Acciones prohibidas dentro del manager**:
  - Calcular daños, refunds o validar estado de recursos.
  - Controlar animaciones o reproducir VFX directamente.
  - Procesar timed hits o manejar estado interno de CP.
- **Uso de Strategies/Services**:
  - Requerimientos de comportamiento se modelan como `Service` o `Strategy` dedicada (ej. `ChargeStrategy`, `DamageStrategy`).
  - Si no existe una estrategia adecuada, se reporta al lead para evaluar nueva interface (ver sección 9).
- **Reporte inmediato**:
  - Cualquier PR que necesite modificar `BattleManagerV2` fuera de orquestación debe notificarse por adelantado al lead. La revisión puede rechazar el cambio si introduce bloat.

---

## 9. Mapa de pipelines y strategies existentes

| Componente                         | Rol                                    | Notas |
|-----------------------------------|-----------------------------------------|-------|
| `ActionPipeline` (`OrchestrationActionPipeline`) | Entry point único para ejecutar acciones. | Usa pipeline legacy mientras migramos. |
| `TimedHitMiddleware`              | Resuelve timed hits y obtiene `TimedHitResult`. | Obligatorio para `ITimedHitAction`. |
| `Damage middleware` (legacy)      | Aplica daño, estados y efectos.         | Mantener aislado del manager. |
| `TriggeredEffectsService`         | Maneja sub-acciones, cadenas, AOE.      | Evitar duplicar lógica de turnos. |
| `BattleTurnService` + `TurnController` | Avance de turnos.                      | Ignoran acciones marcadas como `IsTriggered`. |
| `BattleAnimOrchestrator`          | Control de timelines y delays.          | No se toca desde el manager. |
| `CombatantActionValidator`        | Validaciones previas de recursos/estados. | CP y estados deben checarse aquí. |

**Reglas para nuevas estrategias/pipelines**:
1. Revisar tabla anterior antes de crear una nueva pieza.
2. Si se requiere un pipeline paralelo (p.ej. mini-juego totalmente distinto), proponer arquitectura al lead y documentar en este archivo en la misma tabla.
3. Toda refactorización que introduzca pipeline nuevo debe:
   - Explicar cómo se integra con los existentes.
   - Incluir scripts de validación (tests) y actualizar el checklist de PR.

---

## 10. Glosario
- **PerfectWindowRadius**: rango exacto donde el golpe es perfecto.
- **SuccessWindowRadius**: rango adicional donde el golpe sigue siendo exitoso.
- **PerfectWindowCenter**: momento central (0..1) de la ventana de exito.
- **AutoMissGrace**: margen añadido antes de marcar miss automatico.
- **Tier**: subperfil de `Ks1TimedHitProfile` elegido segun CP cargado.
- **CpCharge**: CP adicional que invierte el jugador.
- **costCP**: CP minimo requerido por la accion.
- **Refund**: CP devuelto segun resultado de timing.
- **DisplayName / Id**: nombres en PascalCase consistentes entre assets.

---

## 11. Notas finales
- Todas las reglas definidas son normativas; cualquier excepcion debe justificarse en el PR correspondiente.
- CI ejecuta validaciones de perfiles y tests de snapshot en cada build nocturna.
- Registrar en este documento cualquier cambio al algoritmo de runners, contratos o semantica de cancelaciones.
- Las decisiones de crear nuevos pipelines, estrategias o refactors mayores deben comunicarse al lead antes de abrir PR. Esta es la ruta oficial de escalación.

---

## Anexo A – Contexto y flujo (referencia)

La siguiente seccion resume el flujo original y las observaciones previas documentadas durante la refactorizacion:
1. `BattleManagerV2` arma la `BattleSelection`, levanta el `ActionPipeline` y mantiene el runner activo.
2. `ActionPipeline` crea `ActionContext` y ejecuta midlewares (incluyendo `TimedHitMiddleware`).
3. `TimedHitMiddleware` selecciona runner (KS1/instant/harness), dispara `RunAsync` y persiste `TimedHitResult`.
4. `BattleManagerV2` publica eventos (`ActionCompletedEvent`, `OnPlayerActionResolved`) tras obtener el resultado.
5. Los principales puntos dolorosos detectados:
   - Falta de helpers (`CancelLiveSequence`, `StartLivePhase`, etc.) en el harness -> compilacion rota.
   - Perfiles con `TimelineDuration` ≈ 0 -> UI en “modo turbo”.
   - Eventos ausentes en runner instantaneo -> listeners sin feedback.
   - Cancelaciones silenciosas -> QA sin contexto.
   - Divergencia entre CP seleccionado y aplicado -> necesidad de `ResetLiveState`.

Este anexo debe revisarse cuando se ajusten contratos o se introduzcan nuevos midlewares.
