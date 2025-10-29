using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution
{
    /// <summary>
    /// MonoBehaviour utility that ticks registered <see cref="ActionSequencer"/> instances
    /// once per frame. Intended for prototypes and logging milestones.
    /// </summary>
    public sealed class ActionSequencerDriver : MonoBehaviour
    {
        [SerializeField] private CombatClock clock;

        private readonly List<ActionSequencer> activeSequencers = new();
        private readonly List<ActionSequencer> buffer = new();

        private void Awake()
        {
            clock ??= new CombatClock();
        }

        private void Update()
        {
            if (activeSequencers.Count == 0)
            {
                return;
            }

            clock.Sample();

            buffer.Clear();
            for (int i = 0; i < activeSequencers.Count; i++)
            {
                var seq = activeSequencers[i];
                seq.Tick();
                if (!seq.IsCompleted && !seq.IsCancelled)
                {
                    buffer.Add(seq);
                }
                else
                {
                    seq.Dispose();
                }
            }

            activeSequencers.Clear();
            activeSequencers.AddRange(buffer);
        }

        public void Register(ActionSequencer sequencer)
        {
            if (sequencer == null)
            {
                return;
            }

            sequencer.Start();
            activeSequencers.Add(sequencer);
        }
    }
}
