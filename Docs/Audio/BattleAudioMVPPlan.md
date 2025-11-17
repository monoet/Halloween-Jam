# Battle Audio MVP Plan (0.1.0)  
Track the implementation of the combat audio MVP and the groundwork for future expansion.

## Scope & Principles
- MVP first, avoid over-engineering; lay hooks for reactions & richer music later.
- Single source of truth for flags and audio assets; no magic strings.
- Audio only reacts to events; no gameplay logic inside audio.

## Deliverables Checklist
- [x] Constants: `BattleAudioFlags` (all current flags), `WeaponFamily`, `ElementId`.
- [x] Context: `CombatEventContext` (+ optional `MarkDetonationPayload`), 2D-only MVP for now.
- [x] Data: `BattleAudioDatabase` (SO) with SfxEntry & MusicConfig; dictionary built in `OnEnable`.
- [x] Controller: `BattleAudioController` (`ICombatEventListener`, `ITurnPhaseListener`) with cooldowns, 2D fallback, FMOD params, music snapshots.
- [ ] Router wiring: CombatEventRouter -> BattleAudioController; TurnPhase -> music.
- [ ] Validation: editor warnings for missing SfxEntry per flag (once) and missing ActorMotionAnchor.
- [ ] Asset: create `BattleAudioDatabase.asset` with MVP entries: windup, impact, runback, mark/apply, mark/detonate, ui/turn_change, music snapshots.

## Mapping (documented once)
- Weapon param: 0=None, 1=Sword, 2=HeavySword, 3=Dagger, 4=Staff, 5=Mace, 6=Fist, 7=Bow, 8=Gun, 9=Thrown.
- Element param: 0=None, 1=Moon, 2=Sun, 3=Mind, 4=Form, 5=Chaos, 6=Forge, 7=Axis.
- Crit: 0/1; Targets: 1=single, 2+=multi.
- Rules: attacks/marks 3D; UI/music 2D; if anchor missing for 3D, degrade to 2D and warn (editor).

## Phased Work Plan
1) Foundations
   - Add `BattleAudioFlags`, enums, `CombatEventContext` (with `MarkPayload` optional).
   - Add `BattleAudioDatabase` types + dictionary build; create `BattleAudioDatabase.asset`.
2) Runtime Hooks
   - Implement `BattleAudioController` (FMOD + fallback logs), param mapping, cooldowns, 2D fallback.
   - Wire `CombatEventRouter` to controller; register for turn phase changes for music.
3) Validation & Editor QoL
   - Warning once per missing SfxEntry; assert/warn for missing `ActorMotionAnchor`.
   - Comment in controller: audio-only responsibilities.
4) Smoke Tests
   - Basic attack: windup → impact → runback with positional SFX.
   - Mark apply/detonate events fire SFX.
   - Enter/exit combat switches snapshots/stinger.
   - Params vary with weapon/element/crit/targets changes.

## Future-Proof Notes (do not build now)
- Mark reactions: `MarkDetonationPayload` already in context; later routing can key off it.
- Cooldowns: replace string key with struct/ValueTuple if GC becomes noisy.
- Music growth: extract dedicated IMusicController if more than 2–3 states appear.
- Per-skill routing: add new flags/constants rather than inline strings.
