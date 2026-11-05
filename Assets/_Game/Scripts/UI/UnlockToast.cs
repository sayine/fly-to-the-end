using System.Collections.Generic;
using Game.Planes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// "NEW PLANE!" toast with the plane's picture, over everything (own canvas above the UI root), for a few
    /// seconds after a level unlocks planes. Lives in the Game scene.
    /// </summary>
    public sealed class UnlockToast : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private Image _thumbnail;
        [SerializeField, Min(0.5f)] private float _duration = 3f;
        [SerializeField] private AudioClip _sfx;

        private readonly Queue<PlaneConfig> _queue = new Queue<PlaneConfig>();
        private float _shownAt = -1f;

        private void Awake() => _canvas.enabled = false;

        public void Show(PlaneConfig plane)
        {
            if (plane != null) _queue.Enqueue(plane);
            if (_shownAt < 0f) Next();
        }

        private void Next()
        {
            if (_queue.Count == 0)
            {
                _canvas.enabled = false;
                _shownAt = -1f;
                return;
            }
            var plane = _queue.Dequeue();
            _name.text = plane.DisplayName;
            _thumbnail.sprite = plane.Thumbnail;
            _canvas.enabled = true;
            _shownAt = Time.unscaledTime;
            if (_sfx != null && Template.Core.ServiceLocator.TryGet(out Template.Services.Audio.IAudioService audio)) audio.PlaySfx(_sfx, 0.8f);
        }

        private void Update()
        {
            if (_shownAt < 0f) return;
            var t = Time.unscaledTime - _shownAt;
            // Slide in from the top, hold, slide out.
            var slide = Mathf.Min(Mathf.SmoothStep(0f, 1f, t / 0.3f), Mathf.SmoothStep(0f, 1f, (_duration - t) / 0.3f));
            _panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(260f, -150f, slide));
            if (t >= _duration) Next();
        }
    }
}
