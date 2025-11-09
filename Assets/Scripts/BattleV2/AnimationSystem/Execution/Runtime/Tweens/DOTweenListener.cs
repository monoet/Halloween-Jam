using BattleV2.AnimationSystem.Execution.Runtime.Tweens;
using DG.Tweening;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    [DisallowMultipleComponent]
    public sealed class DOTweenListener : MonoBehaviour, ICombatEventTweenListener
    {
        [Header("Options")]
        [SerializeField] private bool logWarnings = true;

        public void PlayTween(string flagId, CombatEventContext context, TweenPreset preset)
        {
            if (context == null)
            {
                return;
            }

            int actorId = context.Actor.Id;
            var handle = TweenGate.For(actorId);

            if (string.Equals(flagId, CombatEventFlags.ActionCancel, System.StringComparison.OrdinalIgnoreCase))
            {
                handle.KillActive(false);
                return;
            }

            if (preset == null)
            {
                return;
            }

            var target = ResolveTargetTransform(context);
            if (target == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning($"[DOTweenListener] Missing actor transform for '{flagId}'.", this);
                }
                return;
            }

            Tween tween = CreateTween(flagId, target, context, preset);
            if (tween == null)
            {
                return;
            }

            tween.SetTarget(target);
            handle.Start(tween);
        }

        private static Transform ResolveTargetTransform(CombatEventContext context)
        {
            return context.Actor.Root != null
                ? context.Actor.Root
                : context.Actor.Combatant != null ? context.Actor.Combatant.transform : null;
        }

        private Tween CreateTween(string flagId, Transform target, CombatEventContext context, TweenPreset preset)
        {
            switch (preset.mode)
            {
                case TweenPresetMode.MoveLocal:
                    return CreateMoveLocalTween(target, preset);

                case TweenPresetMode.MoveWorld:
                    return CreateMoveWorldTween(target, preset);

                case TweenPresetMode.RunBackToAnchor:
                    return CreateRunBackTween(target, context, preset);

                case TweenPresetMode.FrameSequence:
                    return CreateFrameSequence(target, preset);

                default:
                    if (logWarnings)
                    {
                        Debug.LogWarning($"[DOTweenListener] Unsupported tween mode '{preset.mode}' for flag '{flagId}'.", this);
                    }
                    return null;
            }
        }

        private static Tween CreateMoveLocalTween(Transform target, TweenPreset preset)
        {
            Vector3 start = target.localPosition;
            Vector3 destination = preset.additive
                ? start + preset.offset
                : preset.absolutePosition;

            return target.DOLocalMove(destination, Mathf.Max(0f, preset.duration))
                .SetEase(preset.ease);
        }

        private static Tween CreateMoveWorldTween(Transform target, TweenPreset preset)
        {
            Vector3 start = target.position;
            Vector3 destination = preset.additive
                ? start + preset.offset
                : preset.absolutePosition;

            return target.DOMove(destination, Mathf.Max(0f, preset.duration))
                .SetEase(preset.ease);
        }

        private Tween CreateRunBackTween(Transform target, CombatEventContext context, TweenPreset preset)
        {
            var anchor = context.Actor.Anchor != null ? context.Actor.Anchor : context.Actor.Root;
            if (anchor == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[DOTweenListener] RunBackToAnchor requested but actor anchor is missing.", this);
                }
                return null;
            }

            return target.DOMove(anchor.position, Mathf.Max(0f, preset.duration))
                .SetEase(preset.ease);
        }

        private Tween CreateFrameSequence(Transform target, TweenPreset preset)
        {
            if (!preset.RequiresFrameSequence || preset.frameSequence == null)
            {
                return null;
            }

            var seq = preset.frameSequence;
            float FrameTime(int frames) => seq.frameRate <= 0f ? 0f : frames / seq.frameRate;

            Vector3 startPos = target.localPosition;
            Sequence timeline = DOTween.Sequence();

            timeline.Append(target.DOLocalMoveX(startPos.x + seq.forward, FrameTime(6)).SetEase(seq.easeForward));
            timeline.AppendInterval(FrameTime(0)); // synchronise immediate callbacks
            timeline.AppendCallback(() =>
                target.localPosition = new Vector3(startPos.x + seq.minorBack, startPos.y, startPos.z));
            timeline.AppendInterval(FrameTime(1));
            timeline.AppendCallback(() =>
                target.localPosition = new Vector3(startPos.x + seq.recoil, startPos.y, startPos.z));
            timeline.AppendInterval(FrameTime(seq.holdFrames));
            timeline.Append(target.DOLocalMoveX(startPos.x, FrameTime(seq.returnFrames)).SetEase(seq.easeReturn));

            return timeline;
        }
    }
}
