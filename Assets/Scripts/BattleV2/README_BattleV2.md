# BattleV2 Architecture Snapshot

This folder contains the new modular battle core.

## Components

- `Core/`
  - `BattleLogger` – centralised logging via `[Battle:<tag>]` (`OnLogged` event).
  - `BattleStateController` – explicit state machine (`Idle`, `AwaitingAction`, `Resolving`, `Victory`, `Defeat`).
  - `CombatContext` – shared references (player, enemy, services, catalog).
  - `BattleServices` – RNG + helpers (`GetAnimatorFor`).
  - `BattleConfig` – ScriptableObject that bundles catalog, input provider and shared services.
- `Actions/`
  - `IAction` / `IActionProvider` – strategy contract for actions.
  - `ActionData` – serialisable data used by catalogs/providers.
  - `ActionCatalog` – builds available lists, resolves `IAction`, provides fallback.
  - `SimpleAttackAction` – example implementation (daño + CP).
- `Providers/`
  - `IBattleInputProvider` – contrato para player/IA/auto input.
  - `AutoBattleInputProvider` – elige la primera acción disponible (tests).
  - `ManualBattleInputProvider` – placeholder que por ahora degrada a auto hasta que el UI V2 esté listo.
- `Orchestration/`
  - `BattleManagerV2` – orquesta el loop, usa providers + catalog y maneja fallback/resolución de enemigo.
  - `BattleBootstrapper` – helper para auto-start en escenas de prueba.
- `UI/`
  - `BattleDebugPanel` – overlay opcional con estado, última acción, CP/SP y logs.

## Wiring paso a paso (Unity Editor)

1. **Preparar assets**
   - Crea un ScriptableObject `BattleConfig` (menu: `Create/Battle/Battle Config`).
   - Asigna un `ActionCatalog` al `BattleConfig` (si no existe, crea uno y añade al menos una acción, por ejemplo `SimpleAttackAction`).
   - Asigna el `ScriptableObject` del input provider (por ahora `AutoBattleInputProvider` o `ManualBattleInputProvider` placeholder).
   - Opcional: ajusta `BattleServices` dentro del config (por defecto crea uno nuevo).

2. **Scene setup**
   - Crea un GameObject (ej. `BattleManagerRoot`).
   - Añade `BattleStateController` y `BattleManagerV2` al mismo objeto.
   - En el inspector de `BattleManagerV2`:
     - Asigna las referencias a `player` y `enemy` (`CombatantState` existentes).
     - Arrastra el `BattleConfig` al campo correspondiente. El manager usará su catálogo/proveedor.
   - (Opcional) Añade `BattleBootstrapper` al root y activa `autoStart` para que `StartBattle()` se ejecute al entrar en Play.

3. **HUD opcional**
   - Coloca `BattleDebugPanel` en canvas y conecta `stateController`, `battleManager`, `player` y los `TMP_Text` necesarios. Esto mostrará estado y logs en runtime.

## Warnings / Troubleshooting

- **Sin acciones disponibles**: asegurarse de que `ActionCatalog` tenga al menos una acción y que `SimpleAttackAction` (u otras) estén configuradas en las listas (basic/magic/items).
- **Referencias nulas**: si `BattleManagerV2` no encuentra `player`/`enemy` o el `inputProvider`, revisa que el `BattleConfig` esté asignado y que los campos no queden vacíos. El manager registra logs `[Battle:BattleManager]` avisando del problema.
- **Provider manual**: el `ManualBattleInputProvider` actual degrada a auto y sólo sirve como placeholder. Cuando exista el UI final, implementará los paneles reales (menu + selector).
- **Sin logs**: asegúrate de tener `BattleDebugPanel` o revisar la consola; todos los eventos importantes se muestran con prefijo `[Battle:<tag>]`.
- **Restart de combate**: `ResetBattle()` resetea estado/contexto antes de `StartBattle()`.

## Próximos pasos

- Implementar el `ManualBattleInputProvider` real que conecte `BattleActionMenu` + `ActionSelectionUI` usando la nueva API.
- Agregar más acciones (`Magic`, `Items`, `Defend`, `Flee`) con sus propias estrategias `IAction`.
- Reemplazar la lógica automática del enemigo con un provider/IA dedicado.

Esto debería cubrir el primer wiring en Unity sin tropiezos.
