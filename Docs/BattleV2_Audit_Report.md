# BattleV2 Audit — Weak Points & ROI Guide

> Ordenado por mayor retorno (fácil / impacto alto) → medio → bajo. Cada ítem incluye evidencia y por qué importa.

---

## Alto ROI (fácil de abordar, alto impacto)

- **`ActionJudgment` coherente post-cobro**
  - Evidencia: `Assets/Scripts/BattleV2/Orchestration/Services/PlayerActionExecutor.cs` construye `judgment` antes de cobrar y solo luego lo actualiza. Cualquier futura refactor que olvide `WithPostCost` reintroduce estados pre-cost. Asegurar que `judgment` se derive siempre después de `SpendCP/SpendSP` evita desalinear logs/marks.
  - Impacto: decisiones/marks basadas en `ResourcesPostCost` no se corrompen; diagnósticos de costo son confiables.

- **Thread drift por `ConfigureAwait(false)` en runtime de animación**
  - Evidencia: `Assets/Scripts/BattleV2/AnimationSystem/Execution/Runtime/Executors/WaitSecondsExecutor.cs` y `TimedHitStepExecutor.cs` usan `ConfigureAwait(false)`; `CombatEventDispatcher` también. Cualquier await que retorne al threadpool y luego llame Unity/CombatantState dispara `UnityThread.AssertMainThread`.
  - Acción: quitar `ConfigureAwait(false)` o forzar `UnityThread.SwitchToMainThread()` antes de tocar Unity/state. Muy bajo esfuerzo, alto valor (crashes intermitentes desaparecen).

- **Refund seguro en excepciones de pipeline**
  - Evidencia: `OrchestrationActionPipeline.RunLegacyPipelineAsync` ya atrapa y retorna `ActionResult.Failure(effectsApplied)`, pero cualquier middleware que aplique side-effects sin marcar `EffectsApplied` vuelve a abrir la puerta al “beneficio sin costo”.
  - Acción: auditar middlewares y marcar `ActionContext.MarkEffectsApplied()` justo antes de cualquier mutación (daño, marks, triggered effects). Fácil de añadir, evita regresiones de refund.

---

## ROI Medio (esfuerzo/moderado, impacto moderado)

- **Sincronización de main thread en Editor sin domain reload**
  - Evidencia: `Assets/Scripts/BattleV2/Core/UnityMainThreadGuard.cs` captura `MainThreadId` en `RuntimeInitializeOnLoadMethod`. Con “Enter Play Mode Options” (sin domain reload), el `SynchronizationContext` puede reciclarse y dejar `MainThreadId` obsoleto → falsos positivos/negativos en `IsMainThread()`.
  - Acción: re-capturar contexto/ID al primer `SwitchToMainThread()` en play, o en hook `EditorApplication.playModeStateChanged`. Evita diagnósticos ruidosos.

- **Drift en `EnsureTargetContext` con context vivo**
  - Evidencia: `Assets/Scripts/BattleV2/Orchestration/Services/ActionPipeline.cs` usa `EnsureTargetContext` para “ajustar” `CombatContext` al `PrimaryTarget`. Si el contexto compartido muta (roster refresh), el pipeline puede ejecutarse con enemigo distinto al snapshot.
  - Acción: congelar `CombatContext` (o clonar) al momento de commit, no recalcular durante ejecución. Impacto: elimina golpes al target equivocado en combates con adds/muertes.

- **Eventos/efectos sin idempotencia explícita**
  - Evidencia: `PlayerActionExecutor` publica `ActionCompletedEvent` sin un identificador de ejecución único; si un fallback reintenta o un doble publish ocurre, listeners sin guardas pueden aplicar efectos dos veces.
  - Acción: agregar execution-id (selectionId) a eventos y hacer listeners idempotentes. Reduce duplicados sutiles (VFX/marks).

---

## ROI Bajo (más difícil o menor impacto)

- **Triple `SwitchToMainThread` en `RunLegacyPipelineAsync`**
  - Evidencia: el método salta al main thread en Enter/BeforeExecute/Exit; si `SwitchToMainThread` usa `Post`, puede añadir latencia de frames.
  - Acción: consolidar a un solo hop + assert. Ganancia pequeña (micro stutter), riesgo bajo.

- **Session lifecycle de targeting**
  - Evidencia: `Assets/Scripts/BattleV2/UI/BattleUITargetInteractor.cs` maneja `pendingTcs`/`sessionId`, pero depende del caller para limpiar estados al fallback. No es crítico tras el debounce, pero puede dejar logs de “NoPending”.
  - Acción: centralizar cancel/cleanup en un solo lugar. Esfuerzo mayor para beneficio menor (mayormente ruido de logs).

---

## Repro/Checklist sugerido

- Ejecutar acción con costo base+charge y forzar excepción dentro del pipeline: validar que no haya refund si `EffectsApplied=true`.
- Simular domain reload desactivado en Editor y verificar `UnityMainThreadGuard.IsMainThread()` frente a `SwitchToMainThread`.
- Correr animaciones largas (TimedHit, WaitSeconds) y confirmar que ningún guard de hilo se dispara (quitar/ajustar `ConfigureAwait(false)`).

---

## Notas adicionales

- Mantener logging normalizado `[Thread.debug00]` para todo salto de hilo y para cualquier punto que marque `EffectsApplied`.
- Si se introduce rollback real, definir un “commit boundary” explícito: cobrar recursos → aplicar efectos → publicar eventos. Cada fase con su propio guard. 
