using Game.Flight;
using Game.Levels;
using Template.Core;
using Template.Services.Audio;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Sound for a scene: music always; in the Game scene also the throw, the flight wind (louder with speed and in
    /// wind zones), crashes, landings, gems / rings (rising pitch per pickup), the second chance and the result
    /// stings. Listens to the plane, the mission and flow events; no gameplay code knows about audio.
    /// </summary>
    public sealed class PaperAudio : MonoBehaviour
    {
        [SerializeField] private AudioConfig _config;
        [Tooltip("Game scene only (empty in the Menu scene).")]
        [SerializeField] private PaperPlane _plane;
        [SerializeField] private MissionDirector _mission;
        [SerializeField] private AudioSource _windSource;

        private IAudioService _audio;
        private bool _playing;
        private float _windVolume;

        private void Awake()
        {
            ServiceLocator.TryGet(out _audio);
            if (_plane != null)
            {
                _plane.Launched += OnLaunched;
                _plane.Crashed += OnCrashed;
                _plane.Landed += OnLanded;
            }
            if (_mission != null)
            {
                _mission.GemCollected += OnGem;
                _mission.RingPassed += OnRing;
            }
            if (_windSource != null)
            {
                _windSource.clip = _config.Wind;
                _windSource.loop = true;
                _windSource.volume = 0f;
                _windSource.spatialBlend = 0f;
                _windSource.playOnAwake = false;
            }
            EventBus<GameStateChangedEvent>.Subscribe(OnState);
            EventBus<RunContinuedEvent>.Subscribe(OnContinued);
        }

        private void Start() => _audio?.PlayMusic(_config.Music, _config.MusicVolume);

        private void OnDestroy()
        {
            if (_plane != null)
            {
                _plane.Launched -= OnLaunched;
                _plane.Crashed -= OnCrashed;
                _plane.Landed -= OnLanded;
            }
            if (_mission != null)
            {
                _mission.GemCollected -= OnGem;
                _mission.RingPassed -= OnRing;
            }
            EventBus<GameStateChangedEvent>.Unsubscribe(OnState);
            EventBus<RunContinuedEvent>.Unsubscribe(OnContinued);
        }

        private void OnState(GameStateChangedEvent e)
        {
            _playing = e.Current == GameState.Playing;
            if (e.Current == GameState.LevelComplete) Play(_config.LevelComplete, 0.8f);
            else if (e.Current == GameState.GameOver) Play(_config.GameOver, 0.6f);
            else if (e.Current == GameState.Menu || e.Current == GameState.Playing) _audio?.PlayMusic(_config.Music, _config.MusicVolume);
        }

        private void OnLaunched() => Play(_config.Throw, _config.ThrowVolume, 0.06f);

        private void OnCrashed(Collider hit)
        {
            Play(_config.Crash, _config.CrashVolume, 0.08f);
            Play(_config.CrashPaper, _config.CrashVolume * 0.8f, 0.1f);
        }

        private void OnLanded(LandingTarget target, Vector3 point) => Play(_config.Land, 0.8f, 0.05f);
        private void OnContinued(RunContinuedEvent e) => Play(_config.Respawn, 0.7f);
        private void OnGem(int count) => PlayPitched(_config.Gem, 0.7f, 1f + _config.StreakPitch * (count - 1));
        private void OnRing(int count) => PlayPitched(_config.Ring, 0.8f, 1f + _config.StreakPitch * (count - 1));

        private void Play(AudioClip clip, float volume, float variance = 0f) => _audio?.PlaySfx(clip, volume, variance);

        private void PlayPitched(AudioClip clip, float volume, float pitch) => _audio?.PlaySfxAtPitch(clip, volume, Mathf.Min(pitch, 1.6f));

        private void Update()
        {
            if (_windSource == null || _plane == null) return;
            var target = 0f;
            if (_playing && _plane.CurrentPhase == PaperPlane.Phase.Flying && (_audio == null || _audio.SoundEnabled))
            {
                var range = _config.WindSpeedRange;
                var speed = Mathf.Clamp01((_plane.Velocity.magnitude - range.x) / Mathf.Max(0.01f, range.y - range.x));
                target = Mathf.Clamp01(speed * _config.WindMaxVolume + _plane.Wind.magnitude * _config.WindZoneBoost);
            }
            _windVolume = Mathf.MoveTowards(_windVolume, target, Time.unscaledDeltaTime * 1.5f);
            _windSource.volume = _windVolume;
            if (_windVolume > 0.001f && !_windSource.isPlaying) _windSource.Play();
            else if (_windVolume <= 0.001f && _windSource.isPlaying) _windSource.Pause();
        }
    }
}
