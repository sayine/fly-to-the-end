using Template.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Template.Services.Input
{
    /// <summary>
    /// Single-finger drag/tap. Axis is a floating virtual joystick centered where the finger went down.
    /// Editor fallback: mouse (via PointerTracker) plus WASD/arrow keys for Axis.
    /// </summary>
    public sealed class TouchInput : IPlayerInput
    {
        private readonly InputConfig _config;
        private readonly PointerTracker _pointer;
        private Vector2 _keyboardAxis;
        private bool _enabled = true;

        public TouchInput(InputConfig config)
        {
            _config = config;
            _pointer = new PointerTracker(config);
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
                    Axis = Vector2.zero;
                }
            }
        }

        public Vector2 Axis { get; private set; }
        public bool IsPressed => _enabled && _pointer.IsPressed;
        public bool Tapped => _enabled && _pointer.Tapped;
        public Vector2 PointerPosition => _pointer.Position;
        public Vector2 DragDelta => _enabled ? _pointer.DragDelta : Vector2.zero;

        public void Calibrate() { }

        public void Tick(float deltaTime)
        {
            if (!_enabled) return;
            _pointer.Tick();

            if (_pointer.IsPressed)
            {
                var offset = (_pointer.Position - _pointer.PressStart) / (Mathf.Max(1, Screen.height) * _config.JoystickRadius);
                Axis = Vector2.ClampMagnitude(offset, 1f);
                return;
            }

            var target = KeyboardAxis.Read();
            _keyboardAxis = Vector2.Lerp(_keyboardAxis, target, MathExtensions.SmoothingFactor(_config.KeyboardSmoothing, deltaTime));
            Axis = _keyboardAxis.sqrMagnitude < 1e-4f ? Vector2.zero : _keyboardAxis;
        }
    }

    internal static class KeyboardAxis
    {
        /// <summary>WASD/arrows (Editor/desktop fallback). Zero when no keyboard.</summary>
        public static Vector2 Read()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            var x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            var y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }
    }
}
