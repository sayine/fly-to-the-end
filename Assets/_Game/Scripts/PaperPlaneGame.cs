using System.Text;
using Game.Flight;
using Game.Levels;
using Template.Core;
using Template.Services.Analytics;
using Template.Services.Haptics;
using Template.Services.Input;
using Template.Services.Save;
using Template.UI.DevTools;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Paper Flight core loop: plane on the shelf → swipe up anywhere to throw → drag anywhere to steer
    /// → any contact crashes → short settle → PlayerDiedEvent. Score = seconds airborne, raised in tenths
    /// (ScoreChangedEvent is an int; the HUD shows it with scale 0.1). First-time players get a ghost-finger
    /// hint for the throw and for steering; it completes (TutorialSeen, tutorial_complete) once they steer.
    /// Talks to the flow only through events, like the template's placeholder game.
    /// </summary>
    public sealed class PaperPlaneGame : MonoBehaviour, IDevTelemetry
    {
        private enum RunPhase
        {
            Idle,     // not Playing (menu, game over, paused)
            Aiming,   // plane on the pad, waiting for a flick
            Flying,
            Settling, // flight over (crash or landing), result shown after a short beat
        }

        [SerializeField] private GameConfig _gameConfig;
        [SerializeField] private FlightConfig _flightConfig;
        [SerializeField] private PaperPlane _plane;
        [SerializeField] private Transform _launchPoint;
        [SerializeField] private Collider _launchPad;
        [SerializeField] private ChaseCamera _chaseCamera;
        [SerializeField] private Camera _camera;
        [Tooltip("Faint direction preview while dragging (SPEC: no full trajectory).")]
        [SerializeField] private LineRenderer _aimArrow;
        [Tooltip("Level setup and mission tracking (Free Flight when no level is selected).")]
        [SerializeField] private MissionDirector _mission;
        [Tooltip("First-time ghost-finger hint (the whole screen is the controller).")]
        [SerializeField] private TouchHintView _hintView;

        private const float ArrowMinLength = 0.5f;
        private const float ArrowMaxLength = 1.4f;

        private readonly FlickGesture _flick = new FlickGesture();
        private IPlayerInput _input;
        private IHapticService _haptics;
        private ISaveService _save;
        private IAnalyticsService _analytics;
        private TouchSteering _steering;
        private RunPhase _phase;
        private bool _playing;
        private float _airTime;      // accumulated over the run (kept across a rewarded continue)
        private int _scoreTenths;
        private float _distance;
        private float _settleTimer;
        private bool _won;            // the run ends as a won level (LevelCompletedEvent), not Game Over

        private const float WinCelebrateTime = 1f; // keep flying this long after a mid-air win
        private Vector3 _lastPosition;
        private bool _wasPressed;
        private bool _tutorial;       // first-time hints active until the player has thrown and steered
        private float _aimIdle;       // seconds on the shelf without touching the screen
        private float _steerHintTime; // seconds of this flight with the steer hint up
        private float _steeredTime;   // seconds the player actually steered during the tutorial

        private const float SteerHintMaxTime = 8f;
        private const float SteerToComplete = 0.6f;
        private const float SteerDragThreshold = 0.25f;

        private void Awake()
        {
            ServiceLocator.TryGet(out _input);
            ServiceLocator.TryGet(out _haptics);
            ServiceLocator.TryGet(out _save);
            ServiceLocator.TryGet(out _analytics);
            _tutorial = _save == null || !_save.Data.TutorialSeen;
            _steering = new TouchSteering(_gameConfig);
            _plane.Crashed += OnCrashed;
            _plane.Landed += OnLanded;
            SetArrow(false);
            ParkPlane();
        }

        private void OnEnable()
        {
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
            EventBus<RunContinuedEvent>.Subscribe(OnRunContinued);
            EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
            EventBus<LevelAdvanceEvent>.Subscribe(OnLevelAdvance);
        }

        private void OnDisable()
        {
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            EventBus<RunContinuedEvent>.Unsubscribe(OnRunContinued);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);
            EventBus<LevelAdvanceEvent>.Unsubscribe(OnLevelAdvance);
        }

        private void OnDestroy()
        {
            if (_plane == null) return;
            _plane.Crashed -= OnCrashed;
            _plane.Landed -= OnLanded;
        }

        // ---------- Flow events ----------
        private void OnRunStarted(RunStartedEvent e)
        {
            _airTime = 0f;
            _scoreTenths = 0;
            _distance = 0f;
            if (_mission != null) _launchPad = _mission.Prepare();
            ParkPlane();
            EventBus<ScoreChangedEvent>.Raise(new ScoreChangedEvent { Score = 0 });
        }

        /// <summary>Stand-in for "Second chance" (Phase 4): relaunch from the pad, airborne time is kept.</summary>
        private void OnRunContinued(RunContinuedEvent e) => ParkPlane();

        private void OnLevelAdvance(LevelAdvanceEvent e)
        {
            if (_mission != null) GameProgress.Get(_mission.Catalog).Advance();
        }

        private void OnStateChanged(GameStateChangedEvent e)
        {
            _playing = e.Current == GameState.Playing;
            if (!_playing)
            {
                _flick.Cancel();
                SetArrow(false);
                _steering.Reset();
                SetHint(TouchHint.None);
            }
        }

        // ---------- Loop ----------
        private void Update()
        {
            if (!_playing) return;
            var dt = Time.deltaTime;
            switch (_phase)
            {
                case RunPhase.Aiming:
                    UpdateAiming();
                    break;
                case RunPhase.Flying:
                    UpdateFlying(dt);
                    break;
                case RunPhase.Settling:
                    _settleTimer += dt;
                    if (_settleTimer >= _gameConfig.CrashSettleTime) FinishRun();
                    break;
            }
        }

        private void UpdateAiming()
        {
            if (_input == null) return;
            var pressed = _input.IsPressed;
            var position = _input.PointerPosition;
            var now = Time.unscaledTime;

            if (pressed && !_wasPressed && !DevTuningPanel.IsOpen && TouchSteering.AcceptsTouch(_gameConfig, position)) _flick.Begin(position, now);
            else if (_flick.Active) _flick.Add(position, now); // includes the release frame's position

            _aimIdle = _flick.Active ? 0f : _aimIdle + Time.deltaTime;
            SetHint(!_flick.Active && (_tutorial || _aimIdle > _gameConfig.IdleHintDelay) ? TouchHint.Launch : TouchHint.None);

            if (_flick.Active)
            {
                var aim = Solve();
                if (pressed) ShowArrow(aim);
                else Launch(aim);
            }
            _wasPressed = pressed;
        }

        private void UpdateFlying(float dt)
        {
            if (_input != null)
            {
                var pressed = _input.IsPressed;
                _plane.Controls = _steering.Update(pressed, pressed && !_wasPressed, _input.PointerPosition, !DevTuningPanel.IsOpen, dt);
                _wasPressed = pressed;
            }
            UpdateSteerTutorial(dt);

            _airTime += dt;
            var tenths = Mathf.FloorToInt(_airTime * 10f);
            if (tenths != _scoreTenths)
            {
                _scoreTenths = tenths;
                EventBus<ScoreChangedEvent>.Raise(new ScoreChangedEvent { Score = _scoreTenths });
            }

            var p = _plane.transform.position;
            _distance += (p - _lastPosition).Flat().magnitude;
            _lastPosition = p;
            if (p.y < _gameConfig.OutOfBoundsHeight) _plane.Crash(null);

            if (_mission != null && _phase == RunPhase.Flying)
            {
                _mission.Tick(dt);
                // Mid-air win (all gems, last ring, reached the room): enjoy a moment of flight, then the result.
                if (_mission.Tracker.Outcome == MissionOutcome.Success) BeginSettle(true, _gameConfig.CrashSettleTime - WinCelebrateTime);
            }
        }

        // ---------- Launch / crash ----------
        private LaunchAim.Result Solve() =>
            LaunchAim.Solve(_flick.Drag, _flick.Speed(_flightConfig.FlickWindow), _camera, _plane.transform.position,
                _launchPoint.forward, _flightConfig);

        private void Launch(LaunchAim.Result aim)
        {
            if (TryLaunch(aim.Velocity))
                Debug.Log($"[Plane] launch speed={aim.Speed:0.00} pitch={aim.Pitch:0.0} yaw={aim.Yaw:0.0} power={aim.Power:0.00}");
        }

        /// <summary>Launches from the pad with an explicit velocity (flick, tests, later the hint demo). Only while aiming.</summary>
        public bool TryLaunch(Vector3 velocity)
        {
            if (!_playing || _phase != RunPhase.Aiming) return false;
            _flick.Cancel();
            SetArrow(false);
            _input?.Calibrate(); // no-op with touch input; re-centres tilt if InputConfig is switched back to Tilt
            _steering.Reset();
            _plane.Launch(velocity, _launchPad);
            _lastPosition = _plane.transform.position;
            _phase = RunPhase.Flying;
            _mission?.OnLaunched();
            _wasPressed = _input != null && _input.IsPressed; // the launching finger can't start a steer
            _steerHintTime = 0f;
            SetHint(_tutorial ? TouchHint.Steer : TouchHint.None);
            _haptics?.Play(HapticType.Light);
            return true;
        }

        private void OnCrashed(Collider hit)
        {
            if (_phase != RunPhase.Flying) return; // also ignores crashes while celebrating a win
            _mission?.FlightEnded();
            BeginSettle(_mission != null && _mission.Tracker.Outcome == MissionOutcome.Success, 0f);
            _plane.Controls = default;
            _haptics?.Play(HapticType.Medium);
            Debug.Log($"[Plane] crash into {(hit != null ? hit.name : "out of bounds")} air={_airTime:0.0}s dist={_distance:0.0}m");
        }

        private void OnLanded(LandingTarget target, Vector3 point)
        {
            if (_phase != RunPhase.Flying) return;
            _mission?.Landed(target, point);
            BeginSettle(_mission != null && _mission.Tracker.Outcome == MissionOutcome.Success, 0f);
            _plane.Controls = default;
            _haptics?.Play(HapticType.Light);
        }

        /// <summary>Flight is over (or won mid-air): the result follows after a short beat.</summary>
        private void BeginSettle(bool won, float startTimer)
        {
            _phase = RunPhase.Settling;
            _won = won;
            _settleTimer = startTimer;
            SetHint(TouchHint.None);
        }

        private void FinishRun()
        {
            _phase = RunPhase.Idle;
            var levelRun = _mission != null && !_mission.IsFreeFlight;
            if (_won && levelRun)
            {
                var stars = _mission.Tracker.Stars;
                var newBest = GameProgress.Get(_mission.Catalog).Record(_mission.LevelIndex, stars, _scoreTenths);
                Debug.Log($"[Level] {_mission.LevelIndex + 1} won: {stars} stars, {_airTime:0.0}s");
                EventBus<LevelCompletedEvent>.Raise(new LevelCompletedEvent { Stars = stars, Score = _scoreTenths, IsNewBest = newBest });
            }
            else
            {
                EventBus<PlayerDiedEvent>.Raise(default);
            }
        }

        private void ParkPlane()
        {
            _phase = RunPhase.Aiming;
            _won = false;
            _flick.Cancel();
            _wasPressed = _input != null && _input.IsPressed; // a held finger from the previous screen can't launch
            SetArrow(false);
            _aimIdle = 0f;
            _plane.PlaceAt(_launchPoint.position, _launchPoint.rotation);
            _steering.Reset();
            if (_chaseCamera != null) _chaseCamera.Snap(_launchPoint.forward);
        }

        // ---------- First-time hints ----------
        /// <summary>Steer hint for the first flights; completes the tutorial once the player has really steered.</summary>
        private void UpdateSteerTutorial(float dt)
        {
            if (!_tutorial) return;
            _steerHintTime += dt;
            if (_steering.Active && _steering.Drag.magnitude > SteerDragThreshold) _steeredTime += dt;
            if (_steeredTime >= SteerToComplete)
            {
                _tutorial = false;
                SetHint(TouchHint.None);
                if (_save != null)
                {
                    _save.Data.TutorialSeen = true;
                    _save.Save();
                }
                _analytics?.TutorialComplete();
                return;
            }
            SetHint(_steerHintTime < SteerHintMaxTime ? TouchHint.Steer : TouchHint.None);
        }

        private void SetHint(TouchHint hint)
        {
            if (_hintView != null) _hintView.SetHint(hint);
        }

        // ---------- Aim arrow ----------
        private void ShowArrow(LaunchAim.Result aim)
        {
            if (_aimArrow == null) return;
            SetArrow(true);
            var start = _plane.transform.position;
            var length = Mathf.Lerp(ArrowMinLength, ArrowMaxLength, aim.Power);
            _aimArrow.SetPosition(0, start);
            _aimArrow.SetPosition(1, start + aim.Velocity.normalized * length);
        }

        private void SetArrow(bool visible)
        {
            if (_aimArrow != null && _aimArrow.enabled != visible) _aimArrow.enabled = visible;
        }

        // ---------- Dev telemetry ----------
        public void WriteTelemetry(StringBuilder sb)
        {
            var s = _plane.State;
            sb.Append(_phase.ToString()).Append("  t ").Append(_airTime.ToString("0.0"))
              .Append("s  dist ").Append(_distance.ToString("0.0")).Append("m  alt ")
              .Append(_plane.transform.position.y.ToString("0.00")).Append("m\n");
            if (_phase == RunPhase.Flying)
            {
                sb.Append("speed ").Append(s.Speed.ToString("0.00")).Append("  AoA ").Append(s.EffectiveAoa.ToString("0.0"))
                  .Append("  bank ").Append(s.EffectiveBank.ToString("0"))
                  .Append(s.Stalled ? "  STALL" : "");
                var w = _plane.Wind;
                if (w.sqrMagnitude > 0.01f)
                    sb.Append("  wind ").Append(w.x.ToString("0.0")).Append(',').Append(w.y.ToString("0.0")).Append(',').Append(w.z.ToString("0.0"));
                sb.Append('\n');
            }
            if (_input != null)
            {
                var c = _plane.Controls;
                sb.Append("drag ").Append(_steering.Drag.x.ToString("0.00")).Append(',').Append(_steering.Drag.y.ToString("0.00"))
                  .Append("  ctrl bank ").Append(c.Bank.ToString("0.00")).Append(" pitch ").Append(c.Pitch.ToString("0.00"));
            }
        }
    }
}
