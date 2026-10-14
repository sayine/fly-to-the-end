using UnityEngine;

namespace Template.Services.Audio
{
    public interface IAudioService
    {
        /// <summary>Single "Sound" toggle (SFX + music), persisted in PlayerData.</summary>
        bool SoundEnabled { get; set; }

        void PlaySfx(AudioClip clip, float volume = 1f, float pitchVariance = 0f);
        void PlayMusic(AudioClip clip, float volume = 0.6f);
        void StopMusic();
    }
}
