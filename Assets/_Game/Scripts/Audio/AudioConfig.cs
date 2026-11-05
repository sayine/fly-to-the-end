using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Every game sound and its mix level. Clips are assigned by AudioBuilder (Kenney CC0 effects + synthesized
    /// whoosh, wind and music); tune volumes here.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Configs/Audio", fileName = "AudioConfig")]
    public sealed class AudioConfig : ScriptableObject
    {
        [Header("Music")]
        [SerializeField] private AudioClip _music;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.4f;

        [Header("Flight")]
        [SerializeField] private AudioClip _throw;
        [SerializeField, Range(0f, 1f)] private float _throwVolume = 0.7f;
        [Tooltip("Looping air noise while flying, louder with speed and in wind zones.")]
        [SerializeField] private AudioClip _wind;
        [SerializeField, Range(0f, 1f)] private float _windMaxVolume = 0.35f;
        [Tooltip("Speed (m/s) for silent → full wind.")]
        [SerializeField] private Vector2 _windSpeedRange = new Vector2(2f, 9f);
        [Tooltip("Extra wind volume per m/s² of wind-zone push.")]
        [SerializeField, Min(0f)] private float _windZoneBoost = 0.04f;
        [SerializeField] private AudioClip _crash;
        [SerializeField] private AudioClip _crashPaper;
        [SerializeField, Range(0f, 1f)] private float _crashVolume = 0.8f;
        [SerializeField] private AudioClip _land;
        [SerializeField] private AudioClip _respawn;

        [Header("Missions")]
        [SerializeField] private AudioClip _gem;
        [SerializeField] private AudioClip _ring;
        [Tooltip("Each gem / ring in a run plays a little higher (pitch step).")]
        [SerializeField, Range(0f, 0.1f)] private float _streakPitch = 0.04f;

        [Header("UI")]
        [SerializeField] private AudioClip _click;
        [SerializeField] private AudioClip _star;
        [SerializeField] private AudioClip _levelComplete;
        [SerializeField] private AudioClip _gameOver;
        [SerializeField] private AudioClip _unlock;
        [SerializeField] private AudioClip _checkIn;

        public AudioClip Music => _music;
        public float MusicVolume => _musicVolume;
        public AudioClip Throw => _throw;
        public float ThrowVolume => _throwVolume;
        public AudioClip Wind => _wind;
        public float WindMaxVolume => _windMaxVolume;
        public Vector2 WindSpeedRange => _windSpeedRange;
        public float WindZoneBoost => _windZoneBoost;
        public AudioClip Crash => _crash;
        public AudioClip CrashPaper => _crashPaper;
        public float CrashVolume => _crashVolume;
        public AudioClip Land => _land;
        public AudioClip Respawn => _respawn;
        public AudioClip Gem => _gem;
        public AudioClip Ring => _ring;
        public float StreakPitch => _streakPitch;
        public AudioClip Click => _click;
        public AudioClip Star => _star;
        public AudioClip LevelComplete => _levelComplete;
        public AudioClip GameOver => _gameOver;
        public AudioClip Unlock => _unlock;
        public AudioClip CheckIn => _checkIn;
    }
}
