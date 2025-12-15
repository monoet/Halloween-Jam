# MagMenu Troubleshooting Log (replicable en ItemMenu)

## Navegacion y focus
- ActionListPopulator limpia filas, reconstruye layout (Canvas.ForceUpdateCanvases + LayoutRebuilder) y arma navegacion explicita con wrap sin depender del inspector.
- No se filtra Selectable.IsInteractable(): las filas soft-disabled siguen navegables para tooltip, evitando que el cursor se atasque.
- Focus inicial: siempre la primera habilitada (o la primera si todas estan bloqueadas) tras rebuild de layout.
- Filas estaticas fuera del ScrollRect bloquean navegacion/feedback: todo debe instanciarse via populator en el Content.
- Scroll de navegacion: agrega `ScrollRectFollowSelection` al GO del ScrollRect (Mag/Item). Usa RowIndex real de cada fila (asignado en ActionListPopulator) y desplaza el contenido al salir de la ventana de `visibleSlots` (banda muerta simetrica: slot2 al subir, slot3 al bajar cuando visibleSlots=4). Ajusta `scrollSpeed`/`margin` segun anticipacion o suavizado deseado. Si hay scrollbar asignada en el ScrollRect, el handle se actualiza via SetValueWithoutNotify.

## Feedback visual
- UISelectionFeedback reescrito: baseScale fija, no acumula, guard isSelected, reset instant en OnDisable.
- SpellRowUI/ItemRowUI autoinyectan UISelectionFeedback si falta (buscan en el Selectable o lo agregan).
- ClearHandlers fuerza reset visual para evitar filas infladas al salir/cancelar.

## Submit/confirm
- Binding directo al Button del Selectable: el submit (Enter/confirm) llega a HandleSubmit, no se pierde en el boton hijo.

## Pitfalls encontrados
- Si el Selectable no esta asignado al boton real en el prefab, no hay feedback ni submit.
- Placeholders en la jerarquia (ej. "Magic Bolt" fijo) tapan el ScrollRect; eliminarlos.
- Si content del populator o spellRowPrefab estan en null, no se pobla la lista.
- ScrollMask: asegurar Viewport con Mask/RectMask2D y Content hijo; sin eso, las filas se dibujan fuera del area.
- Si el seleccionado no es hijo del Content (ej. otro panel) el seguimiento de scroll no corre.

## Guardrails del root a vigilar
- Suprimir SFX de navegacion en el primer focus (root lo hacia via flag en driver).
- Actualizar lastFocus con seleccion de usuario si se quiere "volver al ultimo" al reabrir.

## Practica recomendada para ItemMenu
- Reusar el mismo wiring: content del ScrollRect asignado, prefabs con Selectable correcto, feedback auto, submit bind, sin placeholders en jerarquia. Ajustar visibleSlots/scrollSpeed/margin segun viewport.***
