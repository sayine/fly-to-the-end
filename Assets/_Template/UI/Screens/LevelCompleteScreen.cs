using Template.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Template.UI.Screens
{
    /// <summary>
    /// Level won (level-based games): 0-3 stars popping in one after another, the score, and Next / Retry / Menu.
    /// Driven by LevelCompletedEvent; the flow shows it.
    /// </summary>
    public sealed class LevelCompleteScreen : UIScreen
    {
        [SerializeField] private Image[] _stars = new Image[3];
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private GameObject _newBestLabel;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _menuButton;
        [Tooltip("TMP SetText format. {0:0.0} = one decimal place. Displayed value = score * _scoreScale.")]
        [SerializeField] private string _scoreFormat = "{0}";
        [SerializeField] private float _scoreScale = 1f;
        [SerializeField] private Color _starOn = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color _starOff = new Color(1f, 1f, 1f, 0.18f);

        private const float StarDelay = 0.35f;
        private const float StarPop = 0.25f;

        private int _earned;
        private float _shownAt;

        protected override void OnInit()
        {
            Bind(_nextButton, () => Choose(UIRequest.Next));
            Bind(_retryButton, () => Choose(UIRequest.Retry));
            Bind(_menuButton, () => Choose(UIRequest.Menu));
            EventBus<LevelCompletedEvent>.Subscribe(OnLevelCompleted);
        }

        private void OnDestroy() => EventBus<LevelCompletedEvent>.Unsubscribe(OnLevelCompleted);

        private void OnLevelCompleted(LevelCompletedEvent e)
        {
            _earned = Mathf.Clamp(e.Stars, 0, _stars.Length);
            if (_scoreText != null) _scoreText.SetText(_scoreFormat, e.Score * _scoreScale);
            if (_newBestLabel != null) _newBestLabel.SetActive(e.IsNewBest);
        }

        protected override void OnShow()
        {
            _shownAt = Time.unscaledTime;
            SetButtonsInteractable(true);
            for (var i = 0; i < _stars.Length; i++)
            {
                if (_stars[i] == null) continue;
                _stars[i].color = _starOff;
                _stars[i].rectTransform.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            if (!IsVisible) return;
            var t = Time.unscaledTime - _shownAt;
            for (var i = 0; i < _earned; i++)
            {
                if (_stars[i] == null) continue;
                var local = (t - StarDelay * (i + 1)) / StarPop;
                if (local < 0f) continue;
                _stars[i].color = _starOn;
                // Overshoot pop: 0 → 1.3 → 1.
                var s = local >= 1f ? 1f : Mathf.Sin(Mathf.Clamp01(local) * Mathf.PI) * 0.3f + Mathf.Clamp01(local);
                _stars[i].rectTransform.localScale = Vector3.one * s;
            }
        }

        private void Choose(UIRequest request)
        {
            SetButtonsInteractable(false); // prevent double taps while ads/scene loads run
            Request(request);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_nextButton != null) _nextButton.interactable = interactable;
            if (_retryButton != null) _retryButton.interactable = interactable;
            if (_menuButton != null) _menuButton.interactable = interactable;
        }
    }
}
