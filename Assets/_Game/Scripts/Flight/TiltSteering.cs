using Template.Core;
using UnityEngine;

namespace Game.Flight
{
    /// <summary>
    /// IPlayerInput.Axis (already calibrated, smoothed and dead-zoned by TiltInput) → FlightControls, with the
    /// per-axis dead zone, expo, sensitivity and smoothing from GameConfig (SPEC "Phase 2 — Flight").
    /// Axis.x tilt right → bank right. Axis.y top edge away → nose down (unless InvertPitch).
    /// </summary>
    public sealed class TiltSteering
    {
        private readonly GameConfig _config;
        private Vector2 _smoothed;

        public TiltSteering(GameConfig config) => _config = config;

        public void Reset() => _smoothed = Vector2.zero;

        public FlightControls Update(Vector2 axis, float deltaTime)
        {
            var bank = Shape(axis.x, _config.BankDeadZone, _config.BankExpo, _config.BankSensitivity);
            var pitch = Shape(_config.InvertPitch ? axis.y : -axis.y, _config.PitchDeadZone, _config.PitchExpo, _config.PitchSensitivity);
            _smoothed.x = Mathf.Lerp(_smoothed.x, bank, MathExtensions.SmoothingFactor(_config.BankSmoothing, deltaTime));
            _smoothed.y = Mathf.Lerp(_smoothed.y, pitch, MathExtensions.SmoothingFactor(_config.PitchSmoothing, deltaTime));
            return new FlightControls { Bank = _smoothed.x, Pitch = _smoothed.y };
        }

        /// <summary>Dead zone (rescaled), then expo, then sensitivity, clamped to [-1, 1].</summary>
        public static float Shape(float value, float deadZone, float expo, float sensitivity)
        {
            var v = Mathf.Clamp(value, -1f, 1f).ApplyDeadZone(deadZone);
            v = Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), expo);
            return Mathf.Clamp(v * sensitivity, -1f, 1f);
        }
    }
}
