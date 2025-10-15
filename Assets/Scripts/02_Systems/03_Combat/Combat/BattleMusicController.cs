using UnityEngine;

namespace HalloweenJam.Combat
{
    /// <summary>
    /// Lightweight wrapper around the audio source used during battles.
    /// </summary>
    public sealed class BattleMusicController
    {
        private readonly AudioSource source;
        private readonly AudioClip battleClip;
        private readonly AudioClip victoryClip;
        private readonly float defaultVolume;

        public BattleMusicController(AudioSource source, AudioClip battleClip, AudioClip victoryClip, float defaultVolume = 0.55f)
        {
            this.source = source;
            this.battleClip = battleClip;
            this.victoryClip = victoryClip;
            this.defaultVolume = defaultVolume;
        }

        public void PlayBattleMusic()
        {
            if (source == null || battleClip == null)
            {
                return;
            }

            source.clip = battleClip;
            source.loop = true;
            source.volume = defaultVolume;
            source.Play();
        }

        public void PlayVictoryMusic()
        {
            if (source == null)
            {
                return;
            }

            if (victoryClip != null)
            {
                source.loop = false;
                source.clip = victoryClip;
                source.volume = defaultVolume;
                source.Play();
            }
            else
            {
                source.Stop();
            }
        }
    }
}

