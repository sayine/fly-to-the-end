using Template.Services.Save;
using UnityEngine;

namespace Template.Services.Audio
{
    /// <summary>Real audio service: round-robin pool of 2D AudioSources + one music source.</summary>
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        private const int SfxVoices = 8;

        private AudioSource[] _sfx;
        private AudioSource _music;
        private int _next;
        private ISaveService _save;

        public static AudioService Create(ISaveService save)
        {
            var go = new GameObject("[AudioService]");
            DontDestroyOnLoad(go);
            var service = go.AddComponent<AudioService>();
            service.Init(save);
            return service;
        }

        private void Init(ISaveService save)
        {
            _save = save;
            _sfx = new AudioSource[SfxVoices];
            for (var i = 0; i < SfxVoices; i++) _sfx[i] = CreateSource(false);
            _music = CreateSource(true);
            Apply();
        }

        public bool SoundEnabled
        {
            get => _save == null || _save.Data.SoundEnabled;
            set
            {
                if (_save == null) return;
                _save.Data.SoundEnabled = value;
                _save.Save();
                Apply();
            }
        }

        public void PlaySfx(AudioClip clip, float volume = 1f, float pitchVariance = 0f)
        {
            if (clip == null || !SoundEnabled) return;
            var source = _sfx[_next];
            _next = (_next + 1) % SfxVoices;
            source.pitch = pitchVariance > 0f ? 1f + Random.Range(-pitchVariance, pitchVariance) : 1f;
            source.PlayOneShot(clip, volume);
        }

        public void PlaySfxAtPitch(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || !SoundEnabled) return;
            var source = _sfx[_next];
            _next = (_next + 1) % SfxVoices;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        public void PlayMusic(AudioClip clip, float volume = 0.6f)
        {
            if (clip == null) return;
            if (_music.clip == clip && _music.isPlaying) return;
            _music.clip = clip;
            _music.volume = volume;
            _music.Play();
        }

        public void StopMusic() => _music.Stop();

        private void Apply()
        {
            var muted = !SoundEnabled;
            for (var i = 0; i < _sfx.Length; i++) _sfx[i].mute = muted;
            _music.mute = muted;
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }
    }

    /// <summary>Mock audio service: logs, plays nothing. For tests and headless runs.</summary>
    public sealed class MockAudioService : IAudioService
    {
        public bool SoundEnabled { get; set; } = true;
        public void PlaySfx(AudioClip clip, float volume = 1f, float pitchVariance = 0f) => Debug.Log($"[Audio:Mock] sfx {(clip ? clip.name : "null")}");
        public void PlaySfxAtPitch(AudioClip clip, float volume, float pitch) => Debug.Log($"[Audio:Mock] sfx {(clip ? clip.name : "null")} pitch {pitch:0.00}");
        public void PlayMusic(AudioClip clip, float volume = 0.6f) => Debug.Log($"[Audio:Mock] music {(clip ? clip.name : "null")}");
        public void StopMusic() => Debug.Log("[Audio:Mock] stop music");
    }
}
