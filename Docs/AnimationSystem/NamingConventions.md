# Animation System Naming Conventions

Esta guía resume los nombres a respetar para que StepScheduler resuelva bindings, recetas y eventos sin sorpresas.

## Character Animation Sets (Animator)
- **Formato**: Personaje/Acción o Personaje/Estado
- **Ejemplos actuales**:
  - Ciro/IdleCombat
  - Ciro/LeftRun
  - Ciro/Cast
  - Ciro/SlashAttack
  - Ciro/DamageTaken
- **Recomendado**: Prefijo con el personaje (Ciro/, Estela/) seguido de la acción en PascalCase.

## Tween Bindings
- **Formato**: Verbo + Objetivo (StepForward, RecoilBack, LungeForward).
- Mantener un set compartido si todos usan la misma trasformación; si un personaje necesita tweens únicos, anteponer el nombre (CiroStepForward).

## SFX / VFX Bindings
- **SFX**: sfx_{descripcion} en snake_case (sfx_attack_crit, sfx_item_throw).
- **VFX**: fx_{descripcion} (fx_attack_slash, fx_item_trail).

## System Steps y Gate IDs
- **Ventanas**: ks-light-window (id) con 	ag legible (KS_Light).
- **Fallback / Timeline**: si apuntan a otro timeline, usar ids coherentes (BasicAttack_Recover).

## Recetas (ActionRecipe IDs)
- CamelCase/PascalCase con sufijo descriptivo:
  - BasicAttack_KS_Light
  - BasicAttack_KS_Success
  - UseItem

## Timelines (payloads)
- Siempre escribir clip, 	ween, sfx, etc. con el ID exacto definido en sets/tweens/sfx.
- Ejemplo: clip=KS_Light_Windup;loop=false (si el set tiene KS_Light_Windup).

## Preferencias generales
- No usar espacios; preferir _ o / según corresponda.
- Mantener KS_ como prefijo para ventanas/recetas relacionadas con timed-hit.
- Documentar cualquier excepción en este Markdown.
