using UnityEngine;

namespace Game
{
    /// <summary>All gameplay tuning for the current game (CLAUDE.md rule 2). Replace fields per game.</summary>
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

        [Header("Camera")]
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, 12f, -7f);
        [SerializeField, Min(0.1f)] private float _cameraFollowSharpness = 6f;

        public float BallAcceleration => _ballAcceleration;
        public float BallMaxSpeed => _ballMaxSpeed;
        public float BallDrag => _ballDrag;
        public float FallHeight => _fallHeight;
        public float ScorePerSecond => _scorePerSecond;
        public float PlatformSize => _platformSize;
        public Vector3 CameraOffset => _cameraOffset;
        public float CameraFollowSharpness => _cameraFollowSharpness;
    }
}
