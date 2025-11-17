using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.Audio
{
    [CreateAssetMenu(menuName = "Battle/Audio/BattleAudioDatabase", fileName = "BattleAudioDatabase")]
    public sealed class BattleAudioDatabase : ScriptableObject
    {
        [SerializeField] private List<SfxEntry> sfxEntries = new List<SfxEntry>();
        [SerializeField] private MusicConfig musicConfig = new MusicConfig();

        private Dictionary<string, SfxEntry> sfxByFlagId;

        public IReadOnlyDictionary<string, SfxEntry> SfxByFlagId => sfxByFlagId;
        public MusicConfig Music => musicConfig;

        private void OnEnable()
        {
            BuildIndex();
        }

        private void Awake()
        {
            BuildIndex();
        }

        private void BuildIndex()
        {
            sfxByFlagId = new Dictionary<string, SfxEntry>(StringComparer.OrdinalIgnoreCase);

            if (sfxEntries == null)
            {
                return;
            }

            for (int i = 0; i < sfxEntries.Count; i++)
            {
                var entry = sfxEntries[i];
                if (string.IsNullOrWhiteSpace(entry.FlagId))
                {
                    continue;
                }

                // Last entry wins if duplicates exist; keep simple for MVP.
                sfxByFlagId[entry.FlagId] = entry;
            }
        }

        public bool TryGetSfx(string flagId, out SfxEntry entry)
        {
            if (sfxByFlagId == null)
            {
                BuildIndex();
            }

            return sfxByFlagId != null && sfxByFlagId.TryGetValue(flagId, out entry);
        }
    }

    [Serializable]
    public struct SfxEntry
    {
        [Tooltip("Flag id (e.g., attack/impact). Must match BattleAudioFlags or existing content.")]
        public string FlagId;

        [Tooltip("FMOD event path.")]
        public string EventPath;

        [Tooltip("True to attach and spatialize in 3D; false = 2D event.")]
        public bool Use3D;

        [Header("FMOD Parameter Names")]
        public string WeaponParamName;
        public string ElementParamName;
        public string CritParamName;
        public string TargetsParamName;

        [Header("Anti-Spam")]
        [Tooltip("Cooldown in milliseconds for this flag per actor.")]
        public int MinIntervalMs;

        public static SfxEntry CreateDefault(string flagId, string eventPath, bool use3D = false)
        {
            return new SfxEntry
            {
                FlagId = flagId,
                EventPath = eventPath,
                Use3D = use3D,
                WeaponParamName = "weapon",
                ElementParamName = "element",
                CritParamName = "crit",
                TargetsParamName = "targets",
                MinIntervalMs = 30
            };
        }
    }

    [Serializable]
    public struct MusicConfig
    {
        [Tooltip("Snapshot to represent exploration / out-of-combat.")]
        public string ExplorationSnapshot;

        [Tooltip("Snapshot to represent combat.")]
        public string CombatSnapshot;

        [Tooltip("Optional stinger/event when entering combat.")]
        public string TransitionEvent;

        [Tooltip("Fade time in seconds when switching snapshots.")]
        public float FadeTimeSeconds;
    }
}
