# MagMenu Troubleshooting Log (para replicar en ItemMenu)

## Navegación y focus
- ActionListPopulator limpia filas, reconstruye layout (Canvas.ForceUpdateCanvases + LayoutRebuilder) y arma navegación explícita con wrap; no depende del inspector.
- No se filtra Selectable.IsInteractable(): las filas soft-disabled siguen navegables para tooltip, evitando que el cursor se atasque.
- Focus inicial: siempre la primera habilitada (o la primera si todas están bloqueadas) tras rebuild de layout.
- Filas estáticas fuera del ScrollRect bloqueaban navegación/feedback. Todo debe instanciarse vía populator en el Content para que el focus pase por ellas.
- **Scroll de navegación:** agrega `ScrollRectFollowSelection` al GO del ScrollRect (Mag/Item). Usa el RowIndex real de cada fila (asignado en ActionListPopulator) y desplaza el contenido cuando el cursor sale de la ventana de `visibleSlots` (default 4). Ajusta `scrollSpeed`/`margin` según anticipación o suavizado deseado.

## Feedback visual
- UISelectionFeedback reescrito: usa baseScale fija, no acumula, tiene guard isSelected y resetea instant en OnDisable.
- SpellRowUI/ItemRowUI autoinyectan UISelectionFeedback si falta (buscan en el Selectable o lo agregan).
- ClearHandlers fuerza reset visual para evitar filas infladas al salir/cancelar.

## Submit/confirm
- Binding directo al Button del Selectable: el submit (Enter/confirm) llega a HandleSubmit, no se pierde en el botón hijo.

## Pitfalls encontrados
- Si el Selectable no está asignado al botón real en el prefab, no hay feedback ni submit.
- Placeholders en la jerarquía (ej. “Magic Bolt” fijo) tapan el ScrollRect; eliminarlos.
- Si content del populator o spellRowPrefab están en null, no se pobla la lista.
- ScrollMask: el enmascarado depende del prefab/escena. Asegurar Viewport con Mask/RectMask2D y Content hijo; sin eso, las filas se dibujan fuera del área.

## Guardrails del root a vigilar
- Suprimir SFX de navegación en el primer focus (root lo hacía vía flag en driver).
- Actualizar lastFocus con selección de usuario si se quiere “volver al último” al reabrir.

## Práctica recomendada para ItemMenu
- Reusar el mismo wiring: content del ScrollRect asignado, prefabs con Selectable correcto, feedback auto, submit bind, sin placeholders en jerarquía.
