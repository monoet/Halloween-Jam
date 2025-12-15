# Combat System — Future TODOs & Roadmap (BattleV2)

> Documento de pendientes futuros para terminar de cerrar el combate del JRPG.  
> Enfocado en: **balance real**, **UX**, **robustez técnica**, y **extensibilidad** (sin sobre-diseño prematuro).

---

## 0) Pre-MVP “Delicados / Urgentes”

### 0.0 CP: coherencia de modificadores de daño (Player vs Enemy)

**Riesgo:** el sistema de daño podría estar aplicando modificadores distintos dependiendo de si el atacante es Player o Enemy (o si el pipeline usa rutas diferentes), lo que rompe balance y percepción de “reglas justas”.

**TODO**

- Auditar el cálculo final de daño por atacante (Player/Enemy):
  - ¿mismo path de cálculo?
  - ¿mismos multiplicadores (elemento, weapon family, buffs, defense, crit, etc.)?
  - ¿CP influye en el mismo lugar del pipeline?
- Agregar trazas comparables por hit:
  - `ExecutionId`, actorId/side, actionId, cpSpent, baseDamage, multipliers, finalDamage.
- Crear un “golden test encounter” manual:
  - 1 player vs 1 dummy enemy con stats fijos para comparar.

**Salida esperada**

- Un log/tabla de breakdown de daño por hit **idéntico en estructura** para player/enemy.

---

### 0.1 CP: retorno de daño negativo con CP ≥ 2 (muy delicado)

**Riesgo:** si CP≥2 hace menos daño que CP0, el sistema se siente roto y el jugador deja de invertir CP (mata el loop).

**TODO**

- Confirmar con pruebas controladas:
  - misma acción, mismo target, mismos stats, sin RNG.
  - cpSpent = 0/1/2/3 (mínimo) con breakdown completo.
- Localizar la causa típica (sospechosos comunes):
  - CP aplicando un multiplicador mal (ej: *divide* en lugar de *multiply*).
  - CP afectando la “base” pero luego otra capa lo re-escala hacia abajo.
  - CP gastado pero el `ActionJudgment`/context usa un valor distinto (intención vs gasto real).
- Definir regla simple de balance:
  - `damage(cp=2) >= damage(cp=1) >= damage(cp=0)` (monotonicidad mínima).
  - Si no quieres linealidad: al menos “no castiga”.

**Salida esperada**

- Fix + prueba manual reproducible que pruebe monotonicidad en 3 acciones distintas.

---

### 0.2 Terminar UI MVP (flujo jugable completo)

**Objetivo:** que un playtest no se muera por fricción de UI.

**TODO**

- Confirmación/Back consistente (menú → targeting → confirm → ejecución).
- Señales claras de:
  - CP invertido actual
  - timed hit result
  - si una acción califica para Marks (más adelante)
- Estados UI robustos:
  - input edge/debounce estable
  - no reentrancy
  - no sesiones “zombies”

**Salida esperada**

- “UX flow pass” completo: inicio turno → acción → target → ejecutar → feedback → siguiente.

---

### 0.3 Diseño de acciones del demo (KS, magia, ítems)

**Objetivo:** que el demo tenga un set coherente de decisiones, no un inventario de habilidades sueltas.

**TODO**

- Definir por personaje:
  - 2–3 KS actions “core”
  - 1–2 skills de soporte/utility
  - 1 magia single, 1 magia AoE (si aplica)
  - 1–2 ítems relevantes
- Cada habilidad debe existir por una razón:
  - “abre marks”, “detona marks”, “safe CP”, “all-in CP”, “defensa”, “tempo”
- Matriz rápida:
  - (skill) x (rol) x (costo) x (ventana CP) x (timed hit)

**Salida esperada**

- Documento de kit por personaje (listo para implementarse en ActionData/Scriptables).

---

### 0.5 Step Scheduler + Animations: pulido serio

**Objetivo:** que la ejecución sea sólida, predecible y fácil de debuggear.

**TODO**

- Reducir “hops” y puntos ambiguos (main thread, waits, callbacks).
- Establecer un contrato claro:
  - steps producen eventos / timeline markers
  - lógica consume esos eventos
- Fortalecer “targets per step” + snapshots congelados.
- Instrumentación:
  - logs con `ExecutionId`, stepId, actorId, targetId (por step).

**Salida esperada**

- Scheduler “confianza”: sin race, sin drift, sin duplicados, sin glitches de orden.

---

## 1) Dynamic Party System (per encounter + swap in-combat + transforms/fusions)

> Meta: que el battlefield soporte “rehidratar” un combatant en otro body/prefab sin perder identidad.

### 1.0 Dynamic instantiation / encounter composition

**TODO**

- Spawner idempotente:
  - spawn/despawn combatants en runtime sin romper referencias
- Layout recalculable:
  - re-seat / re-slot de posiciones si cambia party size
- Identidad estable:
  - `CombatantId` persiste aunque cambie el prefab visual

### 1.1 Party swap durante combate

**TODO**

- Sistema de “bench/active”
- Reglas de entrada/salida:
  - ¿cambia turno? ¿consume acción? ¿consume CP?
- UI soporta roster variable

### 1.2 Transformaciones/Fusiones como runtime swap (concepto clave)

**Regla pro para no morir:**  
Transformación ≠ personaje nuevo. Es **swap de body/runtime** con identidad preservada.

**TODO**

- `CombatantState` mantiene:
  - stats runtime, hp/sp/cp, statuses, marks, etc.
  - `CurrentFormId`
  - referencia al body actual (instancia del prefab)
- `TransformController`:
  - unload old body
  - load new body prefab
  - re-bind anim driver + action sets + presenters
  - disparar evento `OnFormChanged`

**Salida esperada**

- Transform “swap” estable con animaciones cabronas **sin duplicar lógica**.

---

## 2) Diseño de characters transformados (fuera de código)

> Objetivo: que las formas sean legibles y balanceables sin reescribir sistemas.

### 2.0 Definir “FormDefinitions”

**TODO**

- Cada forma define:
  - visual rig/prefab
  - action set aditivo (skills extra / skills bloqueadas)
  - overrides temporales de stats (preferible)
  - políticas (opcional) para CP/Marks si algún día se necesita

### 2.1 Stats: override vs scaling/class

Tu intuición es buena: **override temporal** suele ser más viable que mover scalings.

**TODO**

- Definir estándar:
  - Forma aplica `StatModifiers` temporales (multiplicadores/aditivos)
  - Al terminar, revertir limpio
- Si “Class” ya modifica scalings:
  - dejar class intacta
  - forma añade un “layer” superior temporal

**Salida esperada**

- Formas balanceables en data sin tocar el core.

---

## 3) Post-MVP / Overdesign controlado (solo si el loop lo pide)

### 3.0 Marks “extensiones” (si el MVP lo justifica)

**Ejemplos (no comprometidos)**

- same-element hook (dormant) → overload / micro-stack / status pequeño
- stacking temporal por 1 turno (solo en ultimate)
- reacciones que alteran tempo (no solo daño)

**Regla:** no se implementa nada aquí hasta que el playtest demuestre:

- decisión interesante + tensión + consecuencias no lineales.

---

## Obviedades que conviene agregar (porque si no, duele después)

### A) “Golden Encounter” de regresión

Un encounter fijo para probar:

- CP 0/1/2/3 monotónico
- timed hit gates
- AoE RNG por target
- scheduler ordering

### B) Telemetría mínima (sin tooling pesado)

Un modo debug que imprime:

- ExecutionId
- Damage breakdown
- Mark qualification & interaction
- Step order

Esto acelera 10x tu iteración.
