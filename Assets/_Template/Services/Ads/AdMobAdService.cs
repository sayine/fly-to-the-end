#if HC_ADMOB
using System;
using GoogleMobileAds.Api;
using Template.Core;
using Template.Services.Analytics;
using UnityEngine;

namespace Template.Services.Ads
{
    /// <summary>
    /// Google AdMob implementation (device builds with the HC_ADMOB define; the Editor uses MockAdService).
    /// Policy/cooldown/retry/analytics live in AdServiceBase; this class only maps SDK calls and callbacks.
    /// Development builds ALWAYS use Google's test ad units (never click your own live ads: account ban risk);
    /// release builds use the ad unit IDs from the git-ignored Secrets asset.
    /// App ID: Assets > Google Mobile Ads > Settings (or Tools/Template/Setup/Configure AdMob).
    /// </summary>
    public sealed class AdMobAdService : AdServiceBase
    {
        // https://developers.google.com/admob/unity/test-ads (iOS)
        private const string TestInterstitialId = "ca-app-pub-3940256099942544/4411468910";
        private const string TestRewardedId = "ca-app-pub-3940256099942544/1712485313";
        private const string TestBannerId = "ca-app-pub-3940256099942544/2934735716";

        private readonly string _interstitialId;
        private readonly string _rewardedId;
        private readonly string _bannerId;

        private InterstitialAd _interstitial;
        private RewardedAd _rewarded;
        private BannerView _banner;
        private Action _interstitialClosed;
        private Action<bool> _rewardedClosed;
        private volatile bool _rewardEarned;

        public AdMobAdService(AdConfig config, Secrets secrets, IAppLifecycle lifecycle, IAnalyticsService analytics, int sessionCount)
            : base(config, lifecycle, analytics, sessionCount, "AdMob")
        {
            var test = Debug.isDebugBuild;
            _interstitialId = test ? TestInterstitialId : secrets?.InterstitialId;
            _rewardedId = test ? TestRewardedId : secrets?.RewardedId;
            _bannerId = test ? TestBannerId : secrets?.BannerId;
        }

        protected override void InitializeSdk(bool trackingAllowed, Action<bool> onInitialized)
        {
            if (string.IsNullOrEmpty(_interstitialId) && string.IsNullOrEmpty(_rewardedId))
            {
                Debug.LogWarning("[Ads:AdMob] No ad unit IDs (Assets/_Game/Configs/Resources/Secrets.asset). Ads disabled.");
                onInitialized(false);
                return;
            }

            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetiOSAppPauseOnBackground(true);
            MobileAds.Initialize(status => Lifecycle.RunOnMainThread(() => onInitialized(status != null)));
        }

        protected override void LoadInterstitial()
        {
            if (string.IsNullOrEmpty(_interstitialId)) return;
            _interstitial?.Destroy();
            _interstitial = null;
            InterstitialAd.Load(_interstitialId, new AdRequest(), (ad, error) => Lifecycle.RunOnMainThread(() =>
            {
                if (error != null || ad == null)
                {
                    OnInterstitialLoadFailed(error?.GetMessage() ?? "null ad");
                    return;
                }
                _interstitial = ad;
                ad.OnAdFullScreenContentClosed += () => Lifecycle.RunOnMainThread(FireInterstitialClosed);
                ad.OnAdFullScreenContentFailed += e => Lifecycle.RunOnMainThread(FireInterstitialClosed);
                OnInterstitialLoaded();
            }));
        }

        protected override void LoadRewarded()
        {
            if (string.IsNullOrEmpty(_rewardedId)) return;
            _rewarded?.Destroy();
            _rewarded = null;
            RewardedAd.Load(_rewardedId, new AdRequest(), (ad, error) => Lifecycle.RunOnMainThread(() =>
            {
                if (error != null || ad == null)
                {
                    OnRewardedLoadFailed(error?.GetMessage() ?? "null ad");
                    return;
                }
                _rewarded = ad;
                ad.OnAdFullScreenContentClosed += () => Lifecycle.RunOnMainThread(FireRewardedClosed);
                ad.OnAdFullScreenContentFailed += e => Lifecycle.RunOnMainThread(FireRewardedClosed);
                OnRewardedLoaded();
            }));
        }

        protected override void ShowInterstitialInternal(string placement, Action onClosed)
        {
            _interstitialClosed = onClosed;
            if (_interstitial != null && _interstitial.CanShowAd()) _interstitial.Show();
            else FireInterstitialClosed();
        }

        protected override void ShowRewardedInternal(string placement, Action<bool> onClosed)
        {
            _rewardEarned = false;
            _rewardedClosed = onClosed;
            if (_rewarded != null && _rewarded.CanShowAd()) _rewarded.Show(reward => _rewardEarned = true);
            else FireRewardedClosed();
        }

        protected override void ShowBannerInternal()
        {
            if (string.IsNullOrEmpty(_bannerId)) return;
            if (_banner == null)
            {
                _banner = new BannerView(_bannerId, AdSize.Banner, AdPosition.Bottom);
                _banner.LoadAd(new AdRequest());
            }
            _banner.Show();
        }

        protected override void HideBannerInternal() => _banner?.Hide();

        private void FireInterstitialClosed()
        {
            var cb = _interstitialClosed;
            _interstitialClosed = null;
            cb?.Invoke();
        }

        private void FireRewardedClosed()
        {
            var cb = _rewardedClosed;
            _rewardedClosed = null;
            cb?.Invoke(_rewardEarned);
        }
    }
}
#endif
