using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Core;
using UnityEngine;
using LegacyAnimatorWrapper = BattleV2.AnimationSystem.Execution.AnimatorWrapper;

namespace BattleV2.Orchestration.Runtime
{
    /// <summary>
    /// Global registry that maps combatant identifiers to their animation wrappers.
    /// Provides lookup utilities for the animation orchestrator.
    /// </summary>
    public sealed class AnimatorRegistry
    {
        private static readonly Lazy<AnimatorRegistry> LazyInstance = new Lazy<AnimatorRegistry>(() => new AnimatorRegistry());

        private readonly Dictionary<CombatantId, IAnimationWrapper> wrappers = new Dictionary<CombatantId, IAnimationWrapper>();
        private readonly Dictionary<CombatantState, CombatantId> stateToId = new Dictionary<CombatantState, CombatantId>();
        private readonly Dictionary<CombatantState, IAnimationWrapper> legacyCache = new Dictionary<CombatantState, IAnimationWrapper>();
        private readonly object gate = new object();

        public static AnimatorRegistry Instance => LazyInstance.Value;

        private AnimatorRegistry()
        {
        }

        public CombatantId Register(AnimatorWrapper wrapper)
        {
            if (wrapper == null)
            {
                return CombatantId.Empty;
            }

            wrapper.AssignOwner(wrapper.Owner ?? wrapper.GetComponentInParent<CombatantState>());
            var id = wrapper.CombatantId;
            if (!id.HasValue)
            {
                id = CombatantId.FromCombatant(wrapper.Owner);
            }

            RegisterInternal(id, wrapper.Owner, wrapper);
            return id;
        }

        public CombatantId Register(CombatantState combatant, IAnimationWrapper wrapper)
        {
            if (combatant == null || wrapper == null)
            {
                return CombatantId.Empty;
            }

            var id = CombatantId.FromCombatant(combatant);
            RegisterInternal(id, combatant, wrapper);
            return id;
        }

        public void Unregister(AnimatorWrapper wrapper)
        {
            if (wrapper == null)
            {
                return;
            }

            var id = wrapper.CombatantId;
            if (!id.HasValue && wrapper.Owner != null)
            {
                id = CombatantId.FromCombatant(wrapper.Owner);
            }

            Unregister(id, wrapper.Owner);
        }

        public void Unregister(CombatantId id, CombatantState combatant = null)
        {
            if (!id.HasValue && combatant == null)
            {
                return;
            }

            lock (gate)
            {
                if (id.HasValue)
                {
                    wrappers.Remove(id);
                }

                if (combatant != null)
                {
                    stateToId.Remove(combatant);
                    legacyCache.Remove(combatant);
                }
            }
        }

        public bool TryGetWrapper(CombatantState combatant, out IAnimationWrapper wrapper)
        {
            wrapper = null;
            if (combatant == null)
            {
                return false;
            }

            lock (gate)
            {
                if (stateToId.TryGetValue(combatant, out var id) && id.HasValue && wrappers.TryGetValue(id, out wrapper))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetWrapper(CombatantId id, out IAnimationWrapper wrapper)
        {
            if (!id.HasValue)
            {
                wrapper = null;
                return false;
            }

            lock (gate)
            {
                return wrappers.TryGetValue(id, out wrapper);
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                wrappers.Clear();
                stateToId.Clear();
                legacyCache.Clear();
            }
        }

        internal IAnimationWrapper ResolveLegacyWrapper(CombatantState combatant, LegacyAnimatorWrapper legacyWrapper)
        {
            if (combatant == null || legacyWrapper == null)
            {
                return null;
            }

            lock (gate)
            {
                if (legacyCache.TryGetValue(combatant, out var cached))
                {
                    return cached;
                }

                var adapter = new LegacyAnimatorWrapperAdapter(legacyWrapper);
                var id = CombatantId.FromCombatant(combatant);
                wrappers[id] = adapter;
                stateToId[combatant] = id;
                legacyCache[combatant] = adapter;
                return adapter;
            }
        }

        private void RegisterInternal(CombatantId id, CombatantState combatant, IAnimationWrapper wrapper)
        {
            if (!id.HasValue || wrapper == null)
            {
                return;
            }

            lock (gate)
            {
                wrappers[id] = wrapper;
                if (combatant != null)
                {
                    stateToId[combatant] = id;
                }
            }
        }

        /// <summary>
        /// Adapter that allows the orchestration runtime to talk to the legacy AnimatorWrapper (playables) implementation.
        /// </summary>
        private sealed class LegacyAnimatorWrapperAdapter : IAnimationWrapper
        {
            private readonly LegacyAnimatorWrapper legacyWrapper;

            public LegacyAnimatorWrapperAdapter(LegacyAnimatorWrapper wrapper)
            {
                legacyWrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
            }

            public async Task PlayAsync(AnimationPlaybackRequest request, CancellationToken cancellationToken = default)
            {
                if (request.Kind != AnimationPlaybackRequest.PlaybackKind.AnimatorClip || request.AnimationClip == null)
                {
                    return;
                }

                var options = new AnimatorClipOptions(
                    loop: request.Loop,
                    normalizedStartTime: request.NormalizedStartTime,
                    speed: request.Speed,
                    applyFootIK: true,
                    applyPlayableIK: false,
                    overrideDuration: 0d);

                legacyWrapper.PlayClip(request.AnimationClip, options);
                legacyWrapper.AttachCancellation(cancellationToken);

                if (!request.Loop)
                {
                    var duration = request.AnimationClip.length / Math.Max(0.01f, Math.Abs(request.Speed));
                    await Task.Delay(TimeSpan.FromSeconds(duration), cancellationToken);
                }
                else
                {
                    try
                    {
                        await Task.Delay(Timeout.Infinite, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancelled.
                    }
                }
            }

            public void Stop()
            {
                legacyWrapper.Stop();
            }

            public void OnAnimationEvent(AnimationEventPayload payload)
            {
                // Legacy wrapper does not handle animation events. No-op.
            }

            public bool TryGetClip(string id, out AnimationClip clip)
            {
                var installer = AnimationSystemInstaller.Current;
                if (installer?.ClipResolver != null)
                {
                    return installer.ClipResolver.TryGetClip(id, out clip);
                }

                clip = null;
                return false;
            }

            public bool TryGetFlipbook(string id, out FlipbookBinding binding)
            {
                binding = default;
                return false;
            }

            public bool TryGetTween(string id, out TransformTween tween)
            {
                tween = TransformTween.None;
                return false;
            }
        }
    }
}
