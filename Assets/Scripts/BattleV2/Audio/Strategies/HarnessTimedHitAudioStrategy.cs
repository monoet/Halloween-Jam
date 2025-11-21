using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Audio;
using BattleV2.Charge;
using BattleV2.Core;

namespace BattleV2.Audio.Strategies
{
    /// <summary>
    /// Harness-only strategy: emite los flags de timed-hit directamente desde el resultado final (sin depender del runner/eventos).
    /// </summary>
    public sealed class HarnessTimedHitAudioStrategy : ITimedHitAudioStrategy
    {
        public void Emit(TimedHitResult result, CombatantState attacker, CombatantState target)
        {
            var installer = AnimationSystemInstaller.Current;
            var dispatcher = installer != null ? installer.CombatEvents : null;
            if (dispatcher == null || attacker == null)
            {
                return;
            }

            string flag = result.Judgment switch
            {
                TimedHitJudgment.Perfect => BattleAudioFlags.AttackTimedPerfect,
                TimedHitJudgment.Good => BattleAudioFlags.AttackTimedImpact,
                _ => BattleAudioFlags.AttackTimedMiss
            };

            dispatcher.EmitExternalFlag(
                flag,
                attacker,
                target,
                weaponKind: "none",
                element: "neutral",
                isCritical: false,
                targetCount: target != null ? 1 : 1);
        }
    }
}
