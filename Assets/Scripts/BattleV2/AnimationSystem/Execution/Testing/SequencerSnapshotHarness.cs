using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Timelines;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Testing
{
    /// <summary>
    /// Helper to capture sequencer event traces at fixed frame rates.
    /// Drive SequencerSnapshotHarness.RunForFps in editor or tests to compare outputs.
    /// </summary>
    public static class SequencerSnapshotHarness
    {
        public static SequencerSnapshot Run(ActionSequencer sequencer, float fps, int maxIterations = 1200)
        {
            if (sequencer == null)
            {
                throw new ArgumentNullException(nameof(sequencer));
            }

            double delta = fps <= 0f ? 1.0 / 60.0 : 1.0 / fps;
            var log = new List<EventLogEntry>(64);
            sequencer.EventDispatched += HandleEvent;
            sequencer.Start();

            int iterations = 0;
            while (!sequencer.IsCompleted && iterations < maxIterations)
            {
                sequencer.Tick();
                iterations++;
            }

            sequencer.EventDispatched -= HandleEvent;
            sequencer.Dispose();
            return new SequencerSnapshot(fps, log);

            void HandleEvent(SequencerEventInfo info)
            {
                log.Add(new EventLogEntry(info));
            }
        }

        public static void SaveSnapshot(SequencerSnapshot snapshot, string path)
        {
            if (snapshot.Events == null)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"# Snapshot FPS={snapshot.Fps.ToString(CultureInfo.InvariantCulture)}");
            for (int i = 0; i < snapshot.Events.Count; i++)
            {
                builder.AppendLine(snapshot.Events[i].ToString());
            }

            File.WriteAllText(path, builder.ToString());
        }

        public readonly struct SequencerSnapshot
        {
            public SequencerSnapshot(float fps, IReadOnlyList<EventLogEntry> events)
            {
                Fps = fps;
                Events = events;
            }

            public float Fps { get; }
            public IReadOnlyList<EventLogEntry> Events { get; }
        }

        public readonly struct EventLogEntry
        {
            public EventLogEntry(SequencerEventInfo info)
            {
                Type = info.Type;
                Scheduled = info.ScheduledTime;
                Elapsed = info.ElapsedTime;
                Index = info.Index;
                Count = info.TotalCount;
                Tag = info.Tag ?? string.Empty;
                Payload = info.Payload ?? string.Empty;
                Reason = info.Reason ?? string.Empty;
            }

            public ScheduledEventType Type { get; }
            public double Scheduled { get; }
            public double Elapsed { get; }
            public int Index { get; }
            public int Count { get; }
            public string Tag { get; }
            public string Payload { get; }
            public string Reason { get; }

            public override string ToString()
            {
                return $"{Type}|{Scheduled:F6}|{Elapsed:F6}|{Index}/{Count}|{Tag}|{Reason}|{Payload}";
            }
        }
    }
}
