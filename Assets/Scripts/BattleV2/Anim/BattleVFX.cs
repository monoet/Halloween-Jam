using UnityEngine;
using DG.Tweening;

namespace BattleV2.Anim
{
    /// <summary>
    /// Small collection of reusable battle VFX helpers (shakes, glows, flashes).
    /// </summary>
    public static class BattleVFX
    {
        public static Tween EnemyHitShake(Transform enemyModel, AttackAnimProfile profile)
        {
            if (enemyModel == null || profile == null)
            {
                return null;
            }

            return enemyModel.DOShakePosition(
                profile.enemyHitShakeTime,
                profile.enemyHitShakeStrength,
                vibrato: 20,
                randomness: 90f);
        }
    }
}
