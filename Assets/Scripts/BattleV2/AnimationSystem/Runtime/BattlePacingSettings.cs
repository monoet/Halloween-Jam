using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    [CreateAssetMenu(menuName = "Battle/Animation/Battle Pacing Settings", fileName = "BattlePacingSettings")]
    public sealed class BattlePacingSettings : ScriptableObject
    {
        [Tooltip("Delay (seconds) applied after any actor finishes its turn.")]
        [Min(0f)] public float globalTurnGap = 0.25f;

        [Header("Player")]
        [Tooltip("Delay (seconds) before starting the player lifecycle (PRE/run_up).")]
        [Min(0f)] public float playerPreDelay = 0f;
        [Tooltip("Delay (seconds) after player lifecycle (POST/run_back).")]
        [Min(0f)] public float playerPostDelay = 0f;

        [Header("Enemy")]
        [Tooltip("Delay (seconds) after an enemy finishes its ACTION recipe.")]
        [Min(0f)] public float enemyTurnGap = 0.25f;

        [Tooltip("Optional label to identify this tuning profile in logs.")]
        public string debugLabel = "Default";
    }
}
