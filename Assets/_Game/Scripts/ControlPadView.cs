using TMPro;
using UnityEngine;

namespace Game
{
    public enum PadHint
    {
        None,
        Launch, // ghost finger swipes up: "Swipe up to throw"
        Steer,  // ghost finger sways left/right and up/down: "Drag to steer"
    }

    /// <summary>
    /// The steering pad: a soft rounded strip along the bottom, always visible in the Game scene. It brightens
    /// while a finger is on it. For first-time players it shows a ghost finger and a short label (SPEC: hint
    /// animation, no text-heavy tutorial). Owned by PaperPlaneGame; raycasts off so it never blocks HUD buttons.
    /// </summary>
    public sealed class ControlPadView : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _pad;
        [SerializeField] private CanvasGroup _group;
        [Tooltip("Ghost finger (circle) animated for the hint.")]
        [SerializeField] private RectTransform _finger;
        [SerializeField] private CanvasGroup _fingerGroup;
        [SerializeField] private TMP_Text _label;
        [SerializeField, Range(0f, 1f)] private float _idleAlpha = 0.55f;

        private const float LaunchCycle = 1.4f;
        private const float SteerCycle = 2.4f;

        private Rect _lastScreenRect;
        private PadHint _hint = PadHint.None;
        private float _hintTime;

        private void Awake() => SetHint(PadHint.None);

        public void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.enabled != visible) _canvas.enabled = visible;
        }

        /// <summary>padRect in screen pixels; active = a finger is currently using the pad.</summary>
        public void Refresh(Rect padRect, bool active)
        {
            if (_pad == null) return;
            if (padRect != _lastScreenRect)
            {
                _lastScreenRect = padRect;
                var w = Mathf.Max(1, Screen.width);
                var h = Mathf.Max(1, Screen.height);
                _pad.anchorMin = new Vector2(padRect.xMin / w, padRect.yMin / h);
                _pad.anchorMax = new Vector2(padRect.xMax / w, padRect.yMax / h);
                _pad.offsetMin = _pad.offsetMax = Vector2.zero;
            }
            if (_group != null) _group.alpha = active ? 1f : _idleAlpha;
        }

        public void SetHint(PadHint hint)
        {
            if (hint == _hint && _finger != null && _finger.gameObject.activeSelf == (hint != PadHint.None)) return;
            _hint = hint;
            _hintTime = 0f;
            var on = hint != PadHint.None;
            if (_finger != null) _finger.gameObject.SetActive(on);
            if (_label != null)
            {
                _label.gameObject.SetActive(on);
                if (on) _label.SetText(hint == PadHint.Launch ? "Swipe up to throw" : "Drag to steer");
            }
        }

        private void Update()
        {
            if (_hint == PadHint.None || _finger == null || _pad == null) return;
            _hintTime += Time.unscaledDeltaTime;
            var size = _pad.rect.size;
            Vector2 position;
            float alpha;
            if (_hint == PadHint.Launch)
            {
                // Press near the bottom, flick up past the top of the pad, fade out, repeat.
                var t = (_hintTime % LaunchCycle) / LaunchCycle;
                var swipe = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.65f, t));
                position = new Vector2(0f, Mathf.Lerp(-0.25f, 0.9f, swipe) * size.y);
                alpha = t < 0.2f ? t / 0.2f : 1f - Mathf.InverseLerp(0.6f, 0.85f, t);
            }
            else
            {
                // Figure-eight sway inside the pad: left/right = bank, up/down = pitch.
                var phase = _hintTime / SteerCycle * Mathf.PI * 2f;
                position = new Vector2(Mathf.Sin(phase) * size.x * 0.22f, Mathf.Sin(phase * 2f) * size.y * 0.18f);
                alpha = 0.9f;
            }
            _finger.anchoredPosition = position;
            if (_fingerGroup != null) _fingerGroup.alpha = alpha;
        }
    }
}
