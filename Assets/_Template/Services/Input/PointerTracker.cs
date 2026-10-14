using UnityEngine;
using UnityEngine.InputSystem;

namespace Template.Services.Input
{
    /// <summary>
    /// Single-finger pointer state shared by both input modes: primary touch, or mouse as the Editor fallback.
    /// Allocation-free; reads devices directly (no InputActions asset to wire).
    /// </summary>
    internal sealed class PointerTracker
    {
        private readonly InputConfig _config;
        private float _pressTime;
        private Vector2 _pressStart;
        private float _maxMove;
        private Vector2 _lastPosition;

        public bool IsPressed { get; private set; }
        public bool Tapped { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 PressStart => _pressStart;
        public Vector2 DragDelta { get; private set; }

        public PointerTracker(InputConfig config) => _config = config;

        public void Reset()
        {
            IsPressed = false;
            Tapped = false;
            DragDelta = Vector2.zero;
        }

        public void Tick()
        {
            Tapped = false;
            DragDelta = Vector2.zero;

            bool pressed;
            Vector2 position;
            var touch = Touchscreen.current;
            if (touch != null && (touch.primaryTouch.press.isPressed || touch.primaryTouch.press.wasReleasedThisFrame))
            {
                pressed = touch.primaryTouch.press.isPressed;
                position = touch.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null)
            {
                pressed = Mouse.current.leftButton.isPressed;
                position = Mouse.current.position.ReadValue();
            }
            else
            {
                pressed = false;
                position = Position;
            }

            var height = Mathf.Max(1, Screen.height);
            if (pressed && !IsPressed)
            {
                _pressTime = Time.unscaledTime;
                _pressStart = position;
                _lastPosition = position;
                _maxMove = 0f;
            }
            else if (pressed)
            {
                DragDelta = (position - _lastPosition) / height;
                _maxMove = Mathf.Max(_maxMove, (position - _pressStart).magnitude / height);
                _lastPosition = position;
            }
            else if (IsPressed)
            {
                Tapped = Time.unscaledTime - _pressTime <= _config.TapMaxDuration && _maxMove <= _config.TapMaxMove;
            }

            IsPressed = pressed;
            Position = position;
        }
    }
}
