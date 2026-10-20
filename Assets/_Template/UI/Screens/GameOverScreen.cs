using Template.Core;
using Template.Services.Ads;
using Template.Services.Analytics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Template.UI.Screens
{
    /// <summary>
    /// Score, best, "Continue (rewarded)", "Retry", "Menu". The Continue button is only visible when the
    /// flow allows a continue AND a rewarded ad is loaded (graceful fallback: hidden otherwise).
    /// </summary>
    public sealed class GameOverScreen : UIScreen
    {
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _bestText;
        [SerializeField] private GameObject _newBestLabel;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _menuButton;
        [Tooltip("TMP SetText formats. {0:0.0} = one decimal place. Displayed value = score * _scoreScale.")]
        [SerializeField] private string _scoreFormat = "{0}";
        [SerializeField] private string _bestFormat = "BEST {0}";
        [SerializeField] private float _scoreScale = 1f;

        private IAdService _ads;
        private IAnalyticsService _analytics;
        private bool _continueAllowed;
        private bool _offerLogged;

        protected override void OnInit()
        {
            ServiceLocator.TryGet(out _ads);
            ServiceLocator.TryGet(out _analytics);
            Bind(_continueButton, () => Choose(UIRequest.Continue));
            Bind(_retryButton, () => Choose(UIRequest.Retry));
            Bind(_menuButton, () => Choose(UIRequest.Menu));
            EventBus<GameOverEvent>.Subscribe(OnGameOver);
            if (_ads != null) _ads.RewardedReadyChanged += OnRewardedReadyChanged;
        }

        private void OnDestroy()
        {
            EventBus<GameOverEvent>.Unsubscribe(OnGameOver);
            if (_ads != null) _ads.RewardedReadyChanged -= OnRewardedReadyChanged;
        }

        private void OnGameOver(GameOverEvent e)
        {
            _continueAllowed = e.CanContinue;
            if (_scoreText != null) _scoreText.SetText(_scoreFormat, e.Score * _scoreScale);
            if (_bestText != null) _bestText.SetText(_bestFormat, e.BestScore * _scoreScale);
            if (_newBestLabel != null) _newBestLabel.SetActive(e.IsNewBest);
            SetButtonsInteractable(true);
            RefreshContinue();
        }

        protected override void OnShow()
        {
            _offerLogged = false;
            RefreshContinue();
        }

        private void OnRewardedReadyChanged(bool ready) => RefreshContinue();

        private void RefreshContinue()
        {
            if (_continueButton == null) return;
            var visible = _continueAllowed && _ads != null && _ads.IsRewardedReady;
            _continueButton.gameObject.SetActive(visible);
            if (visible && IsVisible && !_offerLogged)
            {
                _offerLogged = true;
                _analytics?.RewardedOffered(AdPlacements.Continue);
            }
        }

        private void Choose(UIRequest request)
        {
            SetButtonsInteractable(false); // prevent double taps while ads/scene loads run
            Request(request);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_continueButton != null) _continueButton.interactable = interactable;
            if (_retryButton != null) _retryButton.interactable = interactable;
            if (_menuButton != null) _menuButton.interactable = interactable;
        }
    }
}
