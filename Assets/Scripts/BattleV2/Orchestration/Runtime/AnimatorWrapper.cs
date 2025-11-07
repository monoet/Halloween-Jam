using System;
using System.Threading;
using System.Threading.Tasks;
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

        private void Awake()
        {
            destroyCts ??= new CancellationTokenSource();
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

        private async Task PlayAnimatorClipAsync(AnimationPlaybackRequest request, CancellationToken token)
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
                var durationSeconds = request.AnimationClip.length / Math.Max(0.01f, Math.Abs(request.Speed));
                await Task.Delay(TimeSpan.FromSeconds(durationSeconds), token);
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
            if (destroyCts == null || destroyCts.IsCancellationRequested)
            {
                destroyCts?.Dispose();
                destroyCts = new CancellationTokenSource();
            }
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

            Debug.Log($"[AnimatorWrapper] {name} using animation set '{animationSet.name}'.");
            animationSet.WarmUpCache();

            var installer = AnimationSystemInstaller.Current;
            if (installer?.ClipResolver == null)
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
