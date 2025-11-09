using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    /// <summary>
    /// QA harness that replays a scripted sequence of combat flags.
    /// </summary>
    public sealed class EventReplayRunner : MonoBehaviour
    {
        [SerializeField] private CombatEventRouter router;
        [SerializeField] private bool autoPlayOnStart = true;
        [SerializeField] private bool loop;
        [SerializeField] private List<ReplayEvent> sequence = new List<ReplayEvent>
        {
            new ReplayEvent { flagId = CombatEventFlags.Windup, delaySeconds = 0f },
            new ReplayEvent { flagId = CombatEventFlags.Runup, delaySeconds = 0.18f },
            new ReplayEvent { flagId = CombatEventFlags.Impact, delaySeconds = 0.16f, perTarget = true, targetCount = 2 },
            new ReplayEvent { flagId = CombatEventFlags.Runback, delaySeconds = 0.22f }
        };

        private Coroutine playbackRoutine;
        private readonly List<CombatEventContext.CombatantRef> targetScratch = new List<CombatEventContext.CombatantRef>(4);

        private void Start()
        {
            if (autoPlayOnStart)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }
        }

        [ContextMenu("Play Replay")]
        public void Play()
        {
            router ??= FindObjectOfType<CombatEventRouter>();
            if (router == null)
            {
                Debug.LogWarning("[EventReplayRunner] CombatEventRouter not found.", this);
                return;
            }

            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
            }

            playbackRoutine = StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            do
            {
                var baseContext = new ScopedContext();
                try
                {
                    for (int i = 0; i < sequence.Count; i++)
                    {
                        var entry = sequence[i];
                        if (entry.delaySeconds > 0f)
                        {
                            yield return new WaitForSeconds(entry.delaySeconds);
                        }

                        DispatchEntry(entry, baseContext.Context);
                    }
                }
                finally
                {
                    baseContext.Dispose();
                }

                if (!loop)
                {
                    break;
                }

                yield return null;
            } while (loop);

            playbackRoutine = null;
        }

        private void DispatchEntry(ReplayEvent entry, CombatEventContext baseContext)
        {
            if (string.IsNullOrWhiteSpace(entry.flagId))
            {
                return;
            }

            targetScratch.Clear();
            if (entry.perTarget && entry.targetCount > 0)
            {
                for (int i = 0; i < entry.targetCount; i++)
                {
                    targetScratch.Add(new CombatEventContext.CombatantRef(
                        1000 + i,
                        CombatantAlignment.Enemy,
                        null,
                        null,
                        null));
                }
            }

            var context = CombatEventContext.Acquire();
            context.Populate(
                baseContext.Actor,
                baseContext.Action,
                targetScratch,
                entry.perTarget && targetScratch.Count > 0,
                baseContext.Tags);

            router.DispatchTestEvent(entry.flagId, context);
            context.Release();
        }

        [System.Serializable]
        private sealed class ReplayEvent
        {
            public string flagId = CombatEventFlags.Windup;
            public float delaySeconds;
            public bool perTarget;
            public int targetCount = 1;
        }

        private readonly struct ScopedContext : System.IDisposable
        {
            public CombatEventContext Context { get; }

            public ScopedContext()
            {
                Context = CombatEventContext.CreateStub();
            }

            public void Dispose()
            {
                Context?.Release();
            }
        }
    }
}
