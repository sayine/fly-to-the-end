using System;
using System.Collections;
using Template.Core;
using Template.Services.Analytics;
using UnityEngine;

namespace Template.Services.Ads
{
    /// <summary>
    /// Shared ad logic: policy/cooldown, preloading, load retry with backoff, analytics, audio pause
    /// and exception safety. SDK-specific subclasses only implement the protected abstract hooks
    /// and report load results through the On*Loaded/On*LoadFailed methods.
    /// </summary>
    public abstract class AdServiceBase : IAdService
    {
        protected readonly AdConfig Config;
        protected readonly IAppLifecycle Lifecycle;
        private readonly IAnalyticsService _analytics;
        private readonly AdPolicy _policy;
        private readonly string _tag;

        private bool _interstitialLoaded;
        private bool _rewardedLoaded;
        private bool _showing;
        private int _interstitialRetry;
        private int _rewardedRetry;

        public bool IsInitialized { get; private set; }
        public bool IsRewardedReady => IsInitialized && _rewardedLoaded && !_showing;
        public event Action<bool> RewardedReadyChanged;

        protected AdServiceBase(AdConfig config, IAppLifecycle lifecycle, IAnalyticsService analytics, int sessionCount, string tag)
        {
            Config = config;
            Lifecycle = lifecycle;
            _analytics = analytics;
            _tag = $"[Ads:{tag}]";
            _policy = new AdPolicy(config != null ? config.Rules : AdRules.Default, sessionCount);
        }

        protected static float Now => Time.realtimeSinceStartup;

        // ---------- SDK hooks ----------
        protected abstract void InitializeSdk(bool trackingAllowed, Action<bool> onInitialized);
        protected abstract void LoadInterstitial();
        protected abstract void LoadRewarded();
        protected abstract void ShowInterstitialInternal(string placement, Action onClosed);
        /// <param name="onClosed">bool = reward earned</param>
        protected abstract void ShowRewardedInternal(string placement, Action<bool> onClosed);
        protected virtual void ShowBannerInternal() { }
        protected virtual void HideBannerInternal() { }

        // ---------- IAdService ----------
        public void Initialize(bool canRequestAds, bool trackingAllowed, Action onComplete)
        {
            if (!canRequestAds)
            {
                Debug.LogWarning($"{_tag} Consent does not allow ad requests. Ads disabled this session.");
                onComplete?.Invoke();
                return;
            }

            var completed = false;
            void Done(bool success)
            {
                if (completed) return;
                completed = true;
                IsInitialized = success;
                Debug.Log($"{_tag} Initialized: {success}");
                if (success)
                {
                    SafeLoadInterstitial();
                    SafeLoadRewarded();
                    if (Config != null && Config.BannerEnabled) ShowBanner();
                }
                onComplete?.Invoke();
            }

            if (!Safe(() => InitializeSdk(trackingAllowed, Done))) Done(false);
        }

        public void SetGameplayActive(bool active) => _policy.GameplayActive = active;

        public bool ShowInterstitial(string placement, Action onClosed = null)
        {
            var reason = !IsInitialized ? AdBlockReason.NotInitialized
                : _showing ? AdBlockReason.AlreadyShowing
                : _policy.CheckInterstitial(Now);
            if (reason == AdBlockReason.None && !_interstitialLoaded) reason = AdBlockReason.NotLoaded;

            if (reason != AdBlockReason.None)
            {
                Debug.Log($"{_tag} Interstitial '{placement}' skipped: {reason}");
                if (reason == AdBlockReason.NotLoaded) SafeLoadInterstitial();
                onClosed?.Invoke();
                return false;
            }

            _interstitialLoaded = false;
            BeginFullscreen();
            var closed = false;
            void Closed()
            {
                if (closed) return;
                closed = true;
                EndFullscreen();
                _policy.NotifyInterstitialShown(Now);
                _analytics?.InterstitialShown(placement);
                SafeLoadInterstitial(); // preload next right away
                onClosed?.Invoke();
            }

            if (!Safe(() => ShowInterstitialInternal(placement, Closed))) Closed();
            return true;
        }

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            if (!IsRewardedReady)
            {
                Debug.Log($"{_tag} Rewarded '{placement}' not ready.");
                if (IsInitialized && !_rewardedLoaded) SafeLoadRewarded();
                onComplete?.Invoke(false);
                return;
            }

            SetRewardedLoaded(false);
            BeginFullscreen();
            var closed = false;
            void Closed(bool rewarded)
            {
                if (closed) return;
                closed = true;
                EndFullscreen();
                _policy.NotifyRewardedShown(Now);
                if (rewarded) _analytics?.RewardedCompleted(placement);
                SafeLoadRewarded();
                onComplete?.Invoke(rewarded);
            }

            if (!Safe(() => ShowRewardedInternal(placement, Closed))) Closed(false);
        }

        public void ShowBanner()
        {
            if (IsInitialized) Safe(ShowBannerInternal);
        }

        public void HideBanner() => Safe(HideBannerInternal);

        // ---------- Load callbacks for subclasses ----------
        protected void OnInterstitialLoaded()
        {
            _interstitialRetry = 0;
            _interstitialLoaded = true;
        }

        protected void OnInterstitialLoadFailed(string error)
        {
            _interstitialLoaded = false;
            ScheduleRetry(ref _interstitialRetry, SafeLoadInterstitial, "interstitial", error);
        }

        protected void OnRewardedLoaded()
        {
            _rewardedRetry = 0;
            SetRewardedLoaded(true);
        }

        protected void OnRewardedLoadFailed(string error)
        {
            SetRewardedLoaded(false);
            ScheduleRetry(ref _rewardedRetry, SafeLoadRewarded, "rewarded", error);
        }

        // ---------- Internals ----------
        private void SetRewardedLoaded(bool loaded)
        {
            var before = IsRewardedReady;
            _rewardedLoaded = loaded;
            if (before != IsRewardedReady) RewardedReadyChanged?.Invoke(IsRewardedReady);
        }

        private void BeginFullscreen()
        {
            var before = IsRewardedReady;
            _showing = true;
            AudioListener.pause = true;
            if (before != IsRewardedReady) RewardedReadyChanged?.Invoke(IsRewardedReady);
        }

        private void EndFullscreen()
        {
            _showing = false;
            AudioListener.pause = false;
            if (IsRewardedReady) RewardedReadyChanged?.Invoke(true);
        }

        private void SafeLoadInterstitial() => Safe(LoadInterstitial);
        private void SafeLoadRewarded() => Safe(LoadRewarded);

        private void ScheduleRetry(ref int attempt, Action load, string kind, string error)
        {
            attempt++;
            var maxDelay = Config != null ? Config.MaxLoadRetryDelay : 64f;
            var delay = Mathf.Min(maxDelay, Mathf.Pow(2f, Mathf.Min(attempt, 6)));
            Debug.Log($"{_tag} {kind} load failed ({error}). Retry #{attempt} in {delay:0}s.");
            Lifecycle?.Run(RetryAfter(delay, load));
        }

        private static IEnumerator RetryAfter(float delay, Action action)
        {
            yield return new WaitForSecondsRealtime(delay);
            action();
        }

        /// <summary>Ad SDK failures must never crash the game: log and continue.</summary>
        private bool Safe(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{_tag} SDK call failed: {e}");
                return false;
            }
        }
    }
}
