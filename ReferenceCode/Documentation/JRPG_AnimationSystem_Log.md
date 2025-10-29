# JRPG Animation System - Development Log

> Mantener este registro sincronizado con los hitos del documento _LOCKED.

## 2025-10-31 — Checkpoint: Milestone 1 (Infraestructura Base)
- ✅ Definido `IAnimationOrchestrator` + `AnimationRequest` (contrato `PlayAsync` respetado).
- ✅ CombatClock monotónico (`CombatClock` + `ICombatClock`), configurable para pruebas.
- ✅ Event bus ligero (`AnimationEventBus`) con eventos Phase/Impact/Window/Lock.
- ✅ Adaptador temporal `LegacyAnimationOrchestratorAdapter` enlazando con el orquestador legacy.
- ✅ Guardrail de editor comprobando que `BattleManagerV2` siga invocando `PlayAsync`.

Notas:
- No se tocaron flujos existentes; solo nuevas dependencias.
- Próximo objetivo: Milestone 2 (datos autorables `ActionTimeline`, `ActionCatalog`, validator). Preparar ScriptableObjects y validaciones editor.

## 2025-10-29 — Milestone 2 - Datos autorables
- [x] Crear ActionTimeline ScriptableObject con tracks y fases.
- [x] Crear ActionCatalog indexando timelines.
- [x] Implementar AnimValidator (rangos y tags).
- [x] Cargar timelines demo (basic_attack, magic_bolt).
- [x] Validar catalogo sin errores.

Notas:
- `Assets/Animation/Timelines/` contiene los assets `basic_attack`, `magic_bolt` y `ActionTimelineCatalog`.
- `ActionTimelineCatalogValidatorEditor` agrega menú `Battle/Animation/Validate Action Timeline Catalog` para disparar validaciones usando `ActionTimelineValidator`.
- Los assets demo usan GUIDs fijos para integrarse con el nuevo contrato sin romper referencias.
- Actualización: renombrado el catálogo de animación a `ActionTimelineCatalog` y el menú de validación a `Validate Action Timeline Catalog` para evitar confusión con el `ActionCatalog` de gameplay.
