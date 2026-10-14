using System;
using Template.Core;
using Template.Services.Analytics;
using UnityEngine;

namespace Template.Services.Ads
{
    /// <summary>
    /// STUB — AppLovin MAX implementation. Activated on device when the scripting define <c>HC_MAX</c> is set.
    /// All policy/cooldown/retry/analytics logic lives in AdServiceBase; only fill the SDK hooks below.
    ///
    /// Integration notes (VERIFY the current install method in the official AppLovin MAX Unity docs first;
    /// it has changed between .unitypackage and UPM/OpenUPM over time):
    ///  1. Install the MAX Unity plugin, open AppLovin > Integration Manager, add SDK key
    ///     (read it from Secrets.MaxSdkKey at runtime instead of committing it where possible).
    ///  2. Add mediated networks (start small: AdMob, Unity Ads, Meta, Mintegral, ironSource/LevelPlay as needed).
    ///  3. Enable "Google UMP Automation" in Integration Manager if using MAX-managed consent (see UmpAttConsentService).
    ///  4. Put ad unit IDs in Assets/_Game/Configs/Resources/Secrets.asset (git-ignored).
    ///  5. Add HC_MAX to Scripting Define Symbols (iOS).
    ///  6. Test with MAX Mediation Debugger (MaxSdk.ShowMediationDebugger()) and test devices; ensure test ads are OFF for release.
    ///  7. app-ads.txt on the developer website (see Docs/CHECKLIST.md).
    /// MAX callbacks are invoked on the Unity main thread by the plugin.
    /// </summary>
    public sealed class MaxAdService : AdServiceBase
    {
        private readonly Secrets _secrets;
        private Action _interstitialClosed;
        private Action<bool> _rewardedClosed;
        private bool _rewardEarned;

        public MaxAdService(AdConfig config, Secrets secrets, IAppLifecycle lifecycle, IAnalyticsService analytics, int sessionCount)
            : base(config, lifecycle, analytics, sessionCount, "MAX")
        {
            _secrets = secrets;
        }

        protected override void InitializeSdk(bool trackingAllowed, Action<bool> onInitialized)
        {
            if (_secrets == null || string.IsNullOrEmpty(_secrets.MaxSdkKey))
            {
                Debug.LogWarning("[Ads:MAX] Missing Secrets.MaxSdkKey.");
                onInitialized(false);
                return;
            }

            // TODO(HC_MAX): register callbacks ONCE here, e.g.
            //  MaxSdkCallbacks.Interstitial.OnAdLoadedEvent     += (id, info) => OnInterstitialLoaded();
            //  MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (id, err)  => OnInterstitialLoadFailed(err.Message);
            //  MaxSdkCallbacks.Interstitial.OnAdHiddenEvent     += (id, info) => FireInterstitialClosed();
            //  MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (id, err, info) => FireInterstitialClosed();
            //  MaxSdkCallbacks.Rewarded.OnAdLoadedEvent         += (id, info) => OnRewardedLoaded();
            //  MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent     += (id, err)  => OnRewardedLoadFailed(err.Message);
            //  MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (id, reward, info) => _rewardEarned = true;
            //  MaxSdkCallbacks.Rewarded.OnAdHiddenEvent         += (id, info) => FireRewardedClosed();
            //  MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent  += (id, err, info) => FireRewardedClosed();
            //  MaxSdkCallbacks.OnSdkInitializedEvent += cfg => onInitialized(true);
            //  MaxSdk.SetSdkKey(_secrets.MaxSdkKey);   // if not set via Integration Manager
            //  MaxSdk.InitializeSdk();
            Debug.LogWarning("[Ads:MAX] STUB: SDK not integrated.");
            onInitialized(false);
        }

        protected override void LoadInterstitial()
        {
            // TODO(HC_MAX): MaxSdk.LoadInterstitial(_secrets.InterstitialId);
        }

        protected override void LoadRewarded()
        {
            // TODO(HC_MAX): MaxSdk.LoadRewardedAd(_secrets.RewardedId);
        }

        protected override void ShowInterstitialInternal(string placement, Action onClosed)
        {
            _interstitialClosed = onClosed;
            // TODO(HC_MAX): if (MaxSdk.IsInterstitialReady(_secrets.InterstitialId)) MaxSdk.ShowInterstitial(_secrets.InterstitialId, placement);
            //               else FireInterstitialClosed();
            FireInterstitialClosed();
        }

        protected override void ShowRewardedInternal(string placement, Action<bool> onClosed)
        {
            _rewardEarned = false;
            _rewardedClosed = onClosed;
            // TODO(HC_MAX): if (MaxSdk.IsRewardedAdReady(_secrets.RewardedId)) MaxSdk.ShowRewardedAd(_secrets.RewardedId, placement);
            //               else FireRewardedClosed();
            FireRewardedClosed();
        }

        protected override void ShowBannerInternal()
        {
            // TODO(HC_MAX): MaxSdk.CreateBanner(_secrets.BannerId, MaxSdkBase.BannerPosition.BottomCenter) once; MaxSdk.ShowBanner(_secrets.BannerId);
        }

        protected override void HideBannerInternal()
        {
            // TODO(HC_MAX): MaxSdk.HideBanner(_secrets.BannerId);
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
