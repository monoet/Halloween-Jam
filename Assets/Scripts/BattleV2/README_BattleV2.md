# BattleV2 Architecture Snapshot

This folder contains the new modular battle core.

## Components

- `Core/`
  - `BattleLogger` - centralised logging via `[Battle:<tag>]` (`OnLogged` event).
  - `BattleStateController` - explicit state machine (`Idle`, `AwaitingAction`, `Resolving`, `Victory`, `Defeat`).
  - `CombatContext` - shared references (player, enemy, services, catalog).
  - `BattleServices` - RNG + helpers (`GetAnimatorFor`).
  - `BattleConfig` - ScriptableObject that bundles catalog, input provider and shared services.
- `Actions/`
  - `IAction` / `IActionProvider` - strategy contract for actions.
  - `BattleActionData` - serialisable data used by catalogs/providers.
  - `ActionCatalog` - builds available lists, resolves `IAction`, provides fallback.
  - `SimpleAttackAction` - example implementation (damage + CP).
- `Providers/`
  - `IBattleInputProvider` - contrato para player/IA/auto input.
  - `AutoBattleInputProvider` - elige la primera accion disponible (tests).
  - `ManualBattleInputProvider` - placeholder que por ahora degrada a auto hasta que el UI V2 este listo.
  - `ManualBattleInputProviderV2` - componente para debug que espera input de teclado (1-9 / Escape).
  - `ScriptedBattleInputProvider` - ScriptableObject que reproduce una lista fija de acciones para smoketests.
- `Charge/`
  - `ChargeProfile` - parametros de carga/notches (tiempo vs CP).
  - `ComboPointScalingProfile` - curva editable que define el multiplicador final segun los CP gastados (con soporte para soft caps).
- `Orchestration/`
  - `BattleManagerV2` - orquesta el loop, usa providers + catalog y maneja fallback/resolucion de enemigo.
  - `BattleBootstrapper` - helper para auto-start en escenas de prueba.
- `UI/`
  - `BattleDebugPanel` - overlay opcional con estado, ultima accion, CP/SP y logs.

## Wiring paso a paso (Unity Editor)

1. **Preparar assets**
   - Crea un ScriptableObject `BattleConfig` (menu: `Create/Battle/Battle Config`).
   - Asigna un `ActionCatalog` al `BattleConfig` (si no existe, crea uno y anade al menos una accion, por ejemplo `SimpleAttackAction`).
   - Para usar el auto, crea un asset via `Create/Battle/Input Provider/Auto` y arrastralo al campo `inputProvider` del config; funciona tanto en modo asset como si dejas el script como `ScriptableObject` suelto en test scenes.
   - Si queres input manual, agrega `ManualBattleInputProviderV2` a un GameObject en escena y referencia ese componente en `BattleConfig`. Para secuencias deterministas usa el asset `ScriptedBattleInputProvider`.
   - Opcional: ajusta `BattleServices` dentro del config (por defecto crea uno nuevo).
   - Opcional: crea un asset `ComboPointScalingProfile` (menu: `Create/Battle/Charge/Combo Point Profile`) y arrastralo al campo `comboPointScaling` del `BattleConfig` para editar la curva de CP (multiplicadores, caps, extrapolacion).

2. **Scene setup**
   - Crea un GameObject vacio llamado `BattleManagerRoot` (GameObject > Create Empty) y dejalo en la raiz de la escena.
   - Con `BattleManagerRoot` seleccionado, usa `Add Component` para sumar `BattleStateController` y `BattleManagerV2`. El orden no importa siempre que ambos scripts vivan en el mismo objeto.
   - Asegurate de que el objeto del jugador y el del enemigo en la escena tengan `CombatantState`; si no, agregalo ahora (el estado runtime se reusar).
   - En el inspector de `BattleManagerV2` rellena los campos:
     - `State`: si ya existe un `BattleStateController` en el root se autocompleta; si no, arrastra manualmente la referencia del componente.
     - `Player`: arrastra el GameObject del jugador que tiene `CombatantState`.
     - `Enemy`: arrastra el GameObject del enemigo con `CombatantState`.
     - `Config`: arrastra el `BattleConfig` que creaste; desde ahi el manager toma `ActionCatalog`, `inputProvider` y `services`.
   - Verifica que `BattleConfig` tenga un catalogo y provider asignados (inspector del asset); esto evita nulls al arrancar.
   - (Opcional) Agrega `BattleBootstrapper` al mismo `BattleManagerRoot`; marca `autoStart` si queres que al entrar en Play corra `ResetBattle()` y `StartBattle()` automaticamente.

3. **HUD opcional**
   - Coloca `BattleDebugPanel` en canvas y conecta `stateController`, `battleManager`, `player` y los `TMP_Text` necesarios. El bloque **UI Elements** del inspector espera (en orden) los textos de estado (`stateText`), ultima accion (`actionText`), CP (`cpText`), SP (`spText`) y log (`logText`).

## Warnings / Troubleshooting

- **Sin acciones disponibles**: asegurate de que `ActionCatalog` tenga al menos una accion y que `SimpleAttackAction` (u otras) esten configuradas en las listas (basic/magic/items).
- **Referencias nulas**: si `BattleManagerV2` no encuentra `player`/`enemy` o el `inputProvider`, revisa que el `BattleConfig` este asignado y que los campos no queden vacios. El manager registra logs `[Battle:BattleManager]` avisando del problema.
- **Provider manual**: el `ManualBattleInputProvider` actual degrada a auto y solo sirve como placeholder. Cuando exista el UI final, implementara los paneles reales (menu + selector).
- **Sin logs**: asegurate de tener `BattleDebugPanel` o revisar la consola; todos los eventos importantes se muestran con prefijo `[Battle:<tag>]`.
- **Restart de combate**: `ResetBattle()` resetea estado/contexto antes de `StartBattle()`.

## Proximos pasos

- Implementar el `ManualBattleInputProvider` real que conecte `BattleActionMenu` + `ActionSelectionUI` usando la nueva API.
- Agregar mas acciones (`Magic`, `Items`, `Defend`, `Flee`) con sus propias estrategias `IAction`.
- Reemplazar la logica automatica del enemigo con un provider/IA dedicado.

## Runtime Integration Checklist

Antes de volver a invertir en UI conviene asegurar que el runtime alimente correctamente al sistema V2:

1. **CombatantState ← CharacterRuntime**
   - Confirma que `CombatantState.InitializeFrom` se ejecuta para cada combatiente cuando cargás la escena o al iniciar batalla.
   - Verifica que nombre, HP, SP y CP reflejen los valores guardados en el runtime (agrega logs temporales o un test simple).
   - Si manejás múltiples personajes, documentá dónde se resuelve cada `CharacterRuntime` y en qué momento se asigna al `CombatantState`.

2. **Catálogo + loadouts**
   - Define qué catálogo usa cada combatiente (uno global o por personaje). Si empleás loadouts, filtra las acciones antes de llamar a `Present`.
   - Asegurá que `ActionCatalog.BuildAvailableFor` respete los desbloqueos y que `CanExecute`/`Resolve` devuelvan implementaciones válidas.

3. **Charge & CP**
   - Usa el harness para mandar `BattleSelection` con `cpCharge` > 0 y confirmar que `ChargeProfile` y `NotchedChargeStrategy` producen los multiplicadores esperados.
   - Ajusta el overlay para permitir fijar manualmente el charge o invocar la estrategia real; así validás tus perfiles sin UI final.

4. **Telemetría**
   - Añade logs o asserts alrededor de `OnPlayerActionSelected/Resolved` para capturar daño final, gasto de CP/SP y estado del turno. Facilita detectar discrepancias cuando conectes la UI definitiva.

Completar estos pasos deja el backend sólido y reduce sorpresas cuando regreses a la capa visual.

## Battle Debug Harness Overlay

Para depurar sin depender de la UI de producción, hay un harness en `Debug/BattleDebugHarness.cs`:

- Añade el componente a un GameObject vacío en escena.
- Al iniciar Play, detecta `BattleManagerV2`, se registra como `IBattleInputProvider` y abre una ventana OnGUI flotante (toggle con `~`).
- Muestra HP/SP/CP de player/enemy, lista acciones (Attack/Magic/Item), permite lanzar acciones con un clic, cancelar turnos, y muestra un log.
- Sirve para validar el flujo completo (acciones, daño, CP) sin wiring de menús. Puedes extenderlo con controles de charge, perfiles, etc.
- No requiere Canvas ni prefabs; es un overlay puramente en código. Cuando la UI final esté lista, puedes desactivarlo o dejarlo como herramienta de QA.

Esto deberia cubrir el primer wiring en Unity sin tropiezos.
