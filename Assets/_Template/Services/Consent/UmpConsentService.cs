#if HC_ADMOB
using System;
using System.Collections;
using System.Collections.Generic;
using GoogleMobileAds.Ump.Api;
using Template.Core;
using UnityEngine;

namespace Template.Services.Consent
{
    /// <summary>
    /// Google UMP consent form (GDPR/US-state regions, configured in AdMob > Privacy &amp; messaging), then the
    /// Apple ATT prompt (Google's recommended order: the optional IDFA explainer message precedes ATT).
    /// Always completes, never throws; SDK callbacks are marshalled to the main thread.
    /// </summary>
    public sealed class UmpConsentService : IConsentService
    {
        private const float AttWaitSeconds = 120f;
        private readonly IAppLifecycle _lifecycle;
        private readonly string _testDeviceHashedId;

        public UmpConsentService(IAppLifecycle lifecycle, string testDeviceHashedId)
        {
            _lifecycle = lifecycle;
            _testDeviceHashedId = testDeviceHashedId;
        }

        public bool IsComplete { get; private set; }
        public bool CanRequestAds => Safe(ConsentInformation.CanRequestAds);
        public bool TrackingAllowed => AttBridge.TrackingAllowed;
        public bool PrivacyOptionsRequired =>
            Safe(() => ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required);

        public void RequestConsent(Action onComplete)
        {
            var request = new ConsentRequestParameters();
            if (Debug.isDebugBuild && !string.IsNullOrEmpty(_testDeviceHashedId))
            {
                // Dev builds on your test device behave as if in the EEA, so the form always appears.
                request.ConsentDebugSettings = new ConsentDebugSettings
                {
                    DebugGeography = DebugGeography.EEA,
                    TestDeviceHashedIds = new List<string> { _testDeviceHashedId },
                };
            }

            try
            {
                ConsentInformation.Update(request, updateError => _lifecycle.RunOnMainThread(() =>
                {
                    if (updateError != null) Debug.LogWarning($"[Consent:UMP] Update failed: {updateError.Message}");
                    ShowFormIfRequired(onComplete);
                }));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Consent:UMP] {e}");
                _lifecycle.Run(RequestAtt(onComplete));
            }
        }

        private void ShowFormIfRequired(Action onComplete)
        {
            try
            {
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => _lifecycle.RunOnMainThread(() =>
                {
                    if (formError != null) Debug.LogWarning($"[Consent:UMP] Form: {formError.Message}");
                    _lifecycle.Run(RequestAtt(onComplete));
                }));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Consent:UMP] {e}");
                _lifecycle.Run(RequestAtt(onComplete));
            }
        }

        private IEnumerator RequestAtt(Action onComplete)
        {
            if (AttBridge.Status == AttStatus.NotDetermined)
            {
                AttBridge.Request();
                var end = Time.realtimeSinceStartup + AttWaitSeconds;
                while (AttBridge.Status == AttStatus.NotDetermined && Time.realtimeSinceStartup < end) yield return null;
            }

            IsComplete = true;
            Debug.Log($"[Consent:UMP] Done. canRequestAds={CanRequestAds} att={AttBridge.Status}");
            onComplete?.Invoke();
        }

        public void ShowPrivacyOptions(Action onClosed)
        {
            try
            {
                ConsentForm.ShowPrivacyOptionsForm(error => _lifecycle.RunOnMainThread(() =>
                {
                    if (error != null) Debug.LogWarning($"[Consent:UMP] Privacy options: {error.Message}");
                    onClosed?.Invoke();
                }));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Consent:UMP] {e}");
                onClosed?.Invoke();
            }
        }

        private static bool Safe(Func<bool> f)
        {
            try { return f(); }
            catch { return false; }
        }
    }
}
#endif
