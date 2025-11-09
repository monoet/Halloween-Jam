using DG.Tweening;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    /// <summary>
    /// Populates the CombatEventRouter with minimal presets when none are configured.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEventRouterPresetSeeder : MonoBehaviour
    {
        [SerializeField] private CombatEventRouter router;
        [SerializeField] private bool seedTweensIfEmpty = true;
        [SerializeField] private bool seedSfxIfEmpty = true;

        private void Awake()
        {
            router ??= GetComponent<CombatEventRouter>();
            if (router == null)
            {
                Debug.LogWarning("[CombatEventRouterPresetSeeder] Missing CombatEventRouter reference.", this);
                return;
            }

            if (seedTweensIfEmpty && router.TweenPresetCount == 0)
            {
                SeedTweenPresets();
            }

            if (seedSfxIfEmpty && router.SfxPresetCount == 0)
            {
                SeedSfxPresets();
            }
        }

        private void SeedTweenPresets()
        {
            router.EnsureTweenPreset(CombatEventFlags.Windup, BuildWindupPreset());
            router.EnsureTweenPreset(CombatEventFlags.Runup, BuildRunupPreset());
            router.EnsureTweenPreset(CombatEventFlags.Runback, BuildRunbackPreset());
        }

        private void SeedSfxPresets()
        {
            router.EnsureSfxPreset("attack/basic:sword:neutral", BuildSfxPreset("event:/Battle/Impact_Sword", 1f, 0.05f));
            router.EnsureSfxPreset("attack/basic:bow:*", BuildSfxPreset("event:/Battle/Impact_Arrow", 1f, 0.03f));
            router.EnsureSfxPreset("default", BuildSfxPreset("event:/Battle/Impact_Generic", 1f, 0f));
        }

        private static TweenPreset BuildWindupPreset()
        {
            return new TweenPreset
            {
                mode = TweenPresetMode.FrameSequence
            };
        }

        private static TweenPreset BuildRunupPreset()
        {
            return new TweenPreset
            {
                mode = TweenPresetMode.MoveLocal,
                additive = false,
                absolutePosition = new Vector3(2f, -3f, 0f),
                duration = 0.3f,
                ease = Ease.OutCubic
            };
        }

        private static TweenPreset BuildRunbackPreset()
        {
            return new TweenPreset
            {
                mode = TweenPresetMode.RunBackToAnchor,
                duration = 0.25f,
                ease = Ease.InOutSine
            };
        }

        private static SfxPreset BuildSfxPreset(string eventPath, float volume, float pitchVariance)
        {
            return new SfxPreset
            {
                eventPath = eventPath,
                volume = volume,
                pitchVariance = pitchVariance
            };
        }
    }
}
