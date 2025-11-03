using System;
using System.Collections.Generic;
using System.Threading;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.Core;
using UnityEngine;
using System.Threading.Tasks;

namespace BattleV2.Orchestration.Runtime
{
    /// <summary>
    /// Abstraction used by the orchestration layer to trigger per-combatant animations
    /// regardless of the underlying rendering strategy (Animator, sprite flipbook, transforms, etc.).
    /// </summary>
    public interface IAnimationWrapper : IAnimationBindingResolver
    {
        Task PlayAsync(AnimationPlaybackRequest request, CancellationToken cancellationToken = default);
        void Stop();
        void OnAnimationEvent(AnimationEventPayload payload);
    }

    /// <summary>
    /// Immutable request describing which animation to play on an <see cref="IAnimationWrapper"/>.
    /// Supports Animator clips, sprite flipbooks and simple transform tweens.
    /// </summary>
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

        public static AnimationPlaybackRequest ForAnimatorClip(
            AnimationClip clip,
            float speed = 1f,
            float normalizedStartTime = 0f,
            bool loop = false)
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

        public static AnimationPlaybackRequest ForSpriteFlipbook(
            IReadOnlyList<Sprite> frames,
            float frameRate = 12f,
            bool loop = true)
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

    /// <summary>
    /// Simple data structure describing a tween to apply to a Transform during an animation.
    /// </summary>
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

        /// <summary>Duration in seconds.</summary>
        public float Duration;
        public Vector3? TargetLocalPosition;
        public Quaternion? TargetLocalRotation;
        public Vector3? TargetLocalScale;
        public AnimationCurve Easing;

        public bool IsValid =>
            Duration > 0f &&
            (TargetLocalPosition.HasValue || TargetLocalRotation.HasValue || TargetLocalScale.HasValue);
    }

    /// <summary>
    /// Lightweight identifier used by the animator registry to associate wrappers with combatants.
    /// </summary>
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
