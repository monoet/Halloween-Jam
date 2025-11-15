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
        [SerializeField] private Transform overrideTarget;

        private Vector3 initialWorldPos;
        private Vector3 initialLocalPos;
        private bool initialized;

        public void PlayTween(string flagId, CombatEventContext context, TweenPreset preset)
        {
            if (context == null || preset == null)
                return;

            int actorId = context.Actor.Id;
            var handle = TweenGate.For(actorId);

            if (string.Equals(flagId, CombatEventFlags.ActionCancel, System.StringComparison.OrdinalIgnoreCase))
            {
                handle.KillActive(false);
                return;
            }

            var target = ResolveTargetTransform(context, overrideTarget);
            if (target == null)
            {
                if (logWarnings)
                    Debug.LogWarning($"[DOTweenListener] Missing actor transform for '{flagId}'.", this);
                return;
            }

            if (!initialized)
            {
                initialWorldPos = target.position;
                initialLocalPos = target.localPosition;
                initialized = true;
            }

            Tween tween = CreateTween(flagId, target, context, preset);
            if (tween == null)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[DOTweenListener] flag={flagId} preset={preset.mode} target={(target != null ? target.name : "(null)")}", this);
#endif

            tween.SetTarget(target);
            handle.Start(tween);
        }

        private static Transform ResolveTargetTransform(CombatEventContext context, Transform forced)
        {
            if (forced != null)
                return forced;

            if (context.Actor.Root != null)
                return context.Actor.Root;

            return context.Actor.Combatant != null
                ? context.Actor.Combatant.transform
                : null;
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
                        Debug.LogWarning($"[DOTweenListener] Unsupported tween mode '{preset.mode}'.", this);
                    return null;
            }
        }

        private Tween CreateMoveLocalTween(Transform target, TweenPreset preset)
        {
            Vector3 start = target.localPosition;
            Vector3 destination = preset.additive
                ? start + preset.offset
                : initialLocalPos + preset.absolutePosition;

            return target.DOLocalMove(destination, Mathf.Max(0f, preset.duration))
                .SetEase(preset.ease);
        }

        private Tween CreateMoveWorldTween(Transform target, TweenPreset preset)
        {
            Vector3 start = target.position;
            Vector3 destination = preset.additive
                ? start + preset.offset
                : initialWorldPos + preset.absolutePosition;

            return target.DOMove(destination, Mathf.Max(0f, preset.duration))
                .SetEase(preset.ease);
        }

        private Tween CreateRunBackTween(Transform target, CombatEventContext context, TweenPreset preset)
        {
            Transform anchor = context.Actor.Anchor;

            if (anchor != null && anchor.gameObject.activeInHierarchy)
            {
                return target.DOMove(anchor.position, Mathf.Max(0f, preset.duration))
                    .SetEase(preset.ease);
            }

            return target.DOMove(initialWorldPos, Mathf.Max(0f, preset.duration))
                .SetEase(preset.ease);
        }

        private Tween CreateFrameSequence(Transform target, TweenPreset preset)
        {
            if (!preset.RequiresFrameSequence || preset.frameSequence == null)
                return null;

            var seq = preset.frameSequence;
            float FrameTime(int frames) => seq.frameRate <= 0f ? 0f : frames / seq.frameRate;

            Vector3 startPos = target.localPosition;
            Sequence timeline = DOTween.Sequence();

            timeline.Append(target.DOLocalMoveX(startPos.x + seq.forward, FrameTime(6)).SetEase(seq.easeForward));

            timeline.AppendCallback(() =>
                target.localPosition = new Vector3(startPos.x + seq.minorBack, startPos.y, startPos.z));

            timeline.AppendInterval(FrameTime(1));

            timeline.AppendCallback(() =>
                target.localPosition = new Vector3(startPos.x + seq.recoil, startPos.y, startPos.z));

            timeline.AppendInterval(FrameTime(seq.holdFrames));

            if (seq.returnFrames > 0)
            {
                timeline.Append(target.DOLocalMoveX(startPos.x, FrameTime(seq.returnFrames)).SetEase(seq.easeReturn));
            }

            return timeline;
        }
    }
}
