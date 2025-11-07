using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;

namespace BattleV2.AnimationSystem.Runtime.Internal
{
    public interface IOrchestratorSessionController
    {
        BattlePhase GetPhase(AnimationContext context);
        void SetPhase(BattlePhase phase, AnimationContext context);
        AmbientHandle StartAmbient(AmbientSpec spec, AnimationContext context);
        void StopAmbient(AmbientHandle handle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        IReadOnlyDictionary<string, BattlePhase> SnapshotPhases();
#endif
    }

    internal sealed class OrchestratorSessionController : IOrchestratorSessionController
    {
        private readonly Dictionary<string, BattlePhase> sessionPhases = new Dictionary<string, BattlePhase>(StringComparer.Ordinal);
        private readonly Dictionary<AmbientHandle, AmbientRecord> ambientTracks = new Dictionary<AmbientHandle, AmbientRecord>();
        private readonly Dictionary<string, HashSet<AmbientHandle>> sessionAmbientIndex = new Dictionary<string, HashSet<AmbientHandle>>(StringComparer.Ordinal);

        public BattlePhase GetPhase(AnimationContext context)
        {
            var sessionId = NormalizeSessionId(context.SessionId);
            if (sessionPhases.TryGetValue(sessionId, out var phase))
            {
                return phase;
            }

            return BattlePhase.None;
        }

        public void SetPhase(BattlePhase phase, AnimationContext context)
        {
            var sessionId = NormalizeSessionId(context.SessionId);
            sessionPhases[sessionId] = phase;
        }

        public AmbientHandle StartAmbient(AmbientSpec spec, AnimationContext context)
        {
            var handle = AmbientHandle.Create();
            ambientTracks[handle] = new AmbientRecord(spec, context);
            var sessionId = NormalizeSessionId(context.SessionId);

            if (!sessionAmbientIndex.TryGetValue(sessionId, out var handles))
            {
                handles = new HashSet<AmbientHandle>();
                sessionAmbientIndex[sessionId] = handles;
            }

            handles.Add(handle);
            return handle;
        }

        public void StopAmbient(AmbientHandle handle)
        {
            if (!handle.IsValid)
            {
                return;
            }

            if (!ambientTracks.Remove(handle, out var record))
            {
                return;
            }

            var sessionId = NormalizeSessionId(record.Context.SessionId);
            if (sessionAmbientIndex.TryGetValue(sessionId, out var handles))
            {
                handles.Remove(handle);
                if (handles.Count == 0)
                {
                    sessionAmbientIndex.Remove(sessionId);
                }
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public IReadOnlyDictionary<string, BattlePhase> SnapshotPhases()
        {
            return new Dictionary<string, BattlePhase>(sessionPhases);
        }
#endif

        private static string NormalizeSessionId(string sessionId)
        {
            return string.IsNullOrWhiteSpace(sessionId) ? AnimationContext.Default.SessionId : sessionId;
        }

        private readonly struct AmbientRecord
        {
            public AmbientRecord(AmbientSpec spec, AnimationContext context)
            {
                Spec = spec ?? AmbientSpec.DefaultLoop();
                Context = context;
            }

            public AmbientSpec Spec { get; }
            public AnimationContext Context { get; }
        }
    }
}
