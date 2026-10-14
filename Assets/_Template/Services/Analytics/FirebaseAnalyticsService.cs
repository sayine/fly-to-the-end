using System.Collections.Generic;
using UnityEngine;

namespace Template.Services.Analytics
{
    /// <summary>
    /// STUB — real provider implementation. Not active until the SDK is installed and the
    /// scripting define <c>HC_FIREBASE</c> is added (Player Settings > Scripting Define Symbols),
    /// at which point the bootstrapper's ServiceFactory switches to this class on device.
    ///
    /// Integration notes (verify against the current official Firebase Unity docs first):
    ///  1. Download the Firebase Unity SDK, import ONLY FirebaseAnalytics.unitypackage
    ///     (Assets > Import Package). Prefer the External Dependency Manager it ships with.
    ///  2. Add GoogleService-Info.plist to Assets/_Game/Configs/Firebase/.
    ///     It is not secret but it is per-game: never reuse it across games.
    ///  3. Add HC_FIREBASE to Scripting Define Symbols for iOS.
    ///  4. Fill the TODOs below. Map parameter values: int/long -> Parameter(long), float/double -> Parameter(double),
    ///     everything else -> ToString().
    ///  5. Respect consent: if !trackingAllowed call FirebaseAnalytics.SetAnalyticsCollectionEnabled(false)
    ///     or use Consent Mode v2 (SetConsent with AnalyticsStorage/AdStorage = Denied).
    ///  Alternative providers (GameAnalytics, Unity Analytics) can implement IAnalyticsService the same way.
    /// </summary>
    public sealed class FirebaseAnalyticsService : IAnalyticsService
    {
        private bool _ready;
        private readonly Queue<KeyValuePair<string, Dictionary<string, object>>> _pending =
            new Queue<KeyValuePair<string, Dictionary<string, object>>>();

        public void Initialize(bool trackingAllowed)
        {
            // TODO(HC_FIREBASE):
            // Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            //     if (task.Result != Firebase.DependencyStatus.Available) { Debug.LogWarning(...); return; }
            //     Firebase.Analytics.FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            //     Firebase.Analytics.FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus> {
            //         { ConsentType.AnalyticsStorage, trackingAllowed ? ConsentStatus.Granted : ConsentStatus.Denied },
            //         { ConsentType.AdStorage,        trackingAllowed ? ConsentStatus.Granted : ConsentStatus.Denied } });
            //     _ready = true; FlushPending();
            // });
            Debug.LogWarning("[Analytics:Firebase] STUB: SDK not integrated. Events are queued and dropped.");
        }

        public void LogEvent(string name, Dictionary<string, object> parameters = null)
        {
            if (!_ready)
            {
                // Events before init are queued (bounded) and flushed once Firebase is ready.
                if (_pending.Count < 64) _pending.Enqueue(new KeyValuePair<string, Dictionary<string, object>>(name, parameters));
                return;
            }
            // TODO(HC_FIREBASE): convert parameters to Firebase.Analytics.Parameter[] and call
            // Firebase.Analytics.FirebaseAnalytics.LogEvent(name, parameterArray);
        }

        public void SetUserProperty(string name, string value)
        {
            // TODO(HC_FIREBASE): Firebase.Analytics.FirebaseAnalytics.SetUserProperty(name, value);
        }

        // ReSharper disable once UnusedMember.Local — called from the TODO above once integrated.
        private void FlushPending()
        {
            while (_pending.Count > 0)
            {
                var e = _pending.Dequeue();
                LogEvent(e.Key, e.Value);
            }
        }
    }
}
