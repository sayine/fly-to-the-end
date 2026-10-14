using System;

namespace Template.Services.Ads
{
    public interface IAdService
    {
        bool IsInitialized { get; }
        bool IsRewardedReady { get; }

        /// <summary>Fires when rewarded availability changes (show/hide "Continue" button).</summary>
        event Action<bool> RewardedReadyChanged;

        /// <summary>Call once, after consent. Always invokes onComplete (also on failure).</summary>
        void Initialize(bool canRequestAds, bool trackingAllowed, Action onComplete);

        /// <summary>Game flow reports active play; interstitials are never shown while true.</summary>
        void SetGameplayActive(bool active);

        /// <summary>
        /// Shows an interstitial if the AdPolicy allows it. onClosed is ALWAYS invoked
        /// (immediately when no ad is shown). Returns true if an ad is being shown.
        /// </summary>
        bool ShowInterstitial(string placement, Action onClosed = null);

        /// <summary>onComplete(true) only if the user earned the reward. Always invoked.</summary>
        void ShowRewarded(string placement, Action<bool> onComplete);

        void ShowBanner();
        void HideBanner();
    }

    public static class AdPlacements
    {
        public const string GameOver = "game_over";
        public const string Continue = "continue";
        public const string DoubleReward = "double_reward";
    }
}
