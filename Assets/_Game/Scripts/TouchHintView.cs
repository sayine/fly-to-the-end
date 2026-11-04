using TMPro;
using UnityEngine;

namespace Game
{
    public enum TouchHint
    {
        None,
        Launch, // ghost finger swipes up: "Swipe up to throw"
        Steer,  // ghost finger sways left/right and up/down: "Drag to steer"
    }

    /// <summary>
    /// First-time hint over the lower part of the screen (SPEC: hint animation, no text-heavy tutorial): a ghost
    /// finger and a short label. The whole screen is the controller, so there is no visible control area.
    /// Owned by PaperPlaneGame; raycasts off so it never blocks HUD buttons.
    /// </summary>
    public sealed class TouchHintView : MonoBehaviour
    {
        [Tooltip("Area the ghost finger moves in (lower screen).")]
        [SerializeField] private RectTransform _area;
        [SerializeField] private RectTransform _finger;
        [SerializeField] private CanvasGroup _fingerGroup;
        [SerializeField] private TMP_Text _label;
        [Tooltip("The label with its backing, shown/hidden together.")]
        [SerializeField] private GameObject _labelRoot;

        private const float LaunchCycle = 1.4f;
        private const float SteerCycle = 2.4f;

        private TouchHint _hint = TouchHint.Launch; // forces the first SetHint(None) to hide everything
        private float _time;

        private void Awake() => SetHint(TouchHint.None);

        public void SetHint(TouchHint hint)
        {
            if (hint == _hint) return;
            _hint = hint;
            _time = 0f;
            var on = hint != TouchHint.None;
            if (_finger != null) _finger.gameObject.SetActive(on);
            if (_labelRoot != null) _labelRoot.SetActive(on);
            if (_label != null && on) _label.SetText(hint == TouchHint.Launch ? "Swipe up to throw" : "Drag to steer");
        }

        private void Update()
        {
            if (_hint == TouchHint.None || _finger == null || _area == null) return;
            _time += Time.unscaledDeltaTime;
            var size = _area.rect.size;
            Vector2 position;
            float alpha;
            if (_hint == TouchHint.Launch)
            {
                // Press low, flick up, fade out, repeat.
                var t = (_time % LaunchCycle) / LaunchCycle;
                var swipe = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.65f, t));
                position = new Vector2(0f, Mathf.Lerp(-0.4f, 0.45f, swipe) * size.y);
                alpha = t < 0.2f ? t / 0.2f : 1f - Mathf.InverseLerp(0.6f, 0.85f, t);
            }
            else
            {
                // Figure-eight sway: left/right = bank, up/down = pitch.
                var phase = _time / SteerCycle * Mathf.PI * 2f;
                position = new Vector2(Mathf.Sin(phase) * size.x * 0.25f, Mathf.Sin(phase * 2f) * size.y * 0.2f);
                alpha = 0.9f;
            }
            _finger.anchoredPosition = position;
            if (_fingerGroup != null) _fingerGroup.alpha = alpha;
        }
    }
}
