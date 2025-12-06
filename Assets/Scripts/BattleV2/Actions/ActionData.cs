using UnityEngine;
using BattleV2.Targeting;
using BattleV2.Marks;
using BattleV2.Execution.TimedHits;
using BattleV2.Charge;
using System.Collections.Generic;

namespace BattleV2.Actions
{
    [System.Serializable]
    public class BattleActionData
    {
        public string id;
        public string displayName;
        public int costSP;
        public int costCP;
        public ScriptableObject actionImpl;

        [Header("UI")]
        [TextArea]
        public string description;
        public Sprite elementIcon;

        [Header("Combat Event Options")]
        [Tooltip("Delay (seconds) between impact raises when the action hits multiple targets. 0 disables staggering.")]
        public float combatEventStaggerStep = 0f;

        [Header("Targeting")]
        public bool requiresTarget = true;
        public TargetAudience targetAudience = TargetAudience.Enemies;
        public TargetShape targetShape = TargetShape.Single;

        [Header("Timed Hit (optional)")]
        [Tooltip("If assigned, this action will request a timed-hit window using this profile.")]
        public Ks1TimedHitProfile timedHitProfile;
        [Tooltip("Optional basic timed-hit profile for simple windows.")]
        public BasicTimedHitProfile basicTimedHitProfile;
        [Tooltip("Runner selection for timed-hit; defaults to the engine's standard runner.")]
        public TimedHitRunnerKind runnerKind = TimedHitRunnerKind.Default;

        [Header("Marks")]
        public List<MarkRule> markRules = new List<MarkRule>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (requiresTarget && targetAudience == TargetAudience.Self)
            {
                targetAudience = TargetAudience.Enemies;
            }
        }
#endif
    }
}
