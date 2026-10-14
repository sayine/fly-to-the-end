using Template.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Template.Services.Input
{
    /// <summary>
    /// Tilt control from the gravity sensor (preferred, already filtered by the OS) or the accelerometer.
    /// Pipeline: raw gravity → calibration rotation (neutral = how the player held the phone at Calibrate)
    /// → angle-normalized axis → invert → sensitivity → radial dead zone → low-pass smoothing.
    /// Axis.x: tilt right = +1. Axis.y: top edge away from the player = +1.
    /// Editor fallback: WASD/arrow keys (smoothed), pointer state via mouse.
    /// </summary>
    public sealed class TiltInput : IPlayerInput
    {
        private readonly InputConfig _config;
        private readonly PointerTracker _pointer;
        private Quaternion _calibration = Quaternion.identity;
        private bool _calibrationPending;
        private Vector2 _smoothed;
        private bool _enabled = true;
        private InputDevice _sensor;

        public TiltInput(InputConfig config)
        {
            _config = config;
            _pointer = new PointerTracker(config);
            _calibrationPending = config.CalibrateOnStart;
            EnableSensor();
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (!value)
                {
                    _pointer.Reset();
                    _smoothed = Vector2.zero;
                    Axis = Vector2.zero;
                }
            }
        }

        public Vector2 Axis { get; private set; }
        public bool IsPressed => _enabled && _pointer.IsPressed;
        public bool Tapped => _enabled && _pointer.Tapped;
        public Vector2 PointerPosition => _pointer.Position;
        public Vector2 DragDelta => _enabled ? _pointer.DragDelta : Vector2.zero;

        /// <summary>True when a real tilt sensor drives Axis (false in the Editor → keyboard).</summary>
        public bool HasSensor => _sensor != null && _sensor.enabled;

        /// <summary>Raw gravity in device space (g units), for debugging.</summary>
        public Vector3 RawGravity { get; private set; }

        public void Calibrate()
        {
            _calibrationPending = true;
            _smoothed = Vector2.zero;
        }

        public void Tick(float deltaTime)
        {
            if (!_enabled) return;
            _pointer.Tick();

            Vector2 target;
            float sharpness;
            if (TryReadGravity(out var gravity))
            {
                RawGravity = gravity;
                if (_calibrationPending)
                {
                    _calibration = TiltMath.Calibrate(gravity);
                    _calibrationPending = false;
                }
                target = TiltToAxis(gravity);
                sharpness = _config.TiltSmoothing;
            }
            else
            {
                target = KeyboardAxis.Read();
                sharpness = _config.KeyboardSmoothing;
            }

            _smoothed = Vector2.Lerp(_smoothed, target, MathExtensions.SmoothingFactor(sharpness, deltaTime));
            Axis = _smoothed.sqrMagnitude < 1e-5f ? Vector2.zero : _smoothed;
        }

        private Vector2 TiltToAxis(Vector3 gravity) => TiltMath.ToAxis(_calibration, gravity, _config.MaxTiltAngle,
            _config.TiltSensitivity, _config.TiltDeadZone, _config.InvertX, _config.InvertY);

        private bool TryReadGravity(out Vector3 gravity)
        {
            gravity = Vector3.zero;
            if (_sensor == null || !_sensor.enabled) return false;

            if (_sensor is GravitySensor gs) gravity = gs.gravity.ReadValue();
            else if (_sensor is Accelerometer acc) gravity = acc.acceleration.ReadValue();

            // Sensors report zero for the first frames after enabling; wait for a real sample.
            return gravity.sqrMagnitude > 0.25f;
        }

        private void EnableSensor()
        {
            if (GravitySensor.current != null)
            {
                InputSystem.EnableDevice(GravitySensor.current);
                _sensor = GravitySensor.current;
            }
            else if (Accelerometer.current != null)
            {
                InputSystem.EnableDevice(Accelerometer.current);
                _sensor = Accelerometer.current;
            }
            else
            {
                _sensor = null;
                if (!Application.isEditor) Debug.LogWarning("[Input] No gravity sensor/accelerometer: tilt falls back to keyboard.");
            }
        }
    }
}
