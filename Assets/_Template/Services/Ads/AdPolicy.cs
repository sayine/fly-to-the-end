namespace Template.Services.Ads
{
    public enum AdBlockReason
    {
        None,
        NotInitialized,
        GameplayActive,
        TooEarlyInFirstSessions,
        Cooldown,
        NotLoaded,
        AlreadyShowing,
    }

    /// <summary>
    /// Pure frequency-capping rules for interstitials. Time is passed in (seconds since app start)
    /// so the policy is deterministic and unit-tested (see AdPolicyTests).
    /// Interpretation of "first one not before session 2 or 90 s":
    /// allowed when sessionCount >= MinSession OR sessionTime >= MinSecondsInSession.
    /// </summary>
    public sealed class AdPolicy
    {
        private readonly AdRules _rules;
        private readonly int _sessionCount;
        private float _lastFullscreenAdTime = float.NegativeInfinity;

        public AdPolicy(AdRules rules, int sessionCount)
        {
            _rules = rules;
            _sessionCount = sessionCount;
        }

        public bool GameplayActive { get; set; }

        public AdBlockReason CheckInterstitial(float now)
        {
            if (GameplayActive) return AdBlockReason.GameplayActive;
            if (_sessionCount < _rules.InterstitialMinSession && now < _rules.InterstitialMinSecondsInSession)
                return AdBlockReason.TooEarlyInFirstSessions;
            if (now - _lastFullscreenAdTime < _rules.InterstitialCooldownSeconds)
                return AdBlockReason.Cooldown;
            return AdBlockReason.None;
        }

        public bool CanShowInterstitial(float now) => CheckInterstitial(now) == AdBlockReason.None;

        /// <summary>Call when the interstitial closes (cooldown counts from close, not open).</summary>
        public void NotifyInterstitialShown(float now) => _lastFullscreenAdTime = now;

        public void NotifyRewardedShown(float now)
        {
            if (_rules.RewardedResetsInterstitialCooldown) _lastFullscreenAdTime = now;
        }
    }
}
