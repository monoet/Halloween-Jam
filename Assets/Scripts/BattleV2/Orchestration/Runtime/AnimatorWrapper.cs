using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Core;
using BattleV2.Common;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace BattleV2.Orchestration.Runtime
{
    public enum VariantStrategy
    {
        PlayBaseOnly,
        AlwaysLast,
        Cycle,
        SequenceOnce
    }

    [Serializable]
    public sealed class CommandVariantConfig
    {
        public string baseCommandId;
        public List<string> variants = new List<string>();
        public VariantStrategy strategy = VariantStrategy.Cycle;
        public bool fallbackToBaseIfMissing = true;
        public bool advanceGuardSameFrame = true;
    }

    /// <summary>
    /// Component attached to battle prefabs responsible for handling local animation playback.
    /// Supports Animator clips (without controllers), sprite flipbooks and simple transform tweens.
    /// </summary>
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
        [SerializeField] private bool registerToGlobalResolver = false;
        [SerializeField] private List<CommandVariantConfig> commandVariants = new List<CommandVariantConfig>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Debug")]
        [SerializeField] private bool debugAw01Enabled = true;
#endif

        [Header("Events")]
        [SerializeField] private UnityEvent<AnimationEventPayload> onAnimationEvent;

        private CombatantState owner;
        private CombatantId? cachedId;

        private PlayableGraph playableGraph;
        private AnimationPlayableOutput animationOutput;
        private AnimationClipPlayable clipPlayable;
        private bool graphInitialized;

        private CancellationTokenSource playbackCts;
        private CancellationTokenSource destroyCts;
        private Sprite originalSprite;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string lastDebugCommandId;
        private int lastDebugFrame = -1;
#endif
        private string lastExecKey;
        private int lastExecFrame = -1;
        private readonly Dictionary<string, CommandVariantConfig> variantMap = new Dictionary<string, CommandVariantConfig>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> variantIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> variantLastFrame = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim consumeGate = new SemaphoreSlim(1, 1);

        private void Awake()
        {
            destroyCts ??= new CancellationTokenSource();
            owner = GetComponentInParent<CombatantState>();
            animator ??= GetComponentInChildren<Animator>(true);
            spriteRenderer ??= GetComponentInChildren<SpriteRenderer>(true);
            animatedRoot ??= animator != null ? animator.transform : transform;
            BuildVariantMap();

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
            destroyCts ??= new CancellationTokenSource();
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

            if (destroyCts != null)
            {
                if (!destroyCts.IsCancellationRequested)
                {
                    destroyCts.Cancel();
                }

                destroyCts.Dispose();
                destroyCts = null;
            }
        }

        public CombatantState Owner => owner;

        public void AssignOwner(CombatantState combatant)
        {
            owner = combatant;
            cachedId = null;
        }

        public void SetAnimationSet(CharacterAnimationSet set)
        {
            animationSet = set;
            RegisterAnimationSet();
            BuildVariantMap();
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

        public Transform AnimatedRoot => animatedRoot != null
            ? animatedRoot
            : animator != null ? animator.transform : transform;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugReceiveCommand(string commandId, string source, string context = null)
        {
            if (!debugAw01Enabled || string.IsNullOrWhiteSpace(commandId))
            {
                return;
            }

            int frame = Time.frameCount;
            if (frame == lastDebugFrame && string.Equals(commandId, lastDebugCommandId, StringComparison.Ordinal))
            {
                return; // de-spam: ignore same command in the same frame
            }

            lastDebugFrame = frame;
            lastDebugCommandId = commandId;

            var actorName = owner != null ? owner.name : "(null)";
            Debug.Log($"[DEBUG-AW01] actor='{actorName}' cmd='{commandId}' src='{source}' ctx='{context ?? "(none)"}'", this);
        }
#endif

        public async Task ConsumeCommand(string commandId, string source, string context, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return;
            }

            await UnityMainThread.SwitchAsync();

            AnimationPlaybackRequest playbackRequest = default;
            bool shouldPlay = false;
            await consumeGate.WaitAsync(ct);
            try
            {
                string execKey = BuildExecKey(commandId, context);
                int frame = Time.frameCount;
                if (execKey == lastExecKey && frame == lastExecFrame)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (debugAw01Enabled)
                    {
                        Debug.Log($"[DEBUG-AW01] DESPAM key='{execKey}'", this);
                    }
#endif
                    return;
                }

                lastExecKey = execKey;
                lastExecFrame = frame;

                string resolvedId = ResolveCommandId(commandId);
                var resolvedIsVariant = !string.Equals(resolvedId, commandId, StringComparison.OrdinalIgnoreCase);
                var cfg = TryGetVariantConfig(commandId);

                if (animator == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (debugAw01Enabled)
                    {
                        Debug.Log($"[DEBUG-AW01] NO_ANIMATOR actor='{owner?.name ?? "(null)"}'", this);
                    }
#endif
                    return;
                }

                if (animationSet == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (debugAw01Enabled)
                    {
                        Debug.Log($"[DEBUG-AW01] NO_SET actor='{owner?.name ?? "(null)"}'", this);
                    }
#endif
                    return;
                }

                if (!animationSet.TryGetClip(resolvedId, out var clip))
                {
                    if (resolvedIsVariant && cfg != null && cfg.fallbackToBaseIfMissing)
                    {
                        animationSet.TryGetClip(commandId, out clip);
                        if (clip != null)
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (debugAw01Enabled)
                            {
                                Debug.Log($"[DEBUG-AW01] MISSING_VARIANT actor='{owner?.name ?? "(null)"}' base='{commandId}' missing='{resolvedId}' -> fallback='{commandId}'", this);
                            }
#endif
                            resolvedId = commandId;
                        }
                    }

                    if (clip == null)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (debugAw01Enabled)
                        {
                            var available = animationSet.ClipBindings != null
                                ? string.Join(",", animationSet.ClipBindings.Select(b => b.Id))
                                : "(none)";
                            Debug.Log($"[DEBUG-AW01] MISSING actor='{owner?.name ?? "(null)"}' id='{resolvedId}' available=[{available}]", this);
                        }
#endif
                        return;
                    }
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (debugAw01Enabled)
                {
                    Debug.Log($"[DEBUG-AW01] consume actor='{owner?.name ?? "(null)"}' cmd='{commandId}' resolved='{resolvedId}' src='{source}' ctx='{context ?? "(none)"}'", this);
                }
#endif

                playbackRequest = AnimationPlaybackRequest.ForAnimatorClip(clip, 1f, 0f, loop: false, commandId: resolvedId);
                shouldPlay = true;
            }
            finally
            {
                consumeGate.Release();
            }

            if (!shouldPlay)
            {
                return;
            }

            try
            {
                await PlayAsync(playbackRequest, ct);
            }
            catch (OperationCanceledException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (debugAw01Enabled)
                {
                    Debug.Log($"[DEBUG-AW01] CANCELLED actor='{owner?.name ?? "(null)"}' id='{playbackRequest.CommandId ?? "(null)"}'", this);
                }
#endif
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (debugAw01Enabled)
                {
                    Debug.Log($"[DEBUG-AW01] ERROR actor='{owner?.name ?? "(null)"}' id='{playbackRequest.CommandId ?? "(null)"}' ex='{ex}'", this);
                }
#endif
            }
        }

        private string BuildExecKey(string commandId, string context)
        {
            var actorKey = owner != null ? owner.GetHashCode().ToString() : "(nullActor)";
            return $"{actorKey}|{commandId}|{context ?? "(nullCtx)"}";
        }

        private CommandVariantConfig TryGetVariantConfig(string baseCommandId)
        {
            if (string.IsNullOrWhiteSpace(baseCommandId))
            {
                return null;
            }

            variantMap.TryGetValue(baseCommandId, out var cfg);
            return cfg;
        }

        private string ResolveCommandId(string baseCommandId)
        {
            if (string.IsNullOrWhiteSpace(baseCommandId))
            {
                return baseCommandId;
            }

            if (!variantMap.TryGetValue(baseCommandId, out var cfg) || cfg == null || cfg.variants == null || cfg.variants.Count == 0)
            {
                return baseCommandId;
            }

            int idx = 0;
            variantIndex.TryGetValue(baseCommandId, out idx);
            int lastFrame = -1;
            variantLastFrame.TryGetValue(baseCommandId, out lastFrame);
            bool sameFrame = lastFrame == Time.frameCount;

            string resolved;
            switch (cfg.strategy)
            {
                case VariantStrategy.PlayBaseOnly:
                    resolved = baseCommandId;
                    break;
                case VariantStrategy.AlwaysLast:
                    resolved = cfg.variants[cfg.variants.Count - 1];
                    break;
                case VariantStrategy.SequenceOnce:
                    resolved = cfg.variants[Mathf.Clamp(idx, 0, cfg.variants.Count - 1)];
                    if (!(cfg.advanceGuardSameFrame && sameFrame))
                    {
                        idx = Mathf.Min(idx + 1, cfg.variants.Count - 1);
                    }
                    break;
                case VariantStrategy.Cycle:
                default:
                    resolved = cfg.variants[idx % cfg.variants.Count];
                    if (!(cfg.advanceGuardSameFrame && sameFrame))
                    {
                        idx = (idx + 1) % cfg.variants.Count;
                    }
                    break;
            }

            variantIndex[baseCommandId] = idx;
            variantLastFrame[baseCommandId] = Time.frameCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugAw01Enabled)
            {
                Debug.Log($"[DEBUG-AW01] variant actor='{owner?.name ?? "(null)"}' base='{baseCommandId}' strategy='{cfg.strategy}' -> resolved='{resolved}' nextIdx={idx}", this);
            }
#endif

            return resolved;
        }

        private void BuildVariantMap()
        {
            variantMap.Clear();
            variantIndex.Clear();
            variantLastFrame.Clear();

            if (commandVariants == null)
            {
                return;
            }

            foreach (var cfg in commandVariants)
            {
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.baseCommandId) || cfg.variants == null || cfg.variants.Count == 0)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (debugAw01Enabled)
                    {
                        Debug.Log($"[DEBUG-AW01] variant config skipped actor='{owner?.name ?? "(null)"}' reason='null/empty base or variants'", this);
                    }
#endif
                    continue;
                }

                variantMap[cfg.baseCommandId] = cfg;
                variantIndex[cfg.baseCommandId] = 0;
                variantLastFrame[cfg.baseCommandId] = -1;
            }
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

        public async Task PlayAsync(AnimationPlaybackRequest request, CancellationToken cancellationToken = default)
        {
            await UnityMainThread.SwitchAsync();

            if (!enabled)
            {
                return;
            }

            CancelPlayback();

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetDestroyCancellationToken());
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

        public void ResetVariantScope(string reason = null, string ctx = null)
        {
            foreach (var key in variantIndex.Keys.ToArray())
            {
                variantIndex[key] = 0;
            }

            foreach (var key in variantLastFrame.Keys.ToArray())
            {
                variantLastFrame[key] = -1;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugAw01Enabled)
            {
                Debug.Log($"[DEBUG-AW01] ResetVariantScope actor='{owner?.name ?? "(null)"}' reason='{reason ?? "(null)"}' ctx='{ctx ?? "(null)"}'", this);
            }
#endif
        }

        public void ResetToFallback(float fadeDuration = 0.1f)
        {
#if false
            Debug.Log($"TTDebug06 [RESET_TO_FALLBACK] actor={owner?.name ?? "(null)"} position={transform.position} fade={fadeDuration}", this);
#endif
            var tweenObserver = GetComponentInChildren<BattleV2.AnimationSystem.Execution.Runtime.Observers.RecipeTweenObserver>(true);
            tweenObserver?.ResetToHomeImmediate();
            // Legacy Mono wrapper does not track a dedicated fallback clip, so stopping playback
            // returns the rig to its authored bind pose / sprite state.
            Stop();
        }

        private void CancelPlayback()
        {
            // Snapshot local para evitar race/reentrada sobre playbackCts
            var cts = playbackCts;
            if (cts == null)
            {
                return;
            }

            // Rompemos el vínculo de inmediato: si otra llamada entra a CancelPlayback, verá null y saldrá.
            playbackCts = null;

            try
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
            }
            catch (ObjectDisposedException)
            {
                // Ya estaba disposed, ignorar.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.Log("[DEBUG-AW01] CancelPlayback: CTS already disposed for '" + gameObject.name + "'", this);
#endif
            }
            finally
            {
                cts.Dispose();
            }
        }

        private async Task PlayAnimatorClipAsync(AnimationPlaybackRequest request, CancellationToken token)
        {
            if (animator == null || request.AnimationClip == null)
            {
                LogPlayback($"Animator clip request ignored. Animator or clip missing on '{name}'.");
                return;
            }

            var cmdId = request.CommandId;
            float holdSeconds = 0f;
            bool hasHold = !string.IsNullOrWhiteSpace(cmdId) && animationSet != null && animationSet.TryGetHoldOffsetSeconds(cmdId, out holdSeconds) && holdSeconds > 0f;
            string holdSkipReason = null;
            if (!hasHold)
            {
                if (animationSet == null) holdSkipReason = "no_set";
                else if (string.IsNullOrWhiteSpace(cmdId)) holdSkipReason = "no_cmd";
                else holdSkipReason = "no_hold";
            }

            EnsurePlayableGraph();
            StopAnimatorGraph();

            var clip = request.AnimationClip;
            clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            clipPlayable.SetApplyFootIK(true);
            clipPlayable.SetApplyPlayableIK(false);
            float clipLength = Mathf.Max(0.01f, clip.length);
            float startTime = Mathf.Clamp01(request.NormalizedStartTime) * clipLength;
            clipPlayable.SetTime(startTime);
            clipPlayable.SetSpeed(hasHold ? 0f : request.Speed);
            clipPlayable.SetDuration(request.Loop ? double.PositiveInfinity : clipLength);
            clipPlayable.SetPropagateSetTime(true);

            animationOutput.SetSourcePlayable(clipPlayable);
            playableGraph.Play();
            playableGraph.Evaluate(0f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hasHold && debugAw01Enabled)
            {
                Debug.Log($"[DEBUG-AW02] hold actor='{owner?.name ?? "(null)"}' id='{cmdId}' seconds={holdSeconds:0.###} startNorm={request.NormalizedStartTime:0.###}", this);
            }
            else if (debugAw01Enabled)
            {
                Debug.Log($"[DEBUG-AW02] hold_skip actor='{owner?.name ?? "(null)"}' id='{cmdId ?? "(null)"}' reason='{holdSkipReason ?? "no_hold"}'", this);
            }
#endif

            if (hasHold)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(holdSeconds), token);
                }
                catch (OperationCanceledException)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (debugAw01Enabled)
                    {
                        Debug.Log($"[DEBUG-AW02] hold_cancel actor='{owner?.name ?? "(null)"}' id='{cmdId}'", this);
                    }
#endif
                    throw;
                }

                clipPlayable.SetSpeed(request.Speed);
                playableGraph.Evaluate(0f);
            }

            LogPlayback($"Playing clip '{clip.name}' (loop={request.Loop}) on '{name}'.");

            if (request.Loop)
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelled.
                }
            }
            else
            {
                float speedAbs = Math.Max(0.01f, Math.Abs(request.Speed));
                float remainingSeconds = Math.Max(0f, (clipLength - startTime)) / speedAbs;
                if (hasHold)
                {
                    remainingSeconds = Math.Max(0f, remainingSeconds - holdSeconds);
                }
                await Task.Delay(TimeSpan.FromSeconds(remainingSeconds), token);
            }
        }

        private async Task PlaySpriteFlipbookAsync(AnimationPlaybackRequest request, CancellationToken token)
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
                    await Task.Delay(TimeSpan.FromSeconds(step), token);
                }
            }
            while (request.Loop && !token.IsCancellationRequested);
        }

        private async Task PlayTransformTweenAsync(TransformTween tween, CancellationToken token)
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

                await Task.Yield();
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

        private CancellationToken GetDestroyCancellationToken()
        {
            if (destroyCts == null)
            {
                destroyCts = new CancellationTokenSource();
            }

            // Si ya fue cancelado (OnDestroy), no recreamos ni "revivimos".
            return destroyCts.Token;
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
                Debug.LogWarning($"[AnimatorWrapper] {name} missing CharacterAnimationSet reference.");
                return;
            }

            if (logPlayback)
            {
                Debug.Log($"[AnimatorWrapper] {name} using animation set '{animationSet.name}'.");
            }
            animationSet.WarmUpCache();

            var installer = AnimationSystemInstaller.Current;
            if (!registerToGlobalResolver || installer?.ClipResolver == null)
            {
                return;
            }

            installer.ClipResolver.RegisterBindings(animationSet.ClipBindings);
        }

        public bool TryGetClip(string id, out AnimationClip clip)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                if (animationSet != null && animationSet.TryGetClip(id, out clip))
                {
                    return true;
                }

                var installer = AnimationSystemInstaller.Current;
                if (installer?.ClipResolver != null && installer.ClipResolver.TryGetClip(id, out clip))
                {
                    return true;
                }
            }

            clip = null;
            return false;
        }

        public bool TryGetFlipbook(string id, out FlipbookBinding binding)
        {
            if (animationSet != null)
            {
                return animationSet.TryGetFlipbook(id, out binding);
            }

            binding = default;
            return false;
        }

        public bool TryGetTween(string id, out TransformTween tween)
        {
            if (animationSet != null)
            {
                return animationSet.TryGetTween(id, out tween);
            }

            tween = TransformTween.None;
            return false;
        }
    }
}
