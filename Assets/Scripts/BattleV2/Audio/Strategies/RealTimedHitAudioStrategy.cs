using BattleV2.Charge;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Audio.Strategies
{
    /// <summary>
    /// Estrategia de producción: no hace nada; el audio se resuelve vía TimedHitResultEvent Scope=Final y TimedHitAudioBridge.
    /// </summary>
    public sealed class RealTimedHitAudioStrategy : ITimedHitAudioStrategy
    {
        public void Emit(TimedHitResult result, CombatantState attacker, CombatantState target)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[AudioStrategy] Real mode → relying on EventBus final event.", attacker);
#endif
        }
    }
}
