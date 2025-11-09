using System;
using System.Collections.Generic;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    /// <summary>
    /// Alignment used by listeners to filter who should respond to a combat flag.
    /// </summary>
    public enum CombatantAlignment
    {
        Unknown = 0,
        Ally = 1,
        Enemy = 2
    }

    /// <summary>
    /// Shared payload emitted with every combat event flag. Instances are pooled to avoid GC spikes.
    /// </summary>
    public sealed class CombatEventContext
    {
        private const int MaxPoolSize = 64; // TODO: expose via config if event volume spikes.
        private static readonly Stack<CombatEventContext> Pool = new Stack<CombatEventContext>(MaxPoolSize);
        private static readonly object PoolGate = new object();

        private readonly List<CombatantRef> targetBuffer = new List<CombatantRef>(4);
        private readonly List<string> tagBuffer = new List<string>(4);

        private CombatEventContext()
        {
        }

        public ActorView Actor { get; private set; }
        public ActionView Action { get; private set; }
        public TargetsView Targets { get; private set; }
        public IReadOnlyList<string> Tags => tagBuffer;

        public static CombatEventContext CreateStub()
        {
            var context = Acquire();
            var actor = new ActorView(0, CombatantAlignment.Ally, null, null, null);
            var action = new ActionView("debug_action", "attack/basic", "sword", "neutral", "debug_recipe");
            context.Populate(actor, action, Array.Empty<CombatantRef>(), false, Array.Empty<string>());
            return context;
        }

        internal static CombatEventContext Acquire()
        {
            lock (PoolGate)
            {
                if (Pool.Count > 0)
                {
                    return Pool.Pop();
                }
            }

            return new CombatEventContext();
        }

        internal void Release()
        {
            targetBuffer.Clear();
            tagBuffer.Clear();
            Actor = default;
            Action = default;
            Targets = default;

            lock (PoolGate)
            {
                if (Pool.Count < MaxPoolSize)
                {
                    Pool.Push(this);
                }
            }
        }

        internal void Populate(
            in ActorView actor,
            in ActionView action,
            IReadOnlyList<CombatantRef> targets,
            bool perTarget,
            IEnumerable<string> tags)
        {
            Actor = actor;
            Action = action;

            targetBuffer.Clear();
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    targetBuffer.Add(targets[i]);
                }
            }

            Targets = new TargetsView(targetBuffer, perTarget);

            tagBuffer.Clear();
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        tagBuffer.Add(tag);
                    }
                }
            }
        }

        public readonly struct ActorView
        {
            public ActorView(
                int id,
                CombatantAlignment alignment,
                CombatantState combatant,
                Transform root,
                Transform anchor)
            {
                Id = id;
                Alignment = alignment;
                Combatant = combatant;
                Root = root;
                Anchor = anchor;
            }

            public int Id { get; }
            public CombatantAlignment Alignment { get; }
            public CombatantState Combatant { get; }
            public Transform Root { get; }
            public Transform Anchor { get; }
            public bool IsValid => Combatant != null || Root != null;
        }

        public readonly struct ActionView
        {
            public ActionView(
                string actionId,
                string family,
                string weaponKind,
                string element,
                string recipeId)
            {
                ActionId = string.IsNullOrWhiteSpace(actionId) ? "(unknown)" : actionId;
                Family = string.IsNullOrWhiteSpace(family) ? ActionId : family;
                WeaponKind = string.IsNullOrWhiteSpace(weaponKind) ? "none" : weaponKind;
                Element = string.IsNullOrWhiteSpace(element) ? "neutral" : element;
                RecipeId = recipeId;
            }

            public string ActionId { get; }
            public string Family { get; }
            public string WeaponKind { get; }
            public string Element { get; }
            public string RecipeId { get; }
        }

        public readonly struct TargetsView
        {
            private static readonly IReadOnlyList<CombatantRef> Empty = Array.Empty<CombatantRef>();
            private readonly IReadOnlyList<CombatantRef> targets;

            internal TargetsView(IReadOnlyList<CombatantRef> targets, bool perTarget)
            {
                this.targets = targets ?? Empty;
                PerTarget = perTarget;
            }

            public IReadOnlyList<CombatantRef> All => targets ?? Empty;
            public int Count => (targets ?? Empty).Count;
            public bool PerTarget { get; }

            public CombatantRef this[int index] => (targets ?? Empty)[index];
        }

        public readonly struct CombatantRef
        {
            public CombatantRef(
                int id,
                CombatantAlignment alignment,
                CombatantState combatant,
                Transform root,
                Transform anchor)
            {
                Id = id;
                Alignment = alignment;
                Combatant = combatant;
                Root = root;
                Anchor = anchor;
            }

            public int Id { get; }
            public CombatantAlignment Alignment { get; }
            public CombatantState Combatant { get; }
            public Transform Root { get; }
            public Transform Anchor { get; }
            public bool IsValid => Combatant != null || Root != null;
        }
    }
}
