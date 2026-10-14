using Template.Core;
using Template.Services.Ads;
using Template.Services.Analytics;
using Template.Services.Consent;
using Template.Services.Haptics;
using Template.Services.Save;

namespace Template.Flow
{
    /// <summary>
    /// Picks mock vs real implementations. Real SDK-backed services are used only on device builds
    /// AND when their scripting define is set (added when the SDK is integrated):
    ///   HC_MAX (AppLovin MAX ads + MAX-managed UMP/ATT consent) · HC_FIREBASE (analytics)
    /// The Editor always uses mocks, so the game runs with zero SDKs.
    /// </summary>
    public static class ServiceFactory
    {
        public static IAnalyticsService CreateAnalytics()
        {
#if HC_FIREBASE && !UNITY_EDITOR
            return new FirebaseAnalyticsService();
#else
            return new MockAnalyticsService();
#endif
        }

        public static IConsentService CreateConsent(AdConfig config, IAppLifecycle lifecycle)
        {
#if HC_MAX && !UNITY_EDITOR
            return new MaxConsentService();
#else
            var mock = config != null ? config.Mock : MockAdSettings.Default;
            return new MockConsentService(lifecycle, mock.ConsentDelay, mock.ConsentGranted);
#endif
        }

        public static IAdService CreateAds(AdConfig config, IAppLifecycle lifecycle, IAnalyticsService analytics, int sessionCount)
        {
#if HC_MAX && !UNITY_EDITOR
            return new MaxAdService(config, Secrets.Load(), lifecycle, analytics, sessionCount);
#else
            return new MockAdService(config, lifecycle, analytics, sessionCount);
#endif
        }

        public static IHapticService CreateHaptics(ISaveService save)
        {
#if UNITY_EDITOR
            return new MockHapticService(save);
#else
            return new HapticService(save);
#endif
        }
    }
}
