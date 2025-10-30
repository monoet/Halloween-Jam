using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BattleV2.AnimationSystem.Execution
{
    /// <summary>
    /// Lightweight adapter that hides the Playables plumbing from higher level animation code.
    /// Manages a single playable graph per actor and exposes a minimal API for the sequencer/runtime builder.
    /// </summary>
    public sealed class AnimatorWrapper : IDisposable
    {
        private readonly AnimatorWrapperBinding binding;
        private readonly string graphName;

        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationMixerPlayable mixer;

        private AnimationClipPlayable activeClip;
        private AnimationClipPlayable fallbackClip;

        private CancellationTokenRegistration cancellationRegistration;
        private bool initialized;
        private bool disposed;

        public AnimatorWrapper(AnimatorWrapperBinding binding)
        {
            this.binding = binding ?? throw new ArgumentNullException(nameof(binding));
            if (binding.Animator == null)
            {
                throw new ArgumentException("AnimatorWrapperBinding.Animator cannot be null.", nameof(binding));
            }

            graphName = $"AnimatorWrapper::{binding.Animator.gameObject.name}";
        }

        /// <summary>
        /// Returns whether the playable graph is active and playing clips.
        /// </summary>
        public bool IsActive => initialized && graph.IsValid() && graph.IsPlaying();

        /// <summary>
        /// Returns the animator associated with this wrapper.
        /// </summary>
        public Animator Animator => binding.Animator;

        /// <summary>
        /// Returns the sockets associated with the actor (used by VFX routers).
        /// </summary>
        public Transform[] Sockets => binding.Sockets;

        /// <summary>
        /// Lazily create and start the PlayableGraph using the binding information.
        /// </summary>
        public void EnsureInitialized()
        {
            if (disposed || initialized)
            {
                return;
            }

            graph = PlayableGraph.Create(graphName);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            mixer = AnimationMixerPlayable.Create(graph, 2, true);
            output = AnimationPlayableOutput.Create(graph, $"{graphName}_Output", binding.Animator);
            output.SetSourcePlayable(mixer);

            if (binding.FallbackClip != null)
            {
                fallbackClip = AnimationClipPlayable.Create(graph, binding.FallbackClip);
                fallbackClip.SetApplyFootIK(true);
                fallbackClip.SetSpeed(1f);
                graph.Connect(fallbackClip, 0, mixer, 1);
                mixer.SetInputWeight(1, 1f);
            }

            graph.Play();
            initialized = true;
        }

        /// <summary>
        /// Play the given clip immediately, fading out the fallback pose if necessary.
        /// </summary>
        public AnimatorClipHandle PlayClip(AnimationClip clip, AnimatorClipOptions options = default)
        {
            if (clip == null)
            {
                Debug.LogWarning($"[AnimatorWrapper] Attempt to play a null clip on {graphName}.");
                return AnimatorClipHandle.Invalid;
            }

            EnsureInitialized();
            TearDownActiveClip();

            activeClip = AnimationClipPlayable.Create(graph, clip);
            ConfigureClipPlayable(activeClip, options);
            graph.Connect(activeClip, 0, mixer, 0);

            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, binding.FallbackClip != null ? 0f : mixer.GetInputWeight(1));

            return new AnimatorClipHandle(activeClip);
        }

        /// <summary>
        /// Request a smooth blend back to the fallback pose. Optional duration controls the fade time.
        /// </summary>
        public void ResetToFallback(float fadeDuration = 0.1f)
        {
            if (!initialized || !mixer.IsValid())
            {
                return;
            }

            if (!activeClip.IsValid())
            {
                // Already on fallback.
                mixer.SetInputWeight(1, 1f);
                return;
            }

            if (fadeDuration <= 0f)
            {
                mixer.SetInputWeight(0, 0f);
                mixer.SetInputWeight(1, 1f);
                TearDownActiveClip();
                return;
            }

            // TODO: replace with a time-based fade driver once the sequencer wires delta time.
            mixer.SetInputWeight(0, 0f);
            mixer.SetInputWeight(1, 1f);
            TearDownActiveClip();
            Debug.LogWarning($"[AnimatorWrapper] Requested fade duration {fadeDuration:0.##}s but time-based fades are not implemented yet for {graphName}.");
        }

        /// <summary>
        /// Stop all playables and clear the graph. Typically used when the animation completes or is cancelled.
        /// </summary>
        public void Stop()
        {
            ResetToFallback(0f);
        }

        /// <summary>
        /// Attach a cancellation token that will stop the graph when triggered.
        /// </summary>
        public void AttachCancellation(CancellationToken token)
        {
            if (!token.CanBeCanceled)
            {
                return;
            }

            cancellationRegistration.Dispose();
            cancellationRegistration = token.Register(() =>
            {
                Stop();
                Debug.Log($"[AnimatorWrapper] Cancellation requested for {graphName}. Graph stopped.");
            });
        }

        private void ConfigureClipPlayable(AnimationClipPlayable playable, AnimatorClipOptions options)
        {
            playable.SetApplyFootIK(options.ApplyFootIK);
            playable.SetApplyPlayableIK(options.ApplyPlayableIK);

            float clipLength = playable.GetAnimationClip()?.length ?? 0f;
            if (clipLength <= 0f)
            {
                clipLength = 1f;
            }

            float startTime = Mathf.Clamp01(options.NormalizedStartTime) * clipLength;
            playable.SetTime(startTime);
            playable.SetSpeed(Mathf.Approximately(options.Speed, 0f) ? 1f : options.Speed);
            playable.SetPropagateSetTime(true);

            if (!options.Loop)
            {
                playable.SetDuration(options.OverrideDuration > 0d ? options.OverrideDuration : clipLength);
                playable.SetDone(false);
            }
        }

        private void TearDownActiveClip()
        {
            if (activeClip.IsValid())
            {
                if (graph.IsValid() && mixer.IsValid())
                {
                    graph.Disconnect(mixer, 0);
                }

                activeClip.Destroy();
                activeClip = default;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            cancellationRegistration.Dispose();

            if (graph.IsValid())
            {
                if (activeClip.IsValid())
                {
                    activeClip.Destroy();
                }

                if (fallbackClip.IsValid())
                {
                    fallbackClip.Destroy();
                }

                if (mixer.IsValid())
                {
                    mixer.Destroy();
                }

                graph.Destroy();
            }
        }
    }

    /// <summary>
    /// Struct that provides the data needed to create an AnimatorWrapper for a specific actor.
    /// </summary>
    public sealed class AnimatorWrapperBinding
    {
        public AnimatorWrapperBinding(Animator animator, AnimationClip fallbackClip, Transform[] sockets = null)
        {
            Animator = animator;
            FallbackClip = fallbackClip;
            Sockets = sockets ?? Array.Empty<Transform>();
        }

        public Animator Animator { get; }
        public AnimationClip FallbackClip { get; }
        public Transform[] Sockets { get; }
    }

    /// <summary>
    /// Options that control how a clip is configured when played through the wrapper.
    /// </summary>
    public readonly struct AnimatorClipOptions
    {
        public AnimatorClipOptions(
            bool loop = true,
            float normalizedStartTime = 0f,
            float speed = 1f,
            bool applyFootIK = true,
            bool applyPlayableIK = false,
            double overrideDuration = 0d)
        {
            Loop = loop;
            NormalizedStartTime = normalizedStartTime;
            Speed = speed;
            ApplyFootIK = applyFootIK;
            ApplyPlayableIK = applyPlayableIK;
            OverrideDuration = overrideDuration;
        }

        public bool Loop { get; }
        public float NormalizedStartTime { get; }
        public float Speed { get; }
        public bool ApplyFootIK { get; }
        public bool ApplyPlayableIK { get; }
        public double OverrideDuration { get; }

        public static AnimatorClipOptions Default => new();
    }

    /// <summary>
    /// Lightweight handle the sequencer can use to query whether a clip is still valid.
    /// </summary>
    public readonly struct AnimatorClipHandle
    {
        internal AnimatorClipHandle(Playable playable)
        {
            Playable = playable;
        }

        public static AnimatorClipHandle Invalid => new AnimatorClipHandle(default);

        internal Playable Playable { get; }

        public bool IsValid => Playable.IsValid();
    }
}
