using UnityEngine;

namespace Template.Services.Audio
{
    public interface IAudioService
    {
        /// <summary>Single "Sound" toggle (SFX + music), persisted in PlayerData.</summary>
        bool SoundEnabled { get; set; }

        void PlaySfx(AudioClip clip, float volume = 1f, float pitchVariance = 0f);

        /// <summary>One-shot at an exact pitch (e.g. rising notes for a streak).</summary>
        void PlaySfxAtPitch(AudioClip clip, float volume, float pitch);
        void PlayMusic(AudioClip clip, float volume = 0.6f);
        void StopMusic();
    }
}
