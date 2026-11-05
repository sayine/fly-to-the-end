using UnityEngine;

namespace Game.Planes
{
    /// <summary>
    /// How a plane's 1-5 stats change the flight model (rule 2: tuning in config). Each stat is turned into
    /// n = (stat - 3) / 2 in [-1, 1]; "per point" values below are the change at n = ±1 (stat 5 / stat 1).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Configs/Plane Stat Scaling", fileName = "PlaneStatScaling")]
    public sealed class PlaneStatScaling : ScriptableObject
    {
        [Header("Speed")]
        [Tooltip("Cruise speed ×(1 ± this). Lift is rescaled so level flight stays level at the new speed.")]
        [SerializeField, Range(0f, 0.5f)] private float _cruiseSpeed = 0.2f;
        [Tooltip("Throw speed (min and max) ×(1 ± this).")]
        [SerializeField, Range(0f, 0.5f)] private float _launchSpeed = 0.15f;

        [Header("Agility")]
        [Tooltip("Turn rate, bank and pitch response ×(1 ± this).")]
        [SerializeField, Range(0f, 0.8f)] private float _turn = 0.3f;
        [Tooltip("Max bank angle ± this many degrees.")]
        [SerializeField, Range(0f, 20f)] private float _maxBank = 8f;

        [Header("Glide")]
        [Tooltip("Neutral sink rate ×(1 ∓ this): high Glide sinks slower.")]
        [SerializeField, Range(0f, 0.9f)] private float _sink = 0.6f;

        [Header("Lift")]
        [Tooltip("Nose-up authority ×(1 ± this).")]
        [SerializeField, Range(0f, 0.6f)] private float _pitchUp = 0.25f;
        [Tooltip("Stall speed ×(1 ∓ this): high Lift stalls later.")]
        [SerializeField, Range(0f, 0.5f)] private float _stallSpeed = 0.15f;

        [Header("Stability")]
        [Tooltip("Wind push ×(1 ∓ this): high Stability is pushed less.")]
        [SerializeField, Range(0f, 0.8f)] private float _wind = 0.35f;
        [Tooltip("Flutter ×(1 ∓ this).")]
        [SerializeField, Range(0f, 0.9f)] private float _flutter = 0.5f;

        public float CruiseSpeed => _cruiseSpeed;
        public float LaunchSpeed => _launchSpeed;
        public float Turn => _turn;
        public float MaxBank => _maxBank;
        public float Sink => _sink;
        public float PitchUp => _pitchUp;
        public float StallSpeed => _stallSpeed;
        public float Wind => _wind;
        public float Flutter => _flutter;

        /// <summary>Stat 1..5 → -1..1.</summary>
        public static float Normalize(int stat) => Mathf.Clamp((stat - 3) / 2f, -1f, 1f);
    }
}
