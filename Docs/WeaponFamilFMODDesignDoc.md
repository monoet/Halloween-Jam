RC

Te lo replanteo con **FMOD params** al centro y **MVP KISS de verdad**: solo lo mínimo para que Ciro y los enemigos suenen diferente según arma, sin meter slots ni tablas raras todavía.

---

# Design Document — `WeaponFamily` + FMOD (MVP KISS)

---

## 0. Scope

Versión mínima funcional para:

* Hacer que **el mismo `basic_attack`** suene distinto según el arma del combatiente.
* Sin sistemas de equipo todavía.
* Sin bases de datos complejas.
* Sin tocar StepScheduler/Recipes.

---

## 1. Objetivo del MVP

Introducir un único campo:

```csharp
public WeaponFamily WeaponFamily; // en CombatantState
```

y usarlo para:

* Enviar un **parámetro FMOD `WeaponFamily`** a los eventos de ataque (`windup`, `impact`, `recover`).
* Permitir que FMOD seleccione internamente la variación correcta (greatsword, beast, genérico).

Nada más.

---

## 2. Principio General

Flujo conceptual mínimo:

```text
Archetype.defaultWeaponFamily
           ↓
    CombatantState.WeaponFamily   ← única verdad runtime
           ↓
    BattleAudioController → FMOD (event + param WeaponFamily)
```

* **Para MVP no hay WeaponSlot**.
* Manual override se permite vía código si hace falta (boss, debug).

---

## 3. Data Model (C#)

### 3.1 Enum `WeaponFamily` (MVP)

```csharp
public enum WeaponFamily
{
    Generic = 0,   // fallback default
    Greatsword = 1,
    Beast = 2
    // Añadir más SOLO cuando haya assets reales.
}
```

### 3.2 Archetype

```csharp
[Serializable]
public sealed class CombatantArchetype
{
    // ...
    public WeaponFamily defaultWeaponFamily = WeaponFamily.Generic;
}
```

* Ciro: `defaultWeaponFamily = Greatsword`.
* Slime: `defaultWeaponFamily = Beast`.
* Enemigo genérico: `Generic`.

### 3.3 CombatantState

```csharp
public sealed class CombatantState
{
    // ...
    public WeaponFamily WeaponFamily { get; set; } = WeaponFamily.Generic;
}
```

### 3.4 Inicialización en `CombatantRosterService`

```csharp
private CombatantState SpawnCombatant(CombatantArchetype archetype /* ... */)
{
    var state = new CombatantState();
    
    state.WeaponFamily = archetype != null 
        ? archetype.defaultWeaponFamily 
        : WeaponFamily.Generic;

    // resto de wiring...

    return state;
}
```

> MVP: no hay WeaponSlot todavía.
> Si algún día se agrega, se mete en este bloque, pero **no ahora**.

---

## 4. Integración con FMOD (MVP)

### 4.1 Diseño en FMOD Studio

Para cada tipo de acción de combate principal:

* `event:/SFX/combat/attack/windup`
* `event:/SFX/combat/attack/impact`
* `event:/SFX/combat/attack/recover`

Crear en cada uno:

1. **Parameter Labeled `WeaponFamily`**

   * Labels:

     * `Generic` (0)
     * `Greatsword` (1)
     * `Beast` (2)

2. Un **multi-instrument** o pistas con conditions:

   Ejemplo en `attack/impact`:

   * Condición `WeaponFamily == Greatsword` → sample pesado, metálico.
   * Condición `WeaponFamily == Beast` → sample viscoso, orgánico.
   * Condición `WeaponFamily == Generic` → sample neutro estándar.

FMOD se encarga de seleccionar el sample.

### 4.2 Lado C# — BattleAudioController

**Suposición**: ya resuelves el evento de FMOD a partir de un flag de audio (`attack/windup`, etc.).

MVP: **no necesitas un resolver de WeaponFamily a path**, solo un parámetro.

```csharp
private const string WeaponFamilyParamName = "WeaponFamily";

private void PlayAttackSfx(string flag, CombatEventContext ctx)
{
    var state = ctx.AttackerState;
    var weaponFamily = (int)(state?.WeaponFamily ?? WeaponFamily.Generic);

    var eventPath = ResolveEventPath(flag); 
    // ej: "event:/SFX/combat/attack/impact"

    var instance = RuntimeManager.CreateInstance(eventPath);

    instance.setParameterByName(WeaponFamilyParamName, weaponFamily);

    // Posicionar en anchor si aplica
    // RuntimeManager.AttachInstanceToGameObject(instance, anchorTransform, rigidbody);

    instance.start();
    instance.release();
}
```

> MVP: `ResolveEventPath(flag)` puede ser un simple switch/diccionario chico.
> No hace falta un `WeaponSfxDatabase` todavía.

---

## 5. Uso con Recipes / StepScheduler

**Nada cambia** en Recipes:

* Siguen disparando flags genéricos:

  * `attack/windup`
  * `attack/impact`
  * `attack/recover`

* `CombatEventRouter` emite eventos con:

  * `flag = "attack/impact"`
  * `ctx.AttackerState` con `WeaponFamily` ya seteado.

* `BattleAudioController`:

  * Resuelve el eventPath.
  * Setea `WeaponFamily` como parámetro FMOD.

**Resultado:**
`basic_attack` es el mismo recipe para Ciro y el Slime,
pero suena totalmente diferente.

---

## 6. Ejemplo de Flujo Completo (MVP)

### 6.1 Setup

* Ciro Archetype:

  * `defaultWeaponFamily = Greatsword`
* Slime Archetype:

  * `defaultWeaponFamily = Beast`

### 6.2 Spawn

* RosterService crea `CombatantState`:

  * Ciro → `WeaponFamily = Greatsword`
  * Slime → `WeaponFamily = Beast`

### 6.3 Ciro usa `basic_attack`

1. Recipe:

   * `attack/run_up`
   * `attack/windup`
   * `attack/impact`

2. CombatEventRouter lanza `attack/impact` con Ciro como attacker.

3. BattleAudioController:

   * `flag = "attack/impact"`
   * eventPath → `"event:/SFX/combat/attack/impact"`
   * `WeaponFamily = Greatsword → (int)1`
   * `instance.setParameterByName("WeaponFamily", 1)`

4. FMOD:

   * Evalúa conditions: `WeaponFamily == Greatsword`
   * Dispara sample de impacto pesado.

### 6.4 Slime usa el mismo `basic_attack`

Todo igual, excepto:

* `WeaponFamily = Beast → (int)2`

FMOD dispara sample viscoso orgánico.
Misma recipe, resultado sonoro distinto.

---

## 7. Futuras Extensiones (NO incluidas en MVP)

Solo registradas, pero **no se implementan todavía**:

1. **WeaponSlot equipado**

   * Si algún día tienes sistema de equipo, podrás:

     * leer arma del slot,
     * mapear a `WeaponFamily`,
     * asignar a `CombatantState` en el spawn / cambio de arma.
   * No es necesario para que Ciro + Slime suenen distinto.

2. **Más `WeaponFamily`**

   * `Sword`, `Staff`, `Claws`, `Ethereal`, etc.
   * Solo añadir cuando tengas:

     * personaje real,
     * SFX reales en FMOD.

3. **Animación/VFX especializados por `WeaponFamily`**

   * Reutilizar el mismo flag en resolvers de anim y VFX.
   * Por ahora no es obligatorio para que funcione el audio.

4. **Resolver de datos (ScriptableObject)**

   * Para proyectos grandes, mover:

     * flag → eventPath
   * a un asset de configuración.
   * Hoy basta con un switch/dictionary.

---

## 8. Semblanza y crítica (KISS / SOLID / YAGNI)

### Lo que está fuerte

* **KISS real**:

  * Un enum chico.
  * Un campo en `CombatantState`.
  * Un parámetro FMOD.
  * Un switch/diccionario pequeño para eventPath.

* **SOLID**:

  * `CombatantState` solo guarda datos.
  * `BattleAudioController` solo:

    * mapea flag → evento,
    * pasa `WeaponFamily` a FMOD.
  * FMOD decide qué sample tocar.

* **YAGNI**:

  * Nada de WeaponSlot en código todavía.
  * Nada de bases de datos de audio complejas.
  * Nada de combinaciones WeaponFamily × ElementId.

### Riesgos principales

1. **Olvidar setear `defaultWeaponFamily` en algún Archetype**

   * Mitigar con:

     * default `Generic`,
     * tal vez un warning de editor si es `Generic` en players “importantes”.

2. **Inflar el enum antes de tiempo**

   * Disciplina: sólo se agrega una nueva familia cuando haces:

     * personaje real,
     * banco/samples reales en FMOD.

3. **Usar lógica complicada para eventPath**

   * Mantenerlo en:

     * `switch (flag)` → 3–5 casos máximo (windup/impact/recover).
   * Si crece, recien ahí pensar en SO.

---

Si quieres, el siguiente paso es que te deje listo:

* `WeaponFamily.cs` (enum MVP),
* campos nuevos en `CombatantArchetype` y `CombatantState`,
* un snippet real de `BattleAudioController` con el envío del parámetro a FMOD.


RC — Cosas que NO cubre pero sí debería dejar “groundwork” mínimo

Esta es la lista pequeña de cosas que todavía no se implementan,
pero sí vale la pena preparar la estructura para evitar retrabajo:

El campo WeaponFamily debe existir ya en CombatantState
Aunque solo se use para audio al inicio, servirá luego para anim/VFX.

El enum debe estar preparado para expandirse
No enorme, pero con una estructura clara y consistente en nombres.

BattleAudioController debe recibir WeaponFamily siempre
Para cuando FMOD crezca con más layers o condiciones.

El pipeline Archetype → State debe estar bien definido
Así cuando metas WeaponSlot, solo agregas al spawn, no cambias sistemas.

FMOD debe tener el parámetro WeaponFamily bien etiquetado
Para permitir mayor granularidad en un futuro sin recrear eventos.

El resolver de audio debe ser una función separada (aunque simple)
Para no amarrarte a un switch gigante cuando tengas 6–10 familias.

Si todo eso está sentado, puedes escalar animación, VFX y equipo sin rehacer nada.