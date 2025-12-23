using System;
using System.Collections.Generic;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Targeting.Policies
{
    public readonly struct TargetingContext
    {
        public TargetingContext(
            int executionId,
            CombatantState actor,
            string actionId,
            TargetShape shape,
            IReadOnlyList<CombatantState> candidatesAlive,
            int seed)
        {
            ExecutionId = executionId;
            Actor = actor;
            ActionId = actionId;
            Shape = shape;
            CandidatesAlive = candidatesAlive;
            Seed = seed;
        }

        public int ExecutionId { get; }
        public CombatantState Actor { get; }
        public string ActionId { get; }
        public TargetShape Shape { get; }
        public IReadOnlyList<CombatantState> CandidatesAlive { get; }
        public int Seed { get; }
    }

    public interface ITargetingPolicy
    {
        string Id { get; }
        TargetPickResult PickTarget(TargetingContext context);
    }

    public readonly struct TargetPickResult
    {
        public TargetPickResult(CombatantState picked, int index, double roll01)
        {
            Picked = picked;
            Index = index;
            Roll01 = roll01;
        }

        public CombatantState Picked { get; }
        public int Index { get; }
        public double Roll01 { get; }
    }

    public sealed class RandomAliveTargetingPolicy : ITargetingPolicy
    {
        public string Id => "RandomAlive";

        public TargetPickResult PickTarget(TargetingContext context)
        {
            var list = context.CandidatesAlive;
            if (list == null || list.Count == 0)
            {
                return new TargetPickResult(null, -1, double.NaN);
            }

            var rng = new System.Random(context.Seed);
            var roll = rng.NextDouble();
            var idx = (int)(roll * list.Count);
            if (idx >= list.Count)
            {
                idx = list.Count - 1;
            }

            return new TargetPickResult(list[idx], idx, roll);
        }
    }

    public static class EnemyTargetingPolicyRegistry
    {
        private static readonly Dictionary<string, ITargetingPolicy> policies = new(StringComparer.OrdinalIgnoreCase)
        {
            { "RandomAlive", new RandomAliveTargetingPolicy() }
        };

        public static ITargetingPolicy Get(string policyId)
        {
            if (string.IsNullOrWhiteSpace(policyId))
            {
                policyId = "RandomAlive";
            }

            return policies.TryGetValue(policyId, out var policy) ? policy : policies["RandomAlive"];
        }
    }

    public static class EnemyTargetingDebug
    {
        public static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                const int fnvOffset = unchecked((int)2166136261);
                const int fnvPrime = 16777619;
                int hash = fnvOffset;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= fnvPrime;
                }
                return hash;
            }
        }

        public static int MixSeed(uint battleSeed, uint spawnInstanceId, uint turnIndex, uint actionHash)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ battleSeed) * 16777619u;
                h = (h ^ spawnInstanceId) * 16777619u;
                h = (h ^ turnIndex) * 16777619u;
                h = (h ^ actionHash) * 16777619u;
                return (int)h;
            }
        }

        public static string FormatCandidates(IReadOnlyList<CombatantState> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return "[]";
            }

            var parts = new string[Mathf.Min(candidates.Count, 6)];
            for (int i = 0; i < parts.Length; i++)
            {
                var c = candidates[i];
                parts[i] = c != null ? $"{c.DisplayName}#{c.GetInstanceID()}" : "(null)";
            }

            return $"[{string.Join(",", parts)}{(candidates.Count > parts.Length ? ",..+" + (candidates.Count - parts.Length) : string.Empty)}]";
        }
    }
}
