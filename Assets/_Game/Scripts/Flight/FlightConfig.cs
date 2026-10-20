using UnityEngine;

namespace Game.Flight
{
    /// <summary>
    /// Arcade paper-plane flight model and launch tuning (SPEC "Flight model"). Forces are accelerations
    /// (mass independent). Every float has a [Range] so DevTuningPanel can draw a slider for it.
    /// Defaults give a ~10:1 glide at ~5 m/s in still air (checked by FlightModelTests).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Configs/Flight Config", fileName = "FlightConfig")]
    public sealed class FlightConfig : ScriptableObject
    {
        [Header("Gravity / lift / drag")]
        [Tooltip("Multiplier on Physics gravity. Lower = floatier, longer flights.")]
        [SerializeField, Range(0.1f, 1.5f)] private float _gravityScale = 0.5f;
        [Tooltip("Lift = LiftCoef * speed² * LiftCurve(AoA). Sets the cruise speed: higher = glides slower.")]
        [SerializeField, Range(0.05f, 1.5f)] private float _liftCoef = 0.4f;
        [Tooltip("Lift is capped at this many 'scaled g' so fast throws fly fast and flat instead of ballooning into the ceiling.")]
        [SerializeField, Range(1f, 3f)] private float _maxLiftG = 1.1f;
        [Tooltip("Top fraction of the lift cap that is rounded off (0 = hard clamp). Below it lift is untouched.")]
        [SerializeField, Range(0.01f, 1f)] private float _liftCapSoftness = 0.1f;
        [Tooltip("Parasitic drag = DragCoef * speed². Main glide-ratio knob (lower = longer glide).")]
        [SerializeField, Range(0f, 0.1f)] private float _dragCoef = 0.016f;
        [Tooltip("Extra drag = InducedDrag * speed² * AoA(rad)². Bleeds speed when pulling up hard.")]
        [SerializeField, Range(0f, 3f)] private float _inducedDragCoef = 0.7f;
        [Tooltip("Lift factor by angle of attack in degrees. Peaks just below StallAngle.")]
        [SerializeField] private AnimationCurve _liftCurve = DefaultLiftCurve();

        [Header("Pitch (angle of attack)")]
        [Tooltip("AoA (deg) with no pitch input. Sets the glide speed together with LiftCoef.")]
        [SerializeField, Range(-5f, 12f)] private float _trimAoa = 4f;
        [Tooltip("AoA added at full nose-up input. Trim + this > StallAngle means over-pulling stalls.")]
        [SerializeField, Range(0f, 20f)] private float _pitchUpAuthority = 12.5f;
        [Tooltip("AoA removed at full nose-down input.")]
        [SerializeField, Range(0f, 20f)] private float _pitchDownAuthority = 9f;
        [Tooltip("How fast the nose follows the target AoA (weathervane stability). Higher = stiffer, less paper-like.")]
        [SerializeField, Range(0.5f, 15f)] private float _pitchResponse = 4f;

        [Header("Bank / turn")]
        [SerializeField, Range(5f, 80f)] private float _maxBankAngle = 40f;
        [Tooltip("How fast bank follows input (1/s).")]
        [SerializeField, Range(0.5f, 15f)] private float _bankResponse = 5f;
        [Tooltip("Yaw rate (deg/s) per degree of bank. 1.8 → 72 deg/s at 40 deg bank.")]
        [SerializeField, Range(0f, 5f)] private float _turnRate = 1.8f;
        [Tooltip("0 = banking keeps full lift, 1 = lift * cos(bank) (sinks in turns).")]
        [SerializeField, Range(0f, 1f)] private float _bankLiftLoss = 0.3f;

        [Header("Stall")]
        [Tooltip("Below this speed (m/s) the plane stalls.")]
        [SerializeField, Range(0f, 6f)] private float _stallSpeed = 2.2f;
        [Tooltip("Above this AoA (deg) the plane stalls.")]
        [SerializeField, Range(5f, 40f)] private float _stallAngle = 15f;
        [Tooltip("Stall ends when speed exceeds StallSpeed * this.")]
        [SerializeField, Range(1f, 3f)] private float _stallRecoverySpeedFactor = 1.4f;
        [Tooltip("AoA (deg) the nose drops to while stalled (before pitch-down input).")]
        [SerializeField, Range(-20f, 5f)] private float _stallNoseAoa = -2f;
        [Tooltip("Lift multiplier while stalled.")]
        [SerializeField, Range(0f, 1f)] private float _stallLiftMultiplier = 0.3f;
        [Tooltip("Roll/pitch wobble amplitude (deg) while stalled: the gentle tumble.")]
        [SerializeField, Range(0f, 45f)] private float _stallWobble = 12f;

        [Header("Flutter")]
        [Tooltip("Perlin noise on AoA/bank (deg) in still air. Wind turbulence adds to it later.")]
        [SerializeField, Range(0f, 5f)] private float _flutterAmplitude = 0.6f;
        [SerializeField, Range(0.1f, 10f)] private float _flutterFrequency = 2.5f;

        [Header("Launch (flick)")]
        [SerializeField, Range(1f, 10f)] private float _minLaunchSpeed = 5f;
        [SerializeField, Range(2f, 15f)] private float _maxLaunchSpeed = 8.5f;
        [Tooltip("Flick speed (screen heights / s) that maps to MinLaunchSpeed. Anything slower, and taps, launch at min.")]
        [SerializeField, Range(0f, 5f)] private float _flickSpeedForMin = 0.8f;
        [Tooltip("Flick speed (screen heights / s) that maps to MaxLaunchSpeed.")]
        [SerializeField, Range(0.5f, 10f)] private float _flickSpeedForMax = 4f;
        [Tooltip("Only the last part of the swipe counts for speed (seconds).")]
        [SerializeField, Range(0.03f, 0.5f)] private float _flickWindow = 0.1f;
        [Tooltip("Swipes shorter than this (screen heights) launch straight ahead at DefaultLaunchPitch.")]
        [SerializeField, Range(0f, 0.2f)] private float _minAimSwipe = 0.04f;
        [Tooltip("Launch pitch (deg) for a tap or a very short swipe.")]
        [SerializeField, Range(-10f, 30f)] private float _defaultLaunchPitch = 5f;
        [SerializeField, Range(-30f, 10f)] private float _minLaunchPitch = -5f;
        [Tooltip("Max launch pitch (deg) for the weakest throw.")]
        [SerializeField, Range(0f, 45f)] private float _maxLaunchPitch = 12f;
        [Tooltip("Max launch pitch (deg) for the strongest throw: the cone narrows with power so hard throws stay below the ceiling.")]
        [SerializeField, Range(-10f, 45f)] private float _maxLaunchPitchAtFullPower = 5f;
        [Tooltip("Max launch yaw (deg) either side: keeps the throw in a forward cone.")]
        [SerializeField, Range(0f, 80f)] private float _maxLaunchYaw = 40f;
        [Tooltip("Swipe direction is projected this far (screen heights) from the plane to find the aim point on screen.")]
        [SerializeField, Range(0.05f, 1f)] private float _aimScreenDistance = 0.3f;
        [Tooltip("Depth (m) along the camera ray of the aim point.")]
        [SerializeField, Range(1f, 30f)] private float _aimDepth = 10f;
        [Tooltip("Touches must start within this distance (screen heights) of the plane to start a flick.")]
        [SerializeField, Range(0.05f, 1f)] private float _grabRadius = 0.3f;
        [Tooltip("Collisions with the launch pad are ignored for this long after release (s).")]
        [SerializeField, Range(0f, 1f)] private float _launchGraceTime = 0.25f;

        public float GravityScale => _gravityScale;
        public float LiftCoef => _liftCoef;
        public float MaxLiftG => _maxLiftG;
        public float LiftCapSoftness => _liftCapSoftness;
        public float DragCoef => _dragCoef;
        public float InducedDragCoef => _inducedDragCoef;
        public AnimationCurve LiftCurve => _liftCurve;
        public float TrimAoa => _trimAoa;
        public float PitchUpAuthority => _pitchUpAuthority;
        public float PitchDownAuthority => _pitchDownAuthority;
        public float PitchResponse => _pitchResponse;
        public float MaxBankAngle => _maxBankAngle;
        public float BankResponse => _bankResponse;
        public float TurnRate => _turnRate;
        public float BankLiftLoss => _bankLiftLoss;
        public float StallSpeed => _stallSpeed;
        public float StallAngle => _stallAngle;
        public float StallRecoverySpeedFactor => _stallRecoverySpeedFactor;
        public float StallNoseAoa => _stallNoseAoa;
        public float StallLiftMultiplier => _stallLiftMultiplier;
        public float StallWobble => _stallWobble;
        public float FlutterAmplitude => _flutterAmplitude;
        public float FlutterFrequency => _flutterFrequency;
        public float MinLaunchSpeed => _minLaunchSpeed;
        public float MaxLaunchSpeed => Mathf.Max(_minLaunchSpeed, _maxLaunchSpeed);
        public float FlickSpeedForMin => _flickSpeedForMin;
        public float FlickSpeedForMax => Mathf.Max(_flickSpeedForMin + 0.01f, _flickSpeedForMax);
        public float FlickWindow => _flickWindow;
        public float MinAimSwipe => _minAimSwipe;
        public float DefaultLaunchPitch => _defaultLaunchPitch;
        public float MinLaunchPitch => _minLaunchPitch;
        public float MaxLaunchPitch => Mathf.Max(_minLaunchPitch, _maxLaunchPitch);
        public float MaxLaunchPitchAtFullPower => Mathf.Max(_minLaunchPitch, _maxLaunchPitchAtFullPower);
        public float MaxLaunchYaw => _maxLaunchYaw;
        public float AimScreenDistance => _aimScreenDistance;
        public float AimDepth => _aimDepth;
        public float GrabRadius => _grabRadius;
        public float LaunchGraceTime => _launchGraceTime;

        /// <summary>Piecewise-linear default (no overshoot around the stall peak).</summary>
        public static AnimationCurve DefaultLiftCurve()
        {
            var keys = new[]
            {
                new Keyframe(-20f, -0.6f),
                new Keyframe(-5f, -0.15f),
                new Keyframe(0f, 0.15f),
                new Keyframe(10f, 1f),
                new Keyframe(13f, 1f),
                new Keyframe(16f, 0.55f),
                new Keyframe(25f, 0.3f),
                new Keyframe(45f, 0.15f),
                new Keyframe(90f, 0f),
            };
            for (var i = 0; i < keys.Length; i++)
            {
                if (i > 0) keys[i].inTangent = Slope(keys[i - 1], keys[i]);
                if (i < keys.Length - 1) keys[i].outTangent = Slope(keys[i], keys[i + 1]);
            }
            return new AnimationCurve(keys)
            {
                preWrapMode = WrapMode.ClampForever,
                postWrapMode = WrapMode.ClampForever,
            };
        }

        private static float Slope(Keyframe a, Keyframe b) => (b.value - a.value) / (b.time - a.time);
    }
}
