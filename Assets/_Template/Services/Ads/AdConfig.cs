using System;
using UnityEngine;

namespace Template.Services.Ads
{
    /// <summary>Pure-data ad frequency rules (unit-testable without a ScriptableObject).</summary>
    [Serializable]
    public struct AdRules
    {
        [Tooltip("Interstitials are allowed from this session number on (1 = first launch).")]
        [Min(1)] public int InterstitialMinSession;

        [Tooltip("...or, in earlier sessions, once this many seconds have passed since app start.")]
        [Min(0)] public float InterstitialMinSecondsInSession;

        [Tooltip("Minimum seconds between two interstitials.")]
        [Min(0)] public float InterstitialCooldownSeconds;

        [Tooltip("A completed rewarded ad also restarts the interstitial cooldown (avoids ad-after-ad).")]
        public bool RewardedResetsInterstitialCooldown;

        public static AdRules Default => new AdRules
        {
            InterstitialMinSession = 2,
            InterstitialMinSecondsInSession = 90f,
            InterstitialCooldownSeconds = 45f,
            RewardedResetsInterstitialCooldown = true,
        };
    }

    [Serializable]
    public struct MockAdSettings
    {
        [Min(0)] public float InitDelay;
        [Min(0)] public float LoadDelay;
        [Min(0)] public float ShowDuration;
        [Range(0, 1)] public float InterstitialFillRate;
        [Range(0, 1)] public float RewardedFillRate;
        [Tooltip("Mock consent: does the simulated user grant tracking?")]
        public bool ConsentGranted;
        [Min(0)] public float ConsentDelay;

        public static MockAdSettings Default => new MockAdSettings
        {
            InitDelay = 0.5f,
            LoadDelay = 1f,
            ShowDuration = 2f,
            InterstitialFillRate = 1f,
            RewardedFillRate = 1f,
            ConsentGranted = true,
            ConsentDelay = 0.5f,
        };
    }

    [CreateAssetMenu(menuName = "Template/Configs/Ad Config", fileName = "AdConfig")]
    public sealed class AdConfig : ScriptableObject
    {
        [SerializeField] private AdRules _rules = AdRules.Default;
        [SerializeField] private bool _bannerEnabled;
        [Tooltip("Max wait for consent (UMP form + ATT prompt, user interaction) at boot. On timeout the game continues; ads stay off if consent didn't allow them.")]
        [SerializeField, Min(1)] private float _consentTimeoutSeconds = 90f;
        [Tooltip("Max wait for ad SDK init at boot. On timeout the game continues; ads start once init completes.")]
        [SerializeField, Min(1)] private float _adInitTimeoutSeconds = 15f;
        [Tooltip("Retry delays for failed loads double up to this cap (seconds).")]
        [SerializeField, Min(1)] private float _maxLoadRetryDelay = 64f;
        [SerializeField] private MockAdSettings _mock = MockAdSettings.Default;

        public AdRules Rules => _rules;
        public bool BannerEnabled => _bannerEnabled;
        public float ConsentTimeoutSeconds => _consentTimeoutSeconds;
        public float AdInitTimeoutSeconds => _adInitTimeoutSeconds;
        public float MaxLoadRetryDelay => _maxLoadRetryDelay;
        public MockAdSettings Mock => _mock;
    }
}
