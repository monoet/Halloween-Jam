using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Setup
{
    [CreateAssetMenu(menuName = "Battle/Combat Events/Tween Cue Set", fileName = "TweenCueSet")]
    public sealed class TweenCueSet : ScriptableObject
    {
        [SerializeField] private List<Cue> cues = new List<Cue>();

        public IReadOnlyList<Cue> Cues => cues;

        public void PopulateLookup(Dictionary<string, TweenPreset> lookup)
        {
            if (lookup == null || cues == null)
            {
                return;
            }

            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                if (string.IsNullOrWhiteSpace(cue.triggerId) || cue.preset == null)
                {
                    continue;
                }

                lookup[cue.triggerId] = cue.preset;
            }
        }

        [System.Serializable]
        public struct Cue
        {
            public string triggerId;
            public TweenPreset preset;
        }
    }
}
