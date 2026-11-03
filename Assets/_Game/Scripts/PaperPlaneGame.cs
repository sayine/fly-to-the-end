using System.Text;
using Game.Flight;
using Template.Core;
using Template.Services.Haptics;
using Template.Services.Input;
using Template.UI.DevTools;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Paper Flight core loop: plane on the shelf → flick to launch → one-finger pad to steer → any contact
    /// crashes → short settle → PlayerDiedEvent. Score = seconds airborne, raised in tenths
    /// (ScoreChangedEvent is an int; the HUD shows it with scale 0.1).
    /// Talks to the flow only through events, like the template's placeholder game.
    /// </summary>
    public sealed class PaperPlaneGame : MonoBehaviour, IDevTelemetry
    {
        private enum RunPhase
        {
            Idle,     // not Playing (menu, game over, paused)
            Aiming,   // plane on the pad, waiting for a flick
            Flying,
            Settling, // crashed, tumble playing out
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
        [Tooltip("Bottom steering pad visuals (shown while flying).")]
        [SerializeField] private ControlPadView _padView;

        private const float ArrowMinLength = 0.5f;
        private const float ArrowMaxLength = 1.4f;

        private readonly FlickGesture _flick = new FlickGesture();
        private IPlayerInput _input;
        private IHapticService _haptics;
        private PadSteering _steering;
        private RunPhase _phase;
        private bool _playing;
        private float _airTime;      // accumulated over the run (kept across a rewarded continue)
        private int _scoreTenths;
        private float _distance;
        private float _settleTimer;
        private Vector3 _lastPosition;
        private bool _wasPressed;

        private void Awake()
        {
            ServiceLocator.TryGet(out _input);
            ServiceLocator.TryGet(out _haptics);
            _steering = new PadSteering(_gameConfig);
            _plane.Crashed += OnCrashed;
            SetArrow(false);
            ParkPlane();
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

        private void OnDestroy()
        {
            if (_plane != null) _plane.Crashed -= OnCrashed;
        }

        // ---------- Flow events ----------
        private void OnRunStarted(RunStartedEvent e)
        {
            _airTime = 0f;
            _scoreTenths = 0;
            _distance = 0f;
            ParkPlane();
            EventBus<ScoreChangedEvent>.Raise(new ScoreChangedEvent { Score = 0 });
        }

        /// <summary>Day 1 stand-in for "Second chance": relaunch from the pad, airborne time is kept.</summary>
        private void OnRunContinued(RunContinuedEvent e) => ParkPlane();

        private void OnStateChanged(GameStateChangedEvent e)
        {
            _playing = e.Current == GameState.Playing;
            if (!_playing)
            {
                _flick.Cancel();
                SetArrow(false);
                _steering.Reset();
            }
            if (_padView != null) _padView.SetVisible(_playing && _phase == RunPhase.Flying);
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
                    if (_settleTimer >= _gameConfig.CrashSettleTime)
                    {
                        _phase = RunPhase.Idle;
                        EventBus<PlayerDiedEvent>.Raise(default);
                    }
                    break;
            }
        }

        private void UpdateAiming()
        {
            if (_input == null) return;
            var pressed = _input.IsPressed;
            var position = _input.PointerPosition;
            var now = Time.unscaledTime;

            if (pressed && !_wasPressed && !DevTuningPanel.IsOpen && IsNearPlane(position)) _flick.Begin(position, now);
            else if (_flick.Active) _flick.Add(position, now); // includes the release frame's position

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
                if (_padView != null)
                    _padView.Refresh(PadSteering.PadRect(_gameConfig, Screen.safeArea), _steering.Active, _steering.Anchor,
                        _steering.Drag, _gameConfig.PadTravel * Screen.height);
            }

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
            _wasPressed = _input != null && _input.IsPressed; // the launching finger can't start a steer
            SetPad(true);
            _haptics?.Play(HapticType.Light);
            return true;
        }

        private void OnCrashed(Collider hit)
        {
            if (_phase != RunPhase.Flying) return;
            _phase = RunPhase.Settling;
            _settleTimer = 0f;
            SetPad(false);
            _plane.Controls = default;
            _haptics?.Play(HapticType.Medium);
            Debug.Log($"[Plane] crash into {(hit != null ? hit.name : "out of bounds")} air={_airTime:0.0}s dist={_distance:0.0}m");
        }

        private void ParkPlane()
        {
            _phase = RunPhase.Aiming;
            _flick.Cancel();
            _wasPressed = _input != null && _input.IsPressed; // a held finger from the previous screen can't launch
            SetArrow(false);
            SetPad(false);
            _plane.PlaceAt(_launchPoint.position, _launchPoint.rotation);
            _steering.Reset();
            if (_chaseCamera != null) _chaseCamera.Snap(_launchPoint.forward);
        }

        private bool IsNearPlane(Vector2 screenPosition)
        {
            if (_camera == null) return true;
            var planeScreen = (Vector2)_camera.WorldToScreenPoint(_plane.transform.position);
            return (screenPosition - planeScreen).magnitude / Mathf.Max(1, Screen.height) <= _flightConfig.GrabRadius;
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

        private void SetPad(bool visible)
        {
            if (_padView != null) _padView.SetVisible(visible);
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
                sb.Append("pad ").Append(_steering.Drag.x.ToString("0.00")).Append(',').Append(_steering.Drag.y.ToString("0.00"))
                  .Append("  ctrl bank ").Append(c.Bank.ToString("0.00")).Append(" pitch ").Append(c.Pitch.ToString("0.00"));
            }
        }
    }
}
