using System.Text;
using Game.Flight;
using Game.Levels;
using Game.Planes;
using Game.UI;
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
    public sealed class PaperPlaneGame : MonoBehaviour, IDevTelemetry, ILevelAnalyticsOwner
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
        [Tooltip("The 20 planes; the selected one (or a rewarded trial) is flown each run.")]
        [SerializeField] private PlaneCatalog _planes;
        [SerializeField] private UnlockToast _unlockToast;

        private const float ArrowMinLength = 0.5f;
        private const float ArrowMaxLength = 1.4f;

        private readonly FlickGesture _flick = new FlickGesture();
        private FlightConfig[] _variants; // per-plane FlightConfig copies, created on first use

        // Second chance: recent in-flight poses to respawn from (ring buffer, sampled every SafeInterval).
        private const int SafeSamples = 20;
        private const float SafeInterval = 0.2f;
        private const int RespawnSamplesBack = 6;      // ~1.2 s before the crash
        private const float InvulnerableTime = 2f;
        private const float RespawnClearance = 0.5f;
        private readonly Vector3[] _safePosition = new Vector3[SafeSamples];
        private readonly Vector3[] _safeVelocity = new Vector3[SafeSamples];
        private readonly Collider[] _overlap = new Collider[8];
        private int _safeHead;
        private int _safeCount;
        private float _safeTimer;

        [Tooltip("Free Flight \"Boost throw\" reward: throw speed multiplier for one run (SPEC §5).")]
        [SerializeField, Range(1f, 2f)] private float _boostThrow = 1.2f;
        private bool _boosted;
        private bool _continued;
        private float _runTime;        // since the run started (aiming included), for analytics
        private string _crashCause = "";
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
            ServiceLocator.Register<ILevelAnalyticsOwner>(this); // we log level_start / level_end with full parameters
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
            EventBus<BoostThrowGrantedEvent>.Subscribe(OnBoostThrow);
        }

        private void OnDisable()
        {
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            EventBus<RunContinuedEvent>.Unsubscribe(OnRunContinued);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);
            EventBus<LevelAdvanceEvent>.Unsubscribe(OnLevelAdvance);
            EventBus<BoostThrowGrantedEvent>.Unsubscribe(OnBoostThrow);
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet(out ILevelAnalyticsOwner owner) && ReferenceEquals(owner, this))
                ServiceLocator.Unregister<ILevelAnalyticsOwner>();
            if (_variants != null)
                foreach (var v in _variants)
                    if (v != null) Destroy(v);
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
            _runTime = 0f;
            _boosted = false;
            _continued = false;
            _crashCause = "";
            _safeCount = 0;
            if (_mission != null) _launchPad = _mission.Prepare();
            ApplyPlane();
            ParkPlane();
            if (IsLevelRun)
                PaperAnalytics.LevelStart(_mission.LevelIndex + 1, _mission.Level, _planes != null ? PlaneCollection.Get(_planes).ForRun : null);
            EventBus<ScoreChangedEvent>.Raise(new ScoreChangedEvent { Score = 0 });
        }

        private bool IsLevelRun => _mission != null && !_mission.IsFreeFlight;

        /// <summary>
        /// Rewarded "Second chance" (SPEC §5): back in the air where the plane was ~1 s before the crash (the most recent
        /// clear spot), briefly invulnerable; the mission goes on with what was achieved. Falls back to the pad.
        /// </summary>
        private void OnRunContinued(RunContinuedEvent e)
        {
            _continued = true;
            _mission?.Resume();
            if (!TryRespawn()) ParkPlane();
        }

        private bool TryRespawn()
        {
            for (var back = Mathf.Min(RespawnSamplesBack, _safeCount - 1); back < _safeCount; back++)
            {
                var i = (_safeHead - 1 - back + SafeSamples * 2) % SafeSamples;
                var position = _safePosition[i];
                if (Blocked(position)) continue;
                var direction = _safeVelocity[i].sqrMagnitude > 0.01f ? _safeVelocity[i].normalized : _launchPoint.forward;
                direction.y = Mathf.Clamp(direction.y, -0.15f, 0.1f); // level-ish, so there is time to steer
                direction.Normalize();
                _plane.Respawn(position, direction * _plane.Config.CruiseSpeed, InvulnerableTime);
                _phase = RunPhase.Flying;
                _won = false;
                _settleTimer = 0f;
                _lastPosition = position;
                _steering.Reset();
                _wasPressed = _input != null && _input.IsPressed;
                SetHint(TouchHint.None);
                if (_chaseCamera != null) _chaseCamera.Snap(direction);
                RaiseAimState();
                return true;
            }
            return false;
        }

        private bool Blocked(Vector3 position)
        {
            var count = Physics.OverlapSphereNonAlloc(position, RespawnClearance, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
                if (_overlap[i].attachedRigidbody != _plane.Body) return true;
            return false;
        }

        private void RecordSafePoint(float dt)
        {
            _safeTimer += dt;
            if (_safeTimer < SafeInterval || _plane.IsInvulnerable) return;
            _safeTimer = 0f;
            _safePosition[_safeHead] = _plane.Body.position;
            _safeVelocity[_safeHead] = _plane.Velocity;
            _safeHead = (_safeHead + 1) % SafeSamples;
            if (_safeCount < SafeSamples) _safeCount++;
        }

        private void OnBoostThrow(BoostThrowGrantedEvent e)
        {
            if (!_playing || _phase != RunPhase.Aiming) return;
            _boosted = true;
            RaiseAimState();
        }

        private void RaiseAimState() => EventBus<AimStateEvent>.Raise(new AimStateEvent
        {
            Aiming = _playing && _phase == RunPhase.Aiming,
            FreeFlight = _mission == null || _mission.IsFreeFlight,
            Boosted = _boosted,
        });

        private void OnLevelAdvance(LevelAdvanceEvent e)
        {
            if (_mission != null) GameProgress.Get(_mission.Catalog).Advance();
        }

        private void OnStateChanged(GameStateChangedEvent e)
        {
            _playing = e.Current == GameState.Playing;
            if (e.Previous == GameState.Paused && e.Current == GameState.Menu && IsLevelRun)
                PaperAnalytics.LevelEnd(_mission.LevelIndex + 1, "quit", 0, _runTime, "", _continued);
            RaiseAimState();
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
            _runTime += dt;
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
            RecordSafePoint(dt);
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

        /// <summary>Flies the selected plane (or this run's trial): its stats on top of the base FlightConfig, its look.</summary>
        private void ApplyPlane()
        {
            if (_planes == null || _planes.Count == 0) return;
            var collection = PlaneCollection.Get(_planes);
            var plane = collection.ForRun;
            var index = _planes.IndexOf(plane.Id);
            _variants ??= new FlightConfig[_planes.Count];
            if (_variants[index] == null) _variants[index] = _flightConfig.CreateVariant(plane.Stats, _planes.Scaling);
            _plane.SetPlane(_variants[index], plane.Mesh, plane.Material);
        }

        // ---------- Launch / crash ----------
        private LaunchAim.Result Solve() =>
            LaunchAim.Solve(_flick.Drag, _flick.Speed(_plane.Config.FlickWindow), _camera, _plane.transform.position,
                _launchPoint.forward, _plane.Config);

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
            if (_boosted) velocity *= _boostThrow;
            _plane.Launch(velocity, _launchPad);
            _safeCount = 0;
            _safeTimer = 0f;
            _lastPosition = _plane.transform.position;
            _phase = RunPhase.Flying;
            _mission?.OnLaunched();
            _wasPressed = _input != null && _input.IsPressed; // the launching finger can't start a steer
            _steerHintTime = 0f;
            SetHint(_tutorial ? TouchHint.Steer : TouchHint.None);
            _haptics?.Play(HapticType.Light);
            RaiseAimState();
            return true;
        }

        private void OnCrashed(Collider hit)
        {
            if (_phase != RunPhase.Flying) return; // also ignores crashes while celebrating a win
            _crashCause = hit != null ? hit.name : "out_of_bounds";
            _mission?.FlightEnded();
            BeginSettle(_mission != null && _mission.Tracker.Outcome == MissionOutcome.Success, 0f);
            _plane.Controls = default;
            _haptics?.Play(HapticType.Medium);
            Debug.Log($"[Plane] crash into {(hit != null ? hit.name : "out of bounds")} air={_airTime:0.0}s dist={_distance:0.0}m");
        }

        private void OnLanded(LandingTarget target, Vector3 point)
        {
            if (_phase != RunPhase.Flying) return;
            _crashCause = "landed_off_target";
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
            if (_planes != null) PlaneCollection.Get(_planes).ClearTrial(); // a rewarded trial lasts one run
            var levelRun = IsLevelRun;
            if (_won && levelRun)
            {
                var stars = _mission.Tracker.Stars;
                var newBest = GameProgress.Get(_mission.Catalog).Record(_mission.LevelIndex, stars, _scoreTenths);
                Debug.Log($"[Level] {_mission.LevelIndex + 1} won: {stars} stars, {_airTime:0.0}s");
                PaperAnalytics.LevelEnd(_mission.LevelIndex + 1, "win", stars, _runTime, "", _continued);
                EventBus<LevelCompletedEvent>.Raise(new LevelCompletedEvent { Stars = stars, Score = _scoreTenths, IsNewBest = newBest });
                if (_planes != null)
                    foreach (var index in PlaneUnlocks.Evaluate(_planes, _mission.Catalog))
                        if (_unlockToast != null) _unlockToast.Show(_planes.Get(index));
            }
            else
            {
                if (levelRun) PaperAnalytics.LevelEnd(_mission.LevelIndex + 1, "lose", 0, _runTime, _crashCause, _continued);
                else PaperAnalytics.FreeFlightEnd(_airTime);
                // A failed level's airtime is not the Free Flight best.
                EventBus<PlayerDiedEvent>.Raise(new PlayerDiedEvent { NotForBest = levelRun });
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
            RaiseAimState();
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
