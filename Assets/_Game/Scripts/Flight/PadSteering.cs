using Template.Core;
using UnityEngine;

namespace Game.Flight
{
    /// <summary>
    /// One-finger steering pad along the bottom of the screen (GameConfig "Touch pad steering").
    /// A touch that starts inside the pad anchors there; dragging from the anchor steers: x → bank, y → pitch
    /// (drag up = nose up unless InvertPitch). The finger may leave the pad while held. Releasing eases the
    /// controls back to neutral. Per-axis dead zone, expo, sensitivity and smoothing.
    /// </summary>
    public sealed class PadSteering
    {
        private readonly GameConfig _config;
        private Vector2 _smoothed;

        public PadSteering(GameConfig config) => _config = config;

        public bool Active { get; private set; }
        public Vector2 Anchor { get; private set; }
        public Vector2 Finger { get; private set; }
        /// <summary>Unshaped drag in [-1, 1] per axis (for the pad's knob).</summary>
        public Vector2 Drag { get; private set; }

        /// <summary>The pad in screen pixels, inside the safe area.</summary>
        public static Rect PadRect(GameConfig config, Rect safeArea)
        {
            var x = safeArea.x + safeArea.width * config.PadSideMargin;
            var y = safeArea.y + safeArea.height * config.PadBottom;
            return new Rect(x, y, safeArea.width * (1f - 2f * config.PadSideMargin), safeArea.height * config.PadHeight);
        }

        public void Reset()
        {
            Active = false;
            Drag = Vector2.zero;
            _smoothed = Vector2.zero;
        }

        /// <param name="pressStarted">The finger went down this frame (and may start a drag if inside the pad).</param>
        /// <param name="allowStart">False while something else owns new touches (e.g. the dev panel).</param>
        public FlightControls Update(bool pressed, bool pressStarted, Vector2 pointer, bool allowStart, float deltaTime)
        {
            if (pressStarted && allowStart && PadRect(_config, Screen.safeArea).Contains(pointer))
            {
                Active = true;
                Anchor = pointer;
            }
            if (!pressed) Active = false;

            Finger = pointer;
            Drag = Active
                ? Vector2.ClampMagnitude((pointer - Anchor) / (Mathf.Max(1, Screen.height) * _config.PadTravel), 1f)
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
