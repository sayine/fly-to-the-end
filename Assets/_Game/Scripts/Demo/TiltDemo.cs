using Template.Core;
using Template.Services.Input;
using UnityEngine;

namespace Game.Demo
{
    /// <summary>
    /// Demo_Tilt: verifies the tilt pipeline end-to-end without the Boot flow.
    /// Uses the registered IPlayerInput if Boot ran, otherwise creates a TiltInput itself.
    /// Shows raw gravity, calibrated axis and a Recalibrate button (IMGUI, debug only: allocates, never ship in a game scene).
    /// Editor: WASD / arrow keys.
    /// </summary>
    public sealed class TiltDemo : MonoBehaviour
    {
        [SerializeField] private InputConfig _inputConfig;
        [SerializeField] private GameConfig _gameConfig;
        [SerializeField] private TiltBall _ball;

        private IPlayerInput _input;
        private bool _ownsInput;
        private GUIStyle _style;

        private void Awake()
        {
            if (!ServiceLocator.TryGet(out _input))
            {
                _input = new TiltInput(_inputConfig);
                _ownsInput = true;
            }
            _input.Enabled = true;
        }

        private void Start() => _input.Calibrate();

        private void Update()
        {
            if (_ownsInput) _input.Tick(Time.unscaledDeltaTime);
            _ball.Axis = _input.Axis;
            if (_ball.transform.position.y < _gameConfig.FallHeight) _ball.ResetToStart();
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.022f) };
            var tilt = _input as TiltInput;
            var line = _style.fontSize * 1.4f;
            var y = Screen.height - Screen.safeArea.yMax + 10f; // IMGUI origin is top-left
            var x = 20f;
            var w = Screen.width - 40f;

            GUI.Label(new Rect(x, y, w, line), tilt != null && tilt.HasSensor ? "Sensor: ON" : "Sensor: none (keyboard WASD/arrows)", _style);
            if (tilt != null) GUI.Label(new Rect(x, y += line, w, line), $"Gravity: {tilt.RawGravity:F2}", _style);
            GUI.Label(new Rect(x, y += line, w, line), $"Axis: {_input.Axis:F2}", _style);
            if (GUI.Button(new Rect(x, y + line * 1.5f, Screen.width * 0.45f, line * 2f), "Recalibrate", GUI.skin.button))
            {
                _input.Calibrate();
                _ball.ResetToStart();
            }
        }
    }
}
