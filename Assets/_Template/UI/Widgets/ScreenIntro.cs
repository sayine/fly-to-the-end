using UnityEngine;

namespace Template.UI.Widgets
{
    /// <summary>
    /// Screen entrance: fades the screen in and pops its panel from slightly small with a soft overshoot.
    /// UIScreen.Show() plays it when the component is present. Unscaled time.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenIntro : MonoBehaviour
    {
        [Tooltip("What pops (usually the main card). Empty = only fade.")]
        [SerializeField] private RectTransform _pop;
        [SerializeField, Min(0.05f)] private float _duration = 0.32f;
        [SerializeField, Range(0.5f, 1f)] private float _fromScale = 0.86f;
        [SerializeField, Range(0f, 0.5f)] private float _fadeTime = 0.15f;

        private CanvasGroup _group;
        private float _start = -1f;

        private void Awake() => _group = GetComponent<CanvasGroup>();

        public void Play()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            _start = Time.unscaledTime;
            Apply(0f);
        }

        private void Update()
        {
            if (_start < 0f) return;
            var t = (Time.unscaledTime - _start) / _duration;
            if (t >= 1f)
            {
                Apply(1f);
                _start = -1f;
                return;
            }
            Apply(t);
        }

        private void Apply(float t)
        {
            _group.alpha = _fadeTime <= 0f ? 1f : Mathf.Clamp01((t * _duration) / _fadeTime);
            if (_pop == null) return;
            // Ease-out-back: overshoots ~4% then settles.
            const float c1 = 1.70158f, c3 = c1 + 1f;
            var u = t - 1f;
            var e = 1f + c3 * u * u * u + c1 * u * u;
            var s = Mathf.LerpUnclamped(_fromScale, 1f, e);
            _pop.localScale = new Vector3(s, s, 1f);
        }
    }
}
