# Combat System Specification — Frecuencia Cósmica

Este sistema de combate es **por turnos, estilo JRPG tipo Octopath Traveler**.
Cada personaje actúa individualmente, con énfasis en **Combo Points (CP)**, **Keepsakes**, **resonancias elementales**, y **acciones sincronizadas**.

## Objetivos
- Sistema modular y escalable.
- Compatible con `CharacterRuntime` y `PartyManager`.
- Separar **lógica del combate** de **UI**.
- Usar eventos (`UnityEvent`) para comunicar turnos y resultados.
- Diseñado para integración posterior con cutscenes, bosses y multi-phase fights.

## Flujo General
1. **BattleManager** inicializa el encuentro:
   - Crea instancias de personajes (`PartyManager.ActiveParty`).
   - Spawnea enemigos definidos por `EncounterData`.
2. **TurnController** determina el orden de acción.
3. **BattleUIManager** despliega el menú (Attack / Skills / Items / Flee).
4. **ActionResolver** ejecuta habilidades, calcula daño, aplica CP, buffs, debuffs.
5. **BattleEndManager** evalúa victoria, derrota o condiciones especiales.

## Campos Clave
- `CharacterRuntime` → Stats base, CP, SP, HP, atributos elementales.
- `EnemyRuntime` → Hereda `CharacterRuntime` con IA básica.
- `KeepsakeAction` → ScriptableObject que define ataques especiales.
- `BattleManager` → Punto central para pausar, reanudar, limpiar combate.

## Elementos del UI
- **Command Panel**: Attack, Skills, Items, Flee.
- **Keepsake Buttons**: `KS1`, `KS2`, `KS3` (timed hits / CP spenders / AOE).
- **Target Selector**: UI dinámica que muestra objetivos válidos.
- **Turn Timeline**: orden visual de turnos.
- **Combat Log Panel**: debug visual o narrativo de acciones.

## Tipos de Daño y Afinidades
- Lunar 🌙 — magia pura (stock mágico).
- Solar ☀️ — fuego o energía vital.
- Electric ⚡ — Nova.
- Gravity 🌀 — Ciro.
- Chaos 🔥 — Fausto.
- Nature 🍃 — Nika.

## Combo Points (CP)
- Generados por habilidades con tag `GenerateCP`.
- Gastados por `Keepsake Actions` o `Chain Attacks`.
- Límite estándar: 5 CP (7 en modo especial).

## Sincronizaciones
Ciertas combinaciones elementales (ej. Solar + Lunar) disparan **Chain Skills**:
- “Eclipse”: Lilia + Jay (All-Target beam)
- “Twilight”: Lilia + Jay (Single powerful strike)

## Estado reciente (2025-10-24)
- BattleManagerV2 delega el turno enemigo a EnemyTurnCoordinator y el fallback a FallbackActionResolver; el manager queda como orquestador (~630 lineas).
- TriggeredEffectsService.Clear() cancela la cola en fin de batalla; se invoca desde BattleManagerV2 (OnDisable/OnDestroy/ResetBattle/HandleBattleEnded).
- Suite de Edit Mode inicial (Assets/Tests/EditMode):
  - CombatantActionValidatorTests cubre happy path e insuficiencia de recursos.
  - TargetingCoordinatorTests valida fallback cuando no hay resolvers.
- Nuevo asmdef BattleV2.EditModeTests.asmdef (Editor only). Ejecutar desde Test Runner -> Edit Mode.
- Pendiente: smoke PlayMode (player -> enemy -> trigger -> fin) para verificar OnTurnReady, timings y cleanup.

