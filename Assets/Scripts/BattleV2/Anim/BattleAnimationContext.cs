using System;
using BattleV2.Orchestration;

namespace BattleV2.Anim
{
    /// <summary>
    /// Data bundle passed to animation strategies so they can interrogate or modify the battle flow safely.
    /// </summary>
    public readonly struct BattleAnimationContext
    {
        public BattleAnimationContext(
            BattleManagerV2 manager,
            BattleAnimationController playerController,
            BattleAnimationController enemyController)
        {
            Manager = manager;
            PlayerController = playerController;
            EnemyController = enemyController;
        }

        public BattleManagerV2 Manager { get; }
        public BattleAnimationController PlayerController { get; }
        public BattleAnimationController EnemyController { get; }

        public void WithControllers(Action<BattleAnimationController> callback)
        {
            if (callback == null)
            {
                return;
            }

            if (PlayerController != null)
            {
                callback(PlayerController);
            }

            if (EnemyController != null)
            {
                callback(EnemyController);
            }
        }
    }
}
