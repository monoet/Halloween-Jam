using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    public interface ITimedInputBuffer
    {
        BufferedInput Register(CombatantState actor, string source = null, double? timestamp = null);
        bool TryConsume(
            CombatantState actor,
            double windowStartTime,
            double windowEndTime,
            TimedHitTolerance tolerance,
            out BufferedInput input,
            out double deltaToCenterSeconds,
            double? windowCenterTime = null);
        void Clear(CombatantState actor);
        void ClearAll();
    }

    public readonly struct BufferedInput
    {
        public BufferedInput(CombatantState actor, double timestamp, string source, int sequence)
        {
            Actor = actor;
            Timestamp = timestamp;
            Source = source;
            Sequence = sequence;
        }

        public CombatantState Actor { get; }
        public double Timestamp { get; }
        public string Source { get; }
        public int Sequence { get; }
    }

    public sealed class TimedInputBuffer : ITimedInputBuffer
    {
        private readonly ICombatClock clock;
        private readonly double retentionSeconds;
        private readonly Dictionary<CombatantState, List<BufferedInput>> buffers = new();
        private int nextSequenceId;

        public TimedInputBuffer(ICombatClock clock, double retentionSeconds = 0.35d)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.retentionSeconds = Math.Max(0.05d, retentionSeconds);
        }

        public BufferedInput Register(CombatantState actor, string source = null, double? timestamp = null)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            clock.Sample();
            double stamp = timestamp ?? clock.Now;
            var buffered = new BufferedInput(actor, stamp, source, ++nextSequenceId);

            var list = GetOrCreate(actor);
            list.Add(buffered);

            PruneExpired(list, stamp - retentionSeconds);
            return buffered;
        }

        public bool TryConsume(
            CombatantState actor,
            double windowStartTime,
            double windowEndTime,
            TimedHitTolerance tolerance,
            out BufferedInput input,
            out double deltaToCenterSeconds,
            double? windowCenterTime = null)
        {
            input = default;
            deltaToCenterSeconds = double.NaN;

            if (actor == null)
            {
                return false;
            }

            if (!buffers.TryGetValue(actor, out var list) || list.Count == 0)
            {
                return false;
            }

            clock.Sample();
            double now = clock.Now;

            double earliest = windowStartTime - tolerance.EarlyMilliseconds * 0.001d;
            double latest = windowEndTime + tolerance.LateMilliseconds * 0.001d;

            double pruneCutoff = Math.Min(earliest, windowStartTime) - retentionSeconds;
            PruneExpired(list, pruneCutoff);
            if (list.Count == 0)
            {
                return false;
            }

            double windowCenter = windowCenterTime ?? (windowStartTime + ((windowEndTime - windowStartTime) * 0.5d));
            int bestIndex = -1;
            double bestDistance = double.MaxValue;
            double bestDelta = double.NaN;

            for (int i = 0; i < list.Count; i++)
            {
                var candidate = list[i];
                if (candidate.Timestamp < earliest)
                {
                    continue;
                }

                if (candidate.Timestamp > latest)
                {
                    break;
                }

                double delta = candidate.Timestamp - windowCenter;
                double distance = Math.Abs(delta);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                    bestDelta = delta;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            input = list[bestIndex];
            deltaToCenterSeconds = bestDelta;
            list.RemoveAt(bestIndex);

            PruneExpired(list, now - retentionSeconds);
            return true;
        }

        public void Clear(CombatantState actor)
        {
            if (actor == null)
            {
                return;
            }

            buffers.Remove(actor);
        }

        public void ClearAll() => buffers.Clear();

        private List<BufferedInput> GetOrCreate(CombatantState actor)
        {
            if (!buffers.TryGetValue(actor, out var list))
            {
                list = new List<BufferedInput>(4);
                buffers[actor] = list;
            }

            return list;
        }

        private static void PruneExpired(List<BufferedInput> list, double cutoff)
        {
            if (list.Count == 0)
            {
                return;
            }

            int removeCount = 0;
            while (removeCount < list.Count && list[removeCount].Timestamp < cutoff)
            {
                removeCount++;
            }

            if (removeCount > 0)
            {
                list.RemoveRange(0, Math.Min(removeCount, list.Count));
            }
        }
    }
}
