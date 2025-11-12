using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Setup
{
    [CreateAssetMenu(menuName = "Battle/Combat Events/Sound Cue Set", fileName = "SoundCueSet")]
    public sealed class SoundCueSet : ScriptableObject
    {
        [SerializeField] private List<Cue> cues = new List<Cue>();

        public IReadOnlyList<Cue> Cues => cues;

        public void PopulateLookup(Dictionary<string, SfxPreset> lookup)
        {
            if (lookup == null || cues == null)
            {
                return;
            }

            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                if (string.IsNullOrWhiteSpace(cue.key) || cue.preset == null)
                {
                    continue;
                }

                lookup[cue.key] = cue.preset;
            }
        }

        [System.Serializable]
        public struct Cue
        {
            public string key;
            public SfxPreset preset;
        }
    }
}
