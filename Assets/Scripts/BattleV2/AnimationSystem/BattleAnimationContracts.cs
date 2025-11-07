
using System;
using System.Collections.Generic;
using System.Threading;
using BattleV2.Core;

namespace BattleV2.AnimationSystem
{
    /// <summary>
    /// Logical phases used by the animation orchestrator. Kept extensible for future party/multiplayer contexts.
    /// </summary>
    public enum BattlePhase
    {
        None = 0,
        Intro = 1,
        Loop = 2,
        Turn = 3,
        Outro = 4,
        Cinematic = 5
    }

    /// <summary>
    /// Context payload describing which session, party, or actors an orchestration call targets.
    /// </summary>
    public readonly struct AnimationContext
    {
        public static readonly AnimationContext Default = new AnimationContext("default");

        public AnimationContext(
            string sessionId,
            CombatantState primaryActor = null,
            IReadOnlyList<CombatantState> participants = null)
        {
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId;
            PrimaryActor = primaryActor;
            Participants = participants;
        }

        public string SessionId { get; }
        public CombatantState PrimaryActor { get; }
        public IReadOnlyList<CombatantState> Participants { get; }

        public AnimationContext WithSession(string sessionId)
        {
            return new AnimationContext(sessionId, PrimaryActor, Participants);
        }

        public AnimationContext WithPrimaryActor(CombatantState actor)
        {
            return new AnimationContext(SessionId, actor, Participants);
        }
    }

    /// <summary>
    /// Parameters describing an ambient/persistent presentation track (camera pan, HUD state, idle, etc.).
    /// </summary>
    public sealed class AmbientSpec
    {
        public static AmbientSpec IntroDefault()
        {
            return new AmbientSpec("ambient_intro_default", loop: true, BattlePhase.Intro);
        }

        public static AmbientSpec DefaultLoop()
        {
            return new AmbientSpec("ambient_loop_default", loop: true, BattlePhase.Loop);
        }

        public AmbientSpec(string id, bool loop, BattlePhase phase = BattlePhase.Loop)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "ambient_default" : id;
            Loop = loop;
            Phase = phase;
        }

        public string Id { get; }
        public bool Loop { get; }
        public BattlePhase Phase { get; }
    }

    /// <summary>
    /// Lightweight token returned when an ambient track is started.
    /// </summary>
    public readonly struct AmbientHandle : IEquatable<AmbientHandle>
    {
        private static long nextId;

        public AmbientHandle(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value != 0;

        public static AmbientHandle Create()
        {
            long id = Interlocked.Increment(ref nextId);
            return new AmbientHandle(id);
        }

        public bool Equals(AmbientHandle other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AmbientHandle handle && Equals(handle);
        public override int GetHashCode() => Value.GetHashCode();

        public static readonly AmbientHandle Invalid = new AmbientHandle(0);
    }
}
