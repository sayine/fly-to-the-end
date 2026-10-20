using UnityEngine;

namespace Game
{
    /// <summary>
    /// Gameplay tuning outside the flight model (CLAUDE.md rule 2): tilt steering, chase camera, run rules.
    /// The "placeholder game" sections still drive Demo_Tilt and the template Menu scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Template/Configs/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Ball (placeholder game)")]
        [SerializeField, Min(0)] private float _ballAcceleration = 25f;
        [SerializeField, Min(0.1f)] private float _ballMaxSpeed = 8f;
        [SerializeField, Min(0)] private float _ballDrag = 0.5f;

        [Header("Rules")]
        [Tooltip("Below this height the ball is lost (lose condition).")]
        [SerializeField] private float _fallHeight = -3f;
        [SerializeField, Min(0)] private float _scorePerSecond = 1f;
        [SerializeField, Min(1)] private float _platformSize = 8f;

        [Header("Camera (placeholder game)")]
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, 12f, -7f);
        [SerializeField, Min(0.1f)] private float _cameraFollowSharpness = 6f;

        [Header("Tilt steering (per axis, applied after InputConfig)")]
        [Tooltip("Bank input multiplier (tilt left/right).")]
        [SerializeField, Range(0.1f, 3f)] private float _bankSensitivity = 1f;
        [Tooltip("Pitch input multiplier (tilt forward/back).")]
        [SerializeField, Range(0.1f, 3f)] private float _pitchSensitivity = 1f;
        [SerializeField, Range(0f, 0.5f)] private float _bankDeadZone = 0.04f;
        [SerializeField, Range(0f, 0.5f)] private float _pitchDeadZone = 0.08f;
        [Tooltip("Response curve: 1 = linear, >1 = finer control near neutral.")]
        [SerializeField, Range(1f, 3f)] private float _bankExpo = 1.3f;
        [SerializeField, Range(1f, 3f)] private float _pitchExpo = 1.5f;
        [Tooltip("Extra low-pass per axis (1/s). Higher = more responsive.")]
        [SerializeField, Range(1f, 40f)] private float _bankSmoothing = 14f;
        [SerializeField, Range(1f, 40f)] private float _pitchSmoothing = 10f;
        [Tooltip("Off: tilting the top edge away = nose down (yoke). On: away = nose up.")]
        [SerializeField] private bool _invertPitch;

        [Header("Chase camera")]
        [SerializeField, Range(0.3f, 6f)] private float _chaseDistance = 1.6f;
        [SerializeField, Range(-1f, 3f)] private float _chaseHeight = 0.55f;
        [Tooltip("The camera looks this far ahead of the plane along its heading (m).")]
        [SerializeField, Range(0f, 5f)] private float _chaseLookAhead = 1.5f;
        [SerializeField, Range(0.5f, 30f)] private float _chasePositionSharpness = 6f;
        [SerializeField, Range(0.5f, 30f)] private float _chaseRotationSharpness = 8f;
        [Tooltip("How fast the camera swings behind a turning plane.")]
        [SerializeField, Range(0.5f, 20f)] private float _chaseHeadingSharpness = 3f;
        [Tooltip("Camera roll as a fraction of plane bank.")]
        [SerializeField, Range(0f, 1f)] private float _chaseRollFactor = 0.25f;
        [SerializeField, Range(30f, 90f)] private float _chaseFovMin = 60f;
        [SerializeField, Range(30f, 110f)] private float _chaseFovMax = 72f;
        [Tooltip("Speeds (m/s) mapped to FovMin..FovMax.")]
        [SerializeField, Range(0f, 10f)] private float _chaseFovSpeedMin = 4f;
        [SerializeField, Range(1f, 20f)] private float _chaseFovSpeedMax = 9f;
        [Tooltip("Camera keeps this far (m) from walls, floor and ceiling.")]
        [SerializeField, Range(0f, 1f)] private float _chaseWallMargin = 0.25f;

        [Header("Run")]
        [Tooltip("Time between the crash and Game Over (crash tumble plays out).")]
        [SerializeField, Range(0f, 3f)] private float _crashSettleTime = 0.6f;
        [Tooltip("Below this height (m) the plane counts as crashed (safety net if it leaves the room).")]
        [SerializeField, Range(-10f, 0f)] private float _outOfBoundsHeight = -1f;

        public float BallAcceleration => _ballAcceleration;
        public float BallMaxSpeed => _ballMaxSpeed;
        public float BallDrag => _ballDrag;
        public float FallHeight => _fallHeight;
        public float ScorePerSecond => _scorePerSecond;
        public float PlatformSize => _platformSize;
        public Vector3 CameraOffset => _cameraOffset;
        public float CameraFollowSharpness => _cameraFollowSharpness;

        public float BankSensitivity => _bankSensitivity;
        public float PitchSensitivity => _pitchSensitivity;
        public float BankDeadZone => _bankDeadZone;
        public float PitchDeadZone => _pitchDeadZone;
        public float BankExpo => _bankExpo;
        public float PitchExpo => _pitchExpo;
        public float BankSmoothing => _bankSmoothing;
        public float PitchSmoothing => _pitchSmoothing;
        public bool InvertPitch => _invertPitch;

        public float ChaseDistance => _chaseDistance;
        public float ChaseHeight => _chaseHeight;
        public float ChaseLookAhead => _chaseLookAhead;
        public float ChasePositionSharpness => _chasePositionSharpness;
        public float ChaseRotationSharpness => _chaseRotationSharpness;
        public float ChaseHeadingSharpness => _chaseHeadingSharpness;
        public float ChaseRollFactor => _chaseRollFactor;
        public float ChaseFovMin => _chaseFovMin;
        public float ChaseFovMax => _chaseFovMax;
        public float ChaseFovSpeedMin => _chaseFovSpeedMin;
        public float ChaseFovSpeedMax => Mathf.Max(_chaseFovSpeedMin + 0.1f, _chaseFovSpeedMax);
        public float ChaseWallMargin => _chaseWallMargin;

        public float CrashSettleTime => _crashSettleTime;
        public float OutOfBoundsHeight => _outOfBoundsHeight;
    }
}
