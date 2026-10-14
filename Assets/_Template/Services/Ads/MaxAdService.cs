#if HC_MAX
using System;
using Template.Core;
using Template.Services.Analytics;
using UnityEngine;

namespace Template.Services.Ads
{
    /// <summary>
    /// AppLovin MAX implementation (device builds with the HC_MAX define; the Editor always uses MockAdService).
    /// Policy/cooldown/retry/analytics live in AdServiceBase; this class only maps MAX calls and callbacks.
    ///
    /// Setup (see Docs/SDK.md): SDK key + "MAX Terms and Privacy Policy Flow" (Google UMP + ATT) are set in
    /// AppLovin > Integration Manager. The consent form and ATT prompt are shown by MAX during InitializeSdk,
    /// see MaxConsentService. Ad unit IDs come from the git-ignored Secrets asset.
    /// MAX invokes callbacks on the Unity main thread.
    /// </summary>
    public sealed class MaxAdService : AdServiceBase
    {
        private readonly Secrets _secrets;
        private Action _interstitialClosed;
        private Action<bool> _rewardedClosed;
        private bool _rewardEarned;
        private bool _bannerCreated;

        public MaxAdService(AdConfig config, Secrets secrets, IAppLifecycle lifecycle, IAnalyticsService analytics, int sessionCount)
            : base(config, lifecycle, analytics, sessionCount, "MAX")
        {
            _secrets = secrets;
        }

        protected override void InitializeSdk(bool trackingAllowed, Action<bool> onInitialized)
        {
            if (_secrets == null)
            {
                Debug.LogWarning("[Ads:MAX] Secrets asset missing (Assets/_Game/Configs/Resources/Secrets.asset). Ads disabled.");
                onInitialized(false);
                return;
            }

            RegisterCallbacks();
            MaxSdkCallbacks.OnSdkInitializedEvent += config =>
            {
                Debug.Log($"[Ads:MAX] SDK initialized. Geography={config.ConsentFlowUserGeography} ATT={config.AppTrackingStatus}");
                onInitialized(true);
            };

            if (Debug.isDebugBuild && !string.IsNullOrEmpty(_secrets.GoogleTestDeviceHashedId))
                MaxSdk.SetExtraParameter("google_test_device_hashed_id", _secrets.GoogleTestDeviceHashedId);

            MaxSdk.InitializeSdk();
        }

        private void RegisterCallbacks()
        {
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (id, info) => { if (id == _secrets.InterstitialId) OnInterstitialLoaded(); };
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (id, error) => { if (id == _secrets.InterstitialId) OnInterstitialLoadFailed(error.Message); };
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (id, info) => FireInterstitialClosed();
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (id, error, info) => FireInterstitialClosed();

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (id, info) => { if (id == _secrets.RewardedId) OnRewardedLoaded(); };
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (id, error) => { if (id == _secrets.RewardedId) OnRewardedLoadFailed(error.Message); };
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (id, reward, info) => _rewardEarned = true;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (id, info) => FireRewardedClosed();
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (id, error, info) => FireRewardedClosed();
        }

        protected override void LoadInterstitial()
        {
            if (!string.IsNullOrEmpty(_secrets.InterstitialId)) MaxSdk.LoadInterstitial(_secrets.InterstitialId);
        }

        protected override void LoadRewarded()
        {
            if (!string.IsNullOrEmpty(_secrets.RewardedId)) MaxSdk.LoadRewardedAd(_secrets.RewardedId);
        }

        protected override void ShowInterstitialInternal(string placement, Action onClosed)
        {
            _interstitialClosed = onClosed;
            if (MaxSdk.IsInterstitialReady(_secrets.InterstitialId)) MaxSdk.ShowInterstitial(_secrets.InterstitialId, placement);
            else FireInterstitialClosed();
        }

        protected override void ShowRewardedInternal(string placement, Action<bool> onClosed)
        {
            _rewardEarned = false;
            _rewardedClosed = onClosed;
            if (MaxSdk.IsRewardedAdReady(_secrets.RewardedId)) MaxSdk.ShowRewardedAd(_secrets.RewardedId, placement);
            else FireRewardedClosed();
        }

        protected override void ShowBannerInternal()
        {
            if (string.IsNullOrEmpty(_secrets.BannerId)) return;
            if (!_bannerCreated)
            {
                MaxSdk.CreateBanner(_secrets.BannerId, MaxSdkBase.BannerPosition.BottomCenter);
                _bannerCreated = true;
            }
            MaxSdk.ShowBanner(_secrets.BannerId);
        }

        protected override void HideBannerInternal()
        {
            if (_bannerCreated) MaxSdk.HideBanner(_secrets.BannerId);
        }

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
