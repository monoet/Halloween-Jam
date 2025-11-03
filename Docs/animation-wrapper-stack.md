# BattleV2 Animation Wrapper Stack (Post-Integration Snapshot)

Este documento recopila el código generado/actualizado durante la tarea *“Implemented the new battle animation wrapper stack without touching BattleManagerV2”*. Cada sección incluye el contenido actual de los archivos relevantes para facilitar revisión o compartirlos con el equipo.

---

## `Assets/Scripts/BattleV2/Orchestration/Runtime/AnimatorWrapper.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace BattleV2.Orchestration.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class AnimatorWrapper : MonoBehaviour, IAnimationWrapper
    {
        [Header("Optional Overrides")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform animatedRoot;
        [SerializeField] private CharacterAnimationSet animationSet;
        [SerializeField] private string combatantIdOverride;
        [SerializeField] private bool autoRegister = true;
        [SerializeField] private bool logPlayback;

        [Header("Events")]
        [SerializeField] private UnityEvent<AnimationEventPayload> onAnimationEvent;

        private CombatantState owner;
        private CombatantId? cachedId;

        private PlayableGraph playableGraph;
        private AnimationPlayableOutput animationOutput;
        private AnimationClipPlayable clipPlayable;
        private bool graphInitialized;

        private CancellationTokenSource playbackCts;
        private Sprite originalSprite;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;

        private void Awake()
        {
            owner = GetComponentInParent<CombatantState>();
            animator ??= GetComponentInChildren<Animator>(true);
            spriteRenderer ??= GetComponentInChildren<SpriteRenderer>(true);
            animatedRoot ??= animator != null ? animator.transform : transform;

            if (spriteRenderer != null)
            {
                originalSprite = spriteRenderer.sprite;
            }

            if (animatedRoot != null)
            {
                originalLocalPosition = animatedRoot.localPosition;
                originalLocalRotation = animatedRoot.localRotation;
                originalLocalScale = animatedRoot.localScale;
            }
        }

        private void OnEnable()
        {
            if (autoRegister)
            {
                AnimatorRegistry.Instance.Register(this);
            }

            RegisterAnimationSet();
        }

        private void OnDisable()
        {
            Stop();
            if (autoRegister)
            {
                AnimatorRegistry.Instance.Unregister(this);
            }
        }

        private void OnDestroy()
        {
            Stop();
            if (graphInitialized && playableGraph.IsValid())
            {
                playableGraph.Destroy();
                graphInitialized = false;
            }
        }

        public CombatantState Owner => owner;

        public void AssignOwner(CombatantState combatant)
        {
            owner = combatant;
            cachedId = null;
        }

        public void OverrideAnimator(Animator overrideAnimator)
        {
            if (overrideAnimator == null)
            {
                return;
            }

            animator = overrideAnimator;
            graphInitialized = false;
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }
        }

        public void SetAnimationSet(CharacterAnimationSet set)
        {
            animationSet = set;
            RegisterAnimationSet();
        }

        public CombatantId CombatantId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(combatantIdOverride))
                {
                    return CombatantId.FromString(combatantIdOverride);
                }

                if (cachedId.HasValue)
                {
                    return cachedId.Value;
                }

                var id = CombatantId.FromCombatant(owner);
                cachedId = id;
                return id;
            }
        }

        public async UniTask PlayAsync(AnimationPlaybackRequest request, CancellationToken cancellationToken = default)
        {
            if (!enabled)
            {
                return;
            }

            CancelPlayback();

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.GetCancellationTokenOnDestroy());
            playbackCts = linkedCts;

            try
            {
                switch (request.Kind)
                {
                    case AnimationPlaybackRequest.PlaybackKind.AnimatorClip:
                        await PlayAnimatorClipAsync(request, linkedCts.Token);
                        break;
                    case AnimationPlaybackRequest.PlaybackKind.SpriteFlipbook:
                        await PlaySpriteFlipbookAsync(request, linkedCts.Token);
                        break;
                    case AnimationPlaybackRequest.PlaybackKind.TransformTween:
                        await PlayTransformTweenAsync(request.Tween, linkedCts.Token);
                        break;
                    default:
                        Debug.LogWarning($"[AnimatorWrapper] Unsupported playback kind '{request.Kind}' on {name}.", this);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled.
            }
            finally
            {
                if (playbackCts == linkedCts)
                {
                    playbackCts.Dispose();
                    playbackCts = null;
                }
            }
        }

        public void Stop()
        {
            CancelPlayback();
            StopAnimatorGraph();
            ResetSpriteRenderer();
            ResetTransformTween();
        }

        public void OnAnimationEvent(AnimationEventPayload payload)
        {
            onAnimationEvent?.Invoke(payload);
        }

        private void CancelPlayback()
        {
            if (playbackCts == null)
            {
                return;
            }

            if (!playbackCts.IsCancellationRequested)
            {
                playbackCts.Cancel();
            }

            playbackCts.Dispose();
            playbackCts = null;
        }

        private async UniTask PlayAnimatorClipAsync(AnimationPlaybackRequest request, CancellationToken token)
        {
            if (animator == null || request.AnimationClip == null)
            {
                LogPlayback($"Animator clip request ignored. Animator or clip missing on '{name}'.");
                return;
            }

            EnsurePlayableGraph();
            StopAnimatorGraph();

            clipPlayable = AnimationClipPlayable.Create(playableGraph, request.AnimationClip);
            clipPlayable.SetApplyFootIK(true);
            clipPlayable.SetApplyPlayableIK(false);
            clipPlayable.SetSpeed(request.Speed);
            clipPlayable.SetTime(request.NormalizedStartTime * Math.Max(0.01f, request.AnimationClip.length));
            clipPlayable.SetDuration(request.Loop ? double.PositiveInfinity : request.AnimationClip.length);
            clipPlayable.SetPropagateSetTime(true);

            animationOutput.SetSourcePlayable(clipPlayable);
            playableGraph.Play();

            LogPlayback($"Playing clip '{request.AnimationClip.name}' (loop={request.Loop}) on '{name}'.");

            if (request.Loop)
            {
                await UniTask.WaitUntilCanceled(token);
            }
            else
            {
                var durationSeconds = request.AnimationClip.length / Math.Max(0.01f, Math.Abs(request.Speed));
                await UniTask.Delay(TimeSpan.FromSeconds(durationSeconds), cancellationToken: token);
            }
        }

        private async UniTask PlaySpriteFlipbookAsync(AnimationPlaybackRequest request, CancellationToken token)
        {
            if (spriteRenderer == null || request.SpriteFrames == null || request.SpriteFrames.Count == 0)
            {
                LogPlayback($"Sprite flipbook request ignored on '{name}'. Missing renderer or frames.");
                return;
            }

            var frames = request.SpriteFrames;
            float step = 1f / Mathf.Max(1f, request.FrameRate);

            LogPlayback($"Playing sprite flipbook ({frames.Count} frames, fps={request.FrameRate}) on '{name}'.");

            do
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    spriteRenderer.sprite = frames[i];
                    await UniTask.Delay(TimeSpan.FromSeconds(step), cancellationToken: token);
                }
            }
            while (request.Loop && !token.IsCancellationRequested);
        }

        private async UniTask PlayTransformTweenAsync(TransformTween tween, CancellationToken token)
        {
            if (animatedRoot == null || !tween.IsValid)
            {
                LogPlayback($"Transform tween invalid on '{name}'.");
                return;
            }

            Vector3 startPos = animatedRoot.localPosition;
            Quaternion startRot = animatedRoot.localRotation;
            Vector3 startScale = animatedRoot.localScale;

            var curve = tween.Easing ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);

            float elapsed = 0f;
            while (elapsed < tween.Duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / tween.Duration);
                float eased = curve.Evaluate(t);

                if (tween.TargetLocalPosition.HasValue)
                {
                    animatedRoot.localPosition = Vector3.LerpUnclamped(startPos, tween.TargetLocalPosition.Value, eased);
                }

                if (tween.TargetLocalRotation.HasValue)
                {
                    animatedRoot.localRotation = Quaternion.SlerpUnclamped(startRot, tween.TargetLocalRotation.Value, eased);
                }

                if (tween.TargetLocalScale.HasValue)
                {
                    animatedRoot.localScale = Vector3.LerpUnclamped(startScale, tween.TargetLocalScale.Value, eased);
                }

                await UniTask.Yield(cancellationToken: token);
            }
        }

        private void EnsurePlayableGraph()
        {
            if (graphInitialized || animator == null)
            {
                return;
            }

            playableGraph = PlayableGraph.Create($"AnimatorWrapper::{name}");
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            animationOutput = AnimationPlayableOutput.Create(playableGraph, $"{name}_Output", animator);
            graphInitialized = true;
        }

        private void StopAnimatorGraph()
        {
            if (!graphInitialized || !playableGraph.IsValid())
            {
                return;
            }

            if (clipPlayable.IsValid())
            {
                clipPlayable.Destroy();
            }

            animationOutput.SetSourcePlayable(Playable.Null);
            playableGraph.Evaluate(0f);
            playableGraph.Stop();
        }

        private void ResetSpriteRenderer()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = originalSprite;
            }
        }

        private void ResetTransformTween()
        {
            if (animatedRoot == null)
            {
                return;
            }

            animatedRoot.localPosition = originalLocalPosition;
            animatedRoot.localRotation = originalLocalRotation;
            animatedRoot.localScale = originalLocalScale;
        }

        private void LogPlayback(string message)
        {
            if (logPlayback)
            {
                Debug.Log($"[AnimatorWrapper] {message}", this);
            }
        }

        public void RegisterAnimationSet()
        {
            if (animationSet == null)
            {
                return;
            }

            var installer = AnimationSystemInstaller.Current;
            if (installer?.ClipResolver == null)
            {
                return;
            }

            installer.ClipResolver.RegisterBindings(animationSet.Entries);
        }
    }
}
```

---

## `Assets/Scripts/BattleV2/Orchestration/Runtime/CharacterAnimationSet.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;
using BattleV2.AnimationSystem.Runtime;

namespace BattleV2.Orchestration.Runtime
{
    [CreateAssetMenu(menuName = "BattleV2/Animation/Character Animation Set")]
    public sealed class CharacterAnimationSet : ScriptableObject
    {
        [SerializeField]
        private AnimationClipBinding[] entries = System.Array.Empty<AnimationClipBinding>();

        public IReadOnlyList<AnimationClipBinding> Entries => entries ?? System.Array.Empty<AnimationClipBinding>();
    }
}
```

---

## `Assets/Scripts/BattleV2/Orchestration/Runtime/IAnimationWrapper.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BattleV2.Orchestration.Runtime
{
    public interface IAnimationWrapper
    {
        UniTask PlayAsync(AnimationPlaybackRequest request, CancellationToken cancellationToken = default);
        void Stop();
        void OnAnimationEvent(AnimationEventPayload payload);
    }

    public readonly struct AnimationPlaybackRequest
    {
        public enum PlaybackKind
        {
            AnimatorClip,
            SpriteFlipbook,
            TransformTween
        }

        public AnimationPlaybackRequest(
            PlaybackKind kind,
            AnimationClip clip,
            IReadOnlyList<Sprite> sprites,
            float frameRate,
            bool loop,
            float speed,
            float normalizedStartTime,
            TransformTween tween)
        {
            Kind = kind;
            AnimationClip = clip;
            SpriteFrames = sprites;
            FrameRate = frameRate;
            Loop = loop;
            Speed = speed <= 0f ? 1f : speed;
            NormalizedStartTime = Mathf.Clamp01(normalizedStartTime);
            Tween = tween;
        }

        public PlaybackKind Kind { get; }
        public AnimationClip AnimationClip { get; }
        public IReadOnlyList<Sprite> SpriteFrames { get; }
        public float FrameRate { get; }
        public bool Loop { get; }
        public float Speed { get; }
        public float NormalizedStartTime { get; }
        public TransformTween Tween { get; }

        public static AnimationPlaybackRequest ForAnimatorClip(AnimationClip clip, float speed = 1f, float normalizedStartTime = 0f, bool loop = false)
        {
            return new AnimationPlaybackRequest(
                PlaybackKind.AnimatorClip,
                clip,
                null,
                0f,
                loop,
                speed,
                normalizedStartTime,
                TransformTween.None);
        }

        public static AnimationPlaybackRequest ForSpriteFlipbook(IReadOnlyList<Sprite> frames, float frameRate = 12f, bool loop = true)
        {
            return new AnimationPlaybackRequest(
                PlaybackKind.SpriteFlipbook,
                null,
                frames,
                Mathf.Max(1f, frameRate),
                loop,
                1f,
                0f,
                TransformTween.None);
        }

        public static AnimationPlaybackRequest ForTransformTween(TransformTween tween)
        {
            return new AnimationPlaybackRequest(
                PlaybackKind.TransformTween,
                null,
                null,
                0f,
                loop: false,
                speed: 1f,
                normalizedStartTime: 0f,
                tween);
        }
    }

    [Serializable]
    public struct TransformTween
    {
        public static TransformTween None => new TransformTween
        {
            Duration = 0f,
            TargetLocalPosition = null,
            TargetLocalRotation = null,
            TargetLocalScale = null,
            Easing = null
        };

        public float Duration;
        public Vector3? TargetLocalPosition;
        public Quaternion? TargetLocalRotation;
        public Vector3? TargetLocalScale;
        public AnimationCurve Easing;

        public bool IsValid =>
            Duration > 0f &&
            (TargetLocalPosition.HasValue || TargetLocalRotation.HasValue || TargetLocalScale.HasValue);
    }

    public readonly struct CombatantId : IEquatable<CombatantId>
    {
        private readonly string value;

        public static CombatantId Empty => new CombatantId(null);

        private CombatantId(string value)
        {
            this.value = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public bool HasValue => !string.IsNullOrEmpty(value);

        public static CombatantId FromString(string customValue) => new CombatantId(customValue);

        public static CombatantId FromCombatant(CombatantState combatant)
        {
            if (combatant == null)
            {
                return Empty;
            }

            return new CombatantId($"{combatant.GetInstanceID()}");
        }

        public bool Equals(CombatantId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CombatantId other && Equals(other);
        public override int GetHashCode() => value != null ? value.GetHashCode(StringComparison.Ordinal) : 0;
        public override string ToString() => value ?? "(null)";
    }
}
```

---
---

## Assets/Scripts/BattleV2/Orchestration/Runtime/AnimatorRegistry.cs

`csharp
using System;
using System.Collections.Generic;
using System.Threading;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LegacyAnimatorWrapper = BattleV2.AnimationSystem.Execution.AnimatorWrapper;

namespace BattleV2.Orchestration.Runtime
{
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

        private sealed class LegacyAnimatorWrapperAdapter : IAnimationWrapper
        {
            private readonly LegacyAnimatorWrapper legacyWrapper;

            public LegacyAnimatorWrapperAdapter(LegacyAnimatorWrapper wrapper)
            {
                legacyWrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
            }

            public async UniTask PlayAsync(AnimationPlaybackRequest request, CancellationToken cancellationToken = default)
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
                    await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: cancellationToken);
                }
                else
                {
                    await UniTask.WaitUntilCanceled(cancellationToken);
                }
            }

            public void Stop()
            {
                legacyWrapper.Stop();
            }

            public void OnAnimationEvent(AnimationEventPayload payload)
            {
                // Legacy wrapper does not handle animation events.
            }
        }
    }
}
`

---
## Assets/Scripts/BattleV2/AnimationSystem/Runtime/AnimationBindingConfig.cs

`csharp
using System;
using System.Collections.Generic;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    [Serializable]
    public sealed class AnimationActorBinding
    {
        [Tooltip("Combatant that owns this presentation entry.")]
        public CombatantState Actor;

        [Tooltip("Animator component driven by the AnimatorWrapper.")]
        public Animator Animator;

        [Tooltip("Idle/fallback pose used when no clip is playing.")]
        public AnimationClip FallbackClip;

        [Tooltip("Optional sockets used by routers (VFX spawn points, etc.).")]
        public Transform[] Sockets;

        public AnimationActorBinding() { }

        public AnimationActorBinding(CombatantState actor, Animator animator, AnimationClip fallbackClip, Transform[] sockets = null)
        {
            Actor = actor;
            Animator = animator;
            FallbackClip = fallbackClip;
            Sockets = sockets ?? Array.Empty<Transform>();
        }

        public bool IsValid => Actor != null && Animator != null;
    }

    [Serializable]
    public struct AnimationClipBinding
    {
        public string Id;
        public AnimationClip Clip;
    }

    public sealed class AnimationClipResolver
    {
        private readonly Dictionary<string, AnimationClip> lookup;

        public AnimationClipResolver(IEnumerable<AnimationClipBinding> bindings)
        {
            lookup = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            if (bindings == null)
            {
                return;
            }

            RegisterBindings(bindings);
        }

        public void RegisterBindings(IEnumerable<AnimationClipBinding> bindings)
        {
            if (bindings == null)
            {
                return;
            }

            foreach (var binding in bindings)
            {
                if (string.IsNullOrWhiteSpace(binding.Id) || binding.Clip == null)
                {
                    continue;
                }

                if (lookup.TryGetValue(binding.Id, out var existingClip))
                {
                    if (existingClip == binding.Clip)
                    {
                        continue;
                    }

                    Debug.LogWarning($"[AnimationClipResolver] Overriding clip binding for id '{binding.Id}'.", binding.Clip);
                }

                lookup[binding.Id] = binding.Clip;
            }
        }

        public bool TryGetClip(string clipId, out AnimationClip clip)
        {
            if (string.IsNullOrWhiteSpace(clipId))
            {
                clip = null;
                return false;
            }

            return lookup.TryGetValue(clipId, out clip);
        }
    }
}
`

---
## Assets/Scripts/BattleV2/AnimationSystem/Runtime/AnimationSystemInstaller.cs *(extracto relevante)*

`csharp
using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Catalog;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Runtime.Internal;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    public sealed class AnimationSystemInstaller : MonoBehaviour
    {
        public static AnimationSystemInstaller Current { get; private set; }
        ...
        private AnimationClipResolver clipResolver;
        ...
        public IAnimationOrchestrator Orchestrator => orchestrator;
        public AnimationClipResolver ClipResolver => clipResolver;
        ...
        var clipResolver = new AnimationClipResolver(clipBindings);
        wrapperResolver = new AnimatorWrapperResolver(ResolveActorBindings());
        ...
        orchestrator = new NewAnimOrchestratorAdapter(
            runtimeBuilder,
            sequencerDriver,
            timelineCatalog,
            lockManager,
            eventBus,
            wrapperResolver,
            clipResolver,
            routerBundle,
            AnimatorRegistry.Instance);
        ...
        private void OnDestroy()
        {
            timedHitService?.Dispose();
            routerBundle?.Dispose();
            wrapperResolver?.Dispose();
            AnimatorRegistry.Instance.Clear();
            if (Current == this)
            {
                Current = null;
            }
        }
        ...
        var orchestrationWrapper = actor.GetComponentInChildren<BattleV2.Orchestration.Runtime.AnimatorWrapper>(true);
        if (orchestrationWrapper != null)
        {
            if (animatorOverride != null)
            {
                orchestrationWrapper.OverrideAnimator(animatorOverride);
            }

            orchestrationWrapper.AssignOwner(actor);
            AnimatorRegistry.Instance.Register(orchestrationWrapper);
        }
    }
}
`

---
## Assets/Scripts/BattleV2/AnimationSystem/Runtime/Internal/AnimationSequenceSession.cs *(extracto relevante)*

`csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using UnityEngine;
using BattleV2.Orchestration.Runtime;
using Cysharp.Threading.Tasks;

namespace BattleV2.AnimationSystem.Runtime.Internal
{
    internal sealed class AnimationSequenceSession : IDisposable
    {
        ...
        private readonly IAnimationWrapper wrapper;
        private CancellationToken sessionCancellationToken;
        private CancellationTokenSource wrapperPlaybackCts;
        ...
        public AnimationSequenceSession(
            AnimationRequest request,
            ActionTimeline timeline,
            ActionSequencer sequencer,
            IAnimationWrapper wrapper,
            AnimationClipResolver clipResolver,
            AnimationRouterBundle routerBundle)
        {
            ...
        }

        public Task RunAsync(ActionSequencerDriver driver, CancellationToken cancellationToken)
        {
            ...
            sessionCancellationToken = cancellationToken;
            ...
        }

        private void HandleAnimationPhase(in SequencerEventInfo info)
        {
            ...
            var playbackRequest = BuildPlaybackRequest(payload, clip);
            CancelWrapperPlayback();
            wrapperPlaybackCts = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellationToken);
            wrapper.PlayAsync(playbackRequest, wrapperPlaybackCts.Token).Forget();
        }

        private static AnimationPlaybackRequest BuildPlaybackRequest(AnimationEventPayload payload, AnimationClip clip)
        {
            ...
            return AnimationPlaybackRequest.ForAnimatorClip(clip, speed, normalizedStart, loop);
        }

        public void Dispose()
        {
            ...
            routerBundle.UnregisterActor(request.Actor);
            CancelWrapperPlayback();
            wrapper.Stop();
        }

        private void CancelInternal(bool asCancellation)
        {
            ...
            CancelWrapperPlayback();
            wrapper.Stop();
            ...
        }

        private void CancelWrapperPlayback()
        {
            if (wrapperPlaybackCts == null)
            {
                return;
            }

            if (!wrapperPlaybackCts.IsCancellationRequested)
            {
                wrapperPlaybackCts.Cancel();
            }

            wrapperPlaybackCts.Dispose();
            wrapperPlaybackCts = null;
        }
    }
}
`

---
## Assets/Scripts/BattleV2/AnimationSystem/Runtime/NewAnimOrchestratorAdapter.cs *(extracto relevante)*

`csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Catalog;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Runtime.Internal;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using UnityEngine;
using BattleV2.Orchestration.Runtime;

namespace BattleV2.AnimationSystem.Runtime
{
    public sealed class NewAnimOrchestratorAdapter : IAnimationOrchestrator, IDisposable
    {
        private readonly AnimatorRegistry registry;
        private readonly Dictionary<CombatantState, AnimationSequenceSession> activeSessions = new();
        private readonly Dictionary<CombatantState, IAnimationWrapper> legacyAdapters = new();
        ...
        public NewAnimOrchestratorAdapter(
            TimelineRuntimeBuilder runtimeBuilder,
            ActionSequencerDriver sequencerDriver,
            ActionTimelineCatalog timelineCatalog,
            IActionLockManager lockManager,
            IAnimationEventBus eventBus,
            AnimatorWrapperResolver wrapperResolver,
            AnimationClipResolver clipResolver,
            AnimationRouterBundle routerBundle,
            AnimatorRegistry registry)
        {
            ...
            this.registry = registry ?? AnimatorRegistry.Instance;
        }
        ...
            IAnimationWrapper wrapper = null;
            if (registry != null)
            {
                registry.TryGetWrapper(request.Actor, out wrapper);
            }

            if (wrapper == null && wrapperResolver != null)
            {
                var legacyWrapper = wrapperResolver.Resolve(request.Actor);
                if (legacyWrapper != null)
                {
                    if (!legacyAdapters.TryGetValue(request.Actor, out wrapper))
                    {
                        wrapper = registry.ResolveLegacyWrapper(request.Actor, legacyWrapper);
                        if (wrapper != null)
                        {
                            legacyAdapters[request.Actor] = wrapper;
                        }
                    }
                }
            }

            if (wrapper == null)
            {
                BattleLogger.Warn("AnimAdapter", $"No AnimatorWrapper binding configured for actor '{request.Actor.name}'.");
                return;
            }
        ...
        public void Dispose()
        {
            ...
            routerBundle.Dispose();
            wrapperResolver.Dispose();
            legacyAdapters.Clear();
        }
    }
}
`

---
## `Assets/Scripts/BattleV2/Orchestration/Services/CombatantRosterService.cs` *(fragmento de registro de wrappers)*

```csharp
        private void RegisterWrapperForCombatant(CombatantState combatant)
        {
            if (combatant == null)
            {
                return;
            }

            var wrappers = combatant.GetComponentsInChildren<AnimatorWrapper>(true);
            if (wrappers == null || wrappers.Length == 0)
            {
                Debug.LogWarning($"[CombatantRosterService] Combatant '{combatant.name}' is missing an AnimatorWrapper component.", combatant);
                return;
            }

            for (int i = 0; i < wrappers.Length; i++)
            {
                var wrapper = wrappers[i];
                if (wrapper == null)
                {
                    continue;
                }

                wrapper.AssignOwner(combatant);
                AnimatorRegistry.Instance.Register(wrapper);
                wrapper.RegisterAnimationSet();
            }
        }
```
