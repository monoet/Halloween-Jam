# Battle UI – Fase 3 (Magic/Item dinámicos) – To‑Do Log

## Objetivo
Poblar dinámicamente los menús de Magic e Item con listas (ScrollRect), estados deshabilitados (SP insuficiente / qty 0), tooltip de descripción y encabezado de coste SP. Sin cambios de escena obligatorios para compilar; el cableado de prefabs/ScrollRect/tooltip se hace en inspector después.

## Pendiente por implementar
- [x] Interfaces y modelos de filas
  - `IActionRowData` { Id, Name, Description, IsEnabled, DisabledReason }
  - `ISpellRowData : IActionRowData` { SpCost, ElementIcon }
  - `IItemRowData : IActionRowData` { Quantity }
  - Modelos inmutables: `SpellRowData`, `ItemRowData`.
- [x] TooltipUI
  - Componente simple Show(string)/Hide() con TMP_Text.
- [x] Row UI (prefabs)
  - `SpellRowUI`, `ItemRowUI` implementan ISelectHandler/ISubmitHandler (opcional pointer enter/click).
  - OnSelect (navegación gamepad/teclado) debe disparar onHover(data) para actualizar tooltip/SP header.
  - Soft-disabled (CanvasGroup o tint). Las filas deshabilitadas siguen siendo seleccionables para leer tooltip, pero bloquean OnSubmit (no usar Button.interactable=false).
- [x] ActionListPopulator
  - Instancia filas bajo ScrollRect content, clear/destroy, focus first enabled, callbacks onHover/onSubmit/onBlocked.
  - Recibe tipo/lista para elegir prefab (spellRowPrefab/itemRowPrefab) o usa un populator por menú; KISS, definir explícitamente el prefab.
  - Clear debe desuscribir callbacks de las filas antes de destruirlas (evita leaks si luego se usa pooling).
- [x] Fuentes de datos
  - Interfaces: `ISpellListSource.GetSpellsFor(actor)`, `IItemListSource.GetItemsFor(actor)`.
  - Adaptadores (MonoBehaviour o ScriptableObject, según wiring más simple): `CatalogSpellListSource` (usa ActionCatalog + SP actual, calcula IsEnabled y DisabledReason), `InventoryItemListSource` (usa inventario, incluye qty y DisabledReason si qty<=0).
  - Deben devolver las listas en orden estable/deseado (p. ej. orden de ActionCatalog o slots del actor) para evitar saltos al navegar.
- [x] Refactor paneles
  - `MagMenuPanel`: usar populator + fuente; hover → tooltip + header SP; submit solo si enabled → notificar spellId.
  - `ItemMenuPanel`: usar populator + fuente; hover → tooltip; submit solo si qty>0 → notificar itemId.
- [x] BattleUIRoot
  - `EnterMagic/EnterItem` deben llamar `ShowFor(actor)` de los paneles y propagar eventos explícitos (OnSpellChosen/OnItemChosen) hacia el driver/manager.
- [x] Datos Spell/Item
  - Añadir `[TextArea] string description` a SOs de spells/items; para spells, campo de ícono de elemento (Sprite) o resolver elemento→ícono.

## Aceptación (QA rápida)
- Magic muestra lista dinámica; primer ítem habilitado queda enfocado; header SP y tooltip actualizan al navegar.
- Confirm en spell deshabilitado no emite evento (opcional feedback bloqueado).
- Item lista con qty visible; confirm solo funciona cuando qty>0 (deshabilitado si qty==0).
- Si todas las filas están deshabilitadas, el foco cae en la primera fila (aunque esté disabled), tooltip funciona y confirm no hace nada.
- Sin errores de compilación; escena puede cablearse en inspector con los nuevos componentes.




===============================================================


implementacion en UNITY (NO CODIGO)

Perfecto. Con tus capturas ya se ve clarísimo qué tienes y qué falta. Esto es la **deuda de UI (MVP) en Unity** para que el código de población dinámica (Fase 3) tenga “dónde vivir” y se sienta coherente.

---

## 0) Qué ya tienes (y qué vamos a reutilizar)

* Ya tienes `Mag_Menu` e `Itm_menu`.
* Ya tienes un `ScrollContainer` dentro de cada uno.
* Ya tienes `SpellEntry` / `ItemEntry` como “fila” (aunque hoy son objetos en escena, no prefabs).
* Ya tienes un widget arriba tipo “SP” (en Magic). Bien: **el costo SP NO va en la fila**, va en ese header.

Lo que falta es convertir esto en:

* **ScrollRect real** (Viewport + Content)
* **Filas como Prefabs instanciables**
* **Tooltip box** (para description)
* **Estados disabled visibles** (sin romper navegación)

---

## 1) Jerarquía MVP recomendada (Magic)

Dentro de `Mag_Menu`:

```
Mag_Menu (Panel)
├─ Header_SP (RectTransform)
│  ├─ Label_SP (TMP_Text)            // “SP”
│  ├─ Value_SP (TMP_Text)            // número dinámico, ej: “12”
│  └─ (opcional) Decor_Dashes
├─ ScrollRect
│  ├─ Viewport (Image + Mask)
│  │  └─ Content (VerticalLayoutGroup + ContentSizeFitter)
└─ (Opcional) EmptyText (TMP_Text)   // “No spells”
```

### Componentes clave

**ScrollRect (objeto padre)**

* `ScrollRect` (Vertical ON, Horizontal OFF)
* `Viewport`: con `Image` + `Mask (Show Mask Graphic: OFF)`
* `Content`:

  * `VerticalLayoutGroup` (Child Control Height ON, Child Force Expand Height OFF)
  * `ContentSizeFitter` (Vertical: PreferredSize)

**Mag_Menu**

* fondo (Image) como ya lo tienes (tu panel translúcido)

---

## 2) Jerarquía MVP recomendada (Item)

Dentro de `Itm_menu`:

```
Itm_menu (Panel)
├─ Header (puede ser solo un título “Items”, o nada)
├─ ScrollRect
│  ├─ Viewport (Image + Mask)
│  │  └─ Content (VerticalLayoutGroup + ContentSizeFitter)
└─ (Opcional) EmptyText (TMP_Text)   // “No items”
```

Importante: en tu screenshot `Itm_menu` también dice “Sp Cost:” — eso en items **no aplica**.
MVP: bórralo o cámbialo por un título simple (“Items”).

---

## 3) Prefabs de fila (dos distintos, como pediste)

### A) `SpellRowPrefab` (para Magic)

Layout:
**SpellName >>> ElementIcon**

```
SpellRow (Prefab)
├─ Button (Unity UI Button)
│  └─ ContentRow (HorizontalLayoutGroup)
│     ├─ SpellNameText (TMP_Text)          // izquierda
│     └─ ElementIcon (Image)               // derecha
└─ DisabledOverlay (CanvasGroup o Image)   // opcional MVP
```

**Componentes mínimos**

* `Button` (o `Selectable` + Image) — recomendado Button porque tu InputDriver usa `submitHandler`.
* `LayoutElement` con altura fija (ej: 28–40px dependiendo tu UI).
* `HorizontalLayoutGroup` para alinear “nombre a la izquierda / icono a la derecha”.

**Disabled MVP (insuficiente SP)**

* No lo hagas “interactable=false” si quieres que aún se pueda seleccionar/leer tooltip.
* Mejor:

  * `CanvasGroup.alpha = 0.45` + opcional un iconito lock
  * Igual se selecciona, pero el submit se bloquea en código.

### B) `ItemRowPrefab` (para Items)

Layout:
**ItemName >>> Qty**

```
ItemRow (Prefab)
├─ Button
│  └─ ContentRow (HorizontalLayoutGroup)
│     ├─ ItemNameText (TMP_Text)
│     └─ ItemCountText (TMP_Text)          // derecha: “x3”
└─ DisabledOverlay (CanvasGroup o Image)
```

Disabled MVP (qty 0):

* misma lógica: se ve “apagado”, se puede seleccionar, pero no se confirma.

---

## 4) Tooltip box (compartido por ambos)

Esto es crucial porque ya estás extendiendo SO con `description`.

Ubicación sugerida (MVP): **una barra/box abajo** centrada o abajo-derecha, fuera del menú.

```
BattleUIRoot (o Canvas_MainUI)
└─ TooltipBox (Panel)
   └─ TooltipText (TMP_Text)
```

MVP:

* TooltipBox siempre existe
* Cuando cambias selección: actualiza texto
* Si no hay nada: lo pones vacío o lo escondes

---

## 5) “Plantillas” actuales (tu SpellEntry/ItemEntry)

Ahorita `SpellEntry`/`ItemEntry` están como objetos dentro del scroll en la escena.

Deuda MVP:

1. Toma `SpellEntry` → conviértelo a **Prefab Variant** `SpellRowPrefab`.
2. Toma `ItemEntry` → conviértelo a **Prefab Variant** `ItemRowPrefab`.
3. En la escena, deja el `Content` vacío (sin filas), porque ahora se instancian dinámicamente.

Y en Magic:

* Tu `SpellCost (SP)` dentro de la fila **deja de existir** o se esconde (porque el costo se muestra arriba).

---

## 6) Wiring visual que el código va a necesitar (sin meternos en lógica)

Para que el populador funcione, en inspector tendrás que poder asignar:

* En `ActionListPopulator`:

  * `contentRoot` → el `Content` del ScrollRect
  * `spellRowPrefab` → tu `SpellRowPrefab`
  * `itemRowPrefab` → tu `ItemRowPrefab`

* En `MagMenuPanel`:

  * `listPopulator`
  * `spCostValueText` (Value_SP)
  * `tooltipUI` (TooltipBox)

* En `ItemMenuPanel`:

  * `listPopulator`
  * `tooltipUI`

---

## 7) Estados disabled: deuda visual mínima (para que se sienta pro)

MVP visual que sí vende:

* Row enabled: normal
* Row disabled:

  * alpha ~0.4
  * opcional: un “—” o lock tiny a la derecha (no obligatorio)
* Tooltip cuando está disabled: muestra description + una línea arriba tipo:

  * “Not enough SP” o “Qty = 0”

Esto hace que el jugador entienda por qué no puede, sin frustración.

---

## 8) Mini checklist de “si esto está bien, ya jala”

* [ ] `ScrollRect` funciona: si hay 20 spells, se puede bajar.
* [ ] Al abrir Magic/Item, hay foco en la primera fila.
* [ ] Al moverte con pad/teclado, el tooltip cambia.
* [ ] Disabled se ve disabled, pero lo puedes seleccionar.
* [ ] Confirm en disabled no hace nada (pero UI no se rompe).
* [ ] Confirm en enabled dispara el flujo normal.

---

Si me pasas una captura más del `ScrollContainer` en el inspector (para ver si ya es `ScrollRect` o solo un GameObject vacío), te digo exactamente qué componentes te faltan poner y dónde (sin adivinar).
