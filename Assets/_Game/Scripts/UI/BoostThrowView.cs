using Template.Core;
using Template.Services.Ads;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Rewarded "Boost throw" (SPEC §5): in Free Flight, while the plane waits on the shelf, a button offers +20% throw
    /// speed for this run. Hidden when no ad is loaded, once used, and once the plane is thrown. Lives on the HUD
    /// (top band, which never starts a throw).
    /// </summary>
    public sealed class BoostThrowView : MonoBehaviour
    {
        public const string Placement = "boost_throw";

        [SerializeField] private Button _button;

        private IAdService _ads;
        private AimStateEvent _state;
        private bool _offered;

        private void Awake()
        {
            ServiceLocator.TryGet(out _ads);
            _button.onClick.AddListener(Watch);
            EventBus<AimStateEvent>.Subscribe(OnAimState);
            if (_ads != null) _ads.RewardedReadyChanged += OnReadyChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            EventBus<AimStateEvent>.Unsubscribe(OnAimState);
            if (_ads != null) _ads.RewardedReadyChanged -= OnReadyChanged;
        }

        private void OnReadyChanged(bool ready) => Refresh();

        private void OnAimState(AimStateEvent e)
        {
            if (!e.Aiming) _offered = false;
            _state = e;
            Refresh();
        }

        private void Refresh()
        {
            var show = _state.Aiming && _state.FreeFlight && !_state.Boosted && _ads != null && _ads.IsRewardedReady;
            if (_button.gameObject.activeSelf != show) _button.gameObject.SetActive(show);
            _button.interactable = true;
            if (show && !_offered)
            {
                _offered = true;
                PaperAnalytics.RewardedOffered(Placement);
            }
        }

        private void Watch()
        {
            _button.interactable = false;
            _ads.ShowRewarded(Placement, rewarded =>
            {
                if (rewarded) EventBus<BoostThrowGrantedEvent>.Raise(default);
                Refresh();
            });
        }
    }
}
