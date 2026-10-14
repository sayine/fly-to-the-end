using UnityEngine;

namespace Template.Services.Input
{
    public enum InputMode
    {
        Touch,
        Tilt,
    }

    [CreateAssetMenu(menuName = "Template/Configs/Input Config", fileName = "InputConfig")]
    public sealed class InputConfig : ScriptableObject
    {
        [SerializeField] private InputMode _mode = InputMode.Tilt;

        [Header("Tilt")]
        [Tooltip("Device tilt (degrees from the calibrated neutral) that maps to a full axis value of 1.")]
        [SerializeField, Range(5f, 60f)] private float _maxTiltAngle = 25f;
        [SerializeField, Range(0.1f, 3f)] private float _tiltSensitivity = 1f;
        [Tooltip("Radial dead zone in normalized axis units.")]
        [SerializeField, Range(0f, 0.5f)] private float _tiltDeadZone = 0.08f;
        [Tooltip("Low-pass filter sharpness (higher = more responsive, lower = smoother).")]
        [SerializeField, Range(1f, 40f)] private float _tiltSmoothing = 12f;
        [SerializeField] private bool _calibrateOnStart = true;
        [SerializeField] private bool _invertX;
        [SerializeField] private bool _invertY;

        [Header("Touch")]
        [Tooltip("Drag distance (fraction of screen height) that maps to a full joystick axis.")]
        [SerializeField, Range(0.02f, 0.5f)] private float _joystickRadius = 0.12f;
        [SerializeField, Range(0.05f, 1f)] private float _tapMaxDuration = 0.25f;
        [Tooltip("Max movement (fraction of screen height) for a press to still count as a tap.")]
        [SerializeField, Range(0.005f, 0.1f)] private float _tapMaxMove = 0.02f;

        [Header("Editor fallback")]
        [Tooltip("Keyboard (WASD/arrows) response sharpness when no sensor/touch is present.")]
        [SerializeField, Range(1f, 40f)] private float _keyboardSmoothing = 10f;

        public InputMode Mode => _mode;
        public float MaxTiltAngle => _maxTiltAngle;
        public float TiltSensitivity => _tiltSensitivity;
        public float TiltDeadZone => _tiltDeadZone;
        public float TiltSmoothing => _tiltSmoothing;
        public bool CalibrateOnStart => _calibrateOnStart;
        public bool InvertX => _invertX;
        public bool InvertY => _invertY;
        public float JoystickRadius => _joystickRadius;
        public float TapMaxDuration => _tapMaxDuration;
        public float TapMaxMove => _tapMaxMove;
        public float KeyboardSmoothing => _keyboardSmoothing;
    }
}
