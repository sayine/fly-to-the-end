using Template.Core;
using Template.Services.Haptics;
using Template.Services.Input;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Placeholder core loop so the template is playable end-to-end: keep the ball on the platform.
    /// Score = seconds survived. Falling off = PlayerDiedEvent. Replace with the real game's mechanic.
    /// Talks to the flow only through events (RunStarted/RunContinued/GameStateChanged in, Score/PlayerDied out).
    /// </summary>
    public sealed class PlaceholderGame : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private TiltBall _ball;

        private IPlayerInput _input;
        private IHapticService _haptics;
        private bool _active;
        private float _time;
        private int _score;

        private void Awake()
        {
            ServiceLocator.TryGet(out _input);
            ServiceLocator.TryGet(out _haptics);
            if (_ball != null) _ball.SetFrozen(true);
        }

        private void OnEnable()
        {
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
            EventBus<RunContinuedEvent>.Subscribe(OnRunContinued);
            EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
        }

        private void OnDisable()
        {
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            EventBus<RunContinuedEvent>.Unsubscribe(OnRunContinued);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);
        }

        private void Update()
        {
            if (!_active || _ball == null) return;

            _ball.Axis = _input != null ? _input.Axis : Vector2.zero;

            _time += Time.deltaTime;
            var score = Mathf.FloorToInt(_time * _config.ScorePerSecond);
            if (score != _score)
            {
                _score = score;
                EventBus<ScoreChangedEvent>.Raise(new ScoreChangedEvent { Score = _score });
            }

            if (_ball.transform.position.y < _config.FallHeight)
            {
                _active = false;
                _ball.SetFrozen(true);
                _haptics?.Play(HapticType.Heavy);
                EventBus<PlayerDiedEvent>.Raise(default);
            }
        }

        private void OnRunStarted(RunStartedEvent e)
        {
            _time = 0f;
            _score = 0;
            _ball.ResetToStart();
        }

        private void OnRunContinued(RunContinuedEvent e)
        {
            _ball.ResetToStart(); // revive in the middle, keep time/score
            _input?.Calibrate();
        }

        private void OnStateChanged(GameStateChangedEvent e)
        {
            _active = e.Current == GameState.Playing;
            if (_ball == null) return;
            if (e.Current == GameState.Playing) _ball.SetFrozen(false);
            else if (e.Current == GameState.GameOver) _ball.SetFrozen(true);
        }
    }
}
