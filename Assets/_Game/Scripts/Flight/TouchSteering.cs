using Template.Core;
using UnityEngine;

namespace Game.Flight
{
    /// <summary>
    /// One-finger steering anywhere on the screen (GameConfig "Touch steering"). A touch anchors where it goes
    /// down; dragging from the anchor steers: x → bank, y → pitch (drag up = nose up unless InvertPitch).
    /// Releasing eases the controls back to neutral. Touches that start in the top band (HUD buttons) are ignored.
    /// Per-axis dead zone, expo, sensitivity and smoothing.
    /// </summary>
    public sealed class TouchSteering
    {
        private readonly GameConfig _config;
        private Vector2 _smoothed;

        public TouchSteering(GameConfig config) => _config = config;

        public bool Active { get; private set; }
        public Vector2 Anchor { get; private set; }
        /// <summary>Unshaped drag in [-1, 1] per axis.</summary>
        public Vector2 Drag { get; private set; }

        /// <summary>True if a touch starting here belongs to gameplay (not the HUD band at the top of the safe area).</summary>
        public static bool AcceptsTouch(GameConfig config, Vector2 screenPosition)
        {
            var safe = Screen.safeArea;
            return safe.Contains(screenPosition) && screenPosition.y < safe.yMax - safe.height * config.TouchIgnoreTop;
        }

        public void Reset()
        {
            Active = false;
            Drag = Vector2.zero;
            _smoothed = Vector2.zero;
        }

        /// <param name="pressStarted">The finger went down this frame (and may start a drag).</param>
        /// <param name="allowStart">False while something else owns new touches (e.g. the dev panel).</param>
        public FlightControls Update(bool pressed, bool pressStarted, Vector2 pointer, bool allowStart, float deltaTime)
        {
            if (pressStarted && allowStart && AcceptsTouch(_config, pointer))
            {
                Active = true;
                Anchor = pointer;
            }
            if (!pressed) Active = false;

            Drag = Active
                ? Vector2.ClampMagnitude((pointer - Anchor) / (Mathf.Max(1, Screen.height) * _config.DragTravel), 1f)
                : Vector2.zero;

            var bank = Shape(Drag.x, _config.BankDeadZone, _config.BankExpo, _config.BankSensitivity);
            var pitch = Shape(_config.InvertPitch ? -Drag.y : Drag.y, _config.PitchDeadZone, _config.PitchExpo, _config.PitchSensitivity);
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
