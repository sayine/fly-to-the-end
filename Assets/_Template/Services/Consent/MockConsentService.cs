using System;
using System.Collections;
using Template.Core;
using UnityEngine;

namespace Template.Services.Consent
{
    /// <summary>Editor/default implementation: fake delay, configurable outcome, logs every step.</summary>
    public sealed class MockConsentService : IConsentService
    {
        private readonly IAppLifecycle _lifecycle;
        private readonly float _delay;
        private readonly bool _grant;

        public bool IsComplete { get; private set; }
        public bool CanRequestAds { get; private set; }
        public bool TrackingAllowed { get; private set; }
        public bool PrivacyOptionsRequired => true; // so the Settings button can be tested in the Editor

        public MockConsentService(IAppLifecycle lifecycle, float delaySeconds, bool grant)
        {
            _lifecycle = lifecycle;
            _delay = delaySeconds;
            _grant = grant;
        }

        public void RequestConsent(Action onComplete)
        {
            Debug.Log($"[Consent:Mock] Requesting consent (UMP + ATT simulated, {_delay:0.0}s)...");
            if (_lifecycle == null)
            {
                Complete(onComplete);
                return;
            }
            _lifecycle.Run(Wait(onComplete));
        }

        public void ShowPrivacyOptions(Action onClosed)
        {
            Debug.Log("[Consent:Mock] Privacy options form shown and closed.");
            onClosed?.Invoke();
        }

        private IEnumerator Wait(Action onComplete)
        {
            yield return new WaitForSecondsRealtime(_delay);
            Complete(onComplete);
        }

        private void Complete(Action onComplete)
        {
            IsComplete = true;
            CanRequestAds = true; // UMP allows non-personalized ads even when consent is declined
            TrackingAllowed = _grant;
            Debug.Log($"[Consent:Mock] Done. canRequestAds={CanRequestAds} trackingAllowed={TrackingAllowed}");
            onComplete?.Invoke();
        }
    }
}
