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
