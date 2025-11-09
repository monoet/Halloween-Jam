using System;
using DG.Tweening;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    public enum TweenPresetMode
    {
        MoveLocal = 0,
        MoveWorld = 1,
        RunBackToAnchor = 2,
        FrameSequence = 3
    }

    [Serializable]
    public sealed class TweenPreset
    {
        [TextArea]
        public string description;

        public TweenPresetMode mode = TweenPresetMode.MoveLocal;
        public Vector3 offset = new Vector3(2f, -3f, 0f);
        public Vector3 absolutePosition = new Vector3(2f, -3f, 0f);
        public float duration = 0.3f;
        public Ease ease = Ease.OutCubic;
        public bool additive = true;
        public FrameSequencePreset frameSequence = new FrameSequencePreset();

        public bool RequiresFrameSequence => mode == TweenPresetMode.FrameSequence;
    }

    [Serializable]
    public sealed class FrameSequencePreset
    {
        public float frameRate = 60f;
        public float forward = 0.10f;
        public float minorBack = 0.135f;
        public float recoil = -0.177f;
        public int holdFrames = 3;
        public int returnFrames = 4;
        public Ease easeForward = Ease.OutCubic;
        public Ease easeReturn = Ease.InOutSine;
    }

    [Serializable]
    public sealed class SfxPreset
    {
        public string eventPath = "event:/Battle/Impact_Generic";
        public float volume = 1f;
        public float pitchVariance = 0f;
    }
}
