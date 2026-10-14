using System;
using UnityEngine;

namespace Template.Services.Consent
{
    /// <summary>
    /// STUB — Google UMP + Apple ATT. Activated on device when the scripting define <c>HC_CONSENT</c> is set.
    ///
    /// Integration notes (verify against current official docs before installing anything):
    ///  * Option A (recommended with AppLovin MAX): MAX's built-in "Google UMP Automation"
    ///    (AppLovin Integration Manager > Privacy settings). MAX then shows UMP + ATT itself during
    ///    MaxSdk.InitializeSdk; this class becomes a thin wrapper that reports status and exposes
    ///    MaxSdk.CmpService.ShowCmpForExistingUser for the Settings "Privacy options" button.
    ///    In that case bootstrapper order is still: consent wrapper -> ads init (MAX handles the UI).
    ///  * Option B: Google Mobile Ads Unity plugin's UMP API (GoogleMobileAds.Ump.Api):
    ///      ConsentInformation.Update(new ConsentRequestParameters(), err => {
    ///          ConsentForm.LoadAndShowConsentFormIfRequired(formErr => { ... CanRequestAds = ConsentInformation.CanRequestAds(); }) });
    ///      PrivacyOptionsRequired = ConsentInformation.PrivacyOptionsRequirementStatus == Required
    ///      ShowPrivacyOptions -> ConsentForm.ShowPrivacyOptionsForm(...)
    ///  * ATT (iOS 14.5+): request AFTER UMP (Google's IDFA explainer message can precede it).
    ///    Unity package "com.unity.ads.ios-support" (Unity.Advertisement.IosSupport):
    ///      ATTrackingStatusBinding.RequestAuthorizationTracking(); poll GetAuthorizationTrackingStatus() until != NOT_DETERMINED.
    ///    Info.plist needs NSUserTrackingUsageDescription (MAX's Integration Manager / a PostProcessBuild can add it).
    ///  * Always time out (AdConfig.ConsentTimeoutSeconds is enforced by the bootstrapper) and always call onComplete.
    /// </summary>
    public sealed class UmpAttConsentService : IConsentService
    {
        public bool IsComplete { get; private set; }
        public bool CanRequestAds { get; private set; }
        public bool TrackingAllowed { get; private set; }
        public bool PrivacyOptionsRequired { get; private set; }

        public void RequestConsent(Action onComplete)
        {
            // TODO(HC_CONSENT): 1) UMP update + LoadAndShowConsentFormIfRequired
            // TODO(HC_CONSENT): 2) iOS: ATT request, set TrackingAllowed from status == AUTHORIZED
            // TODO(HC_CONSENT): 3) PrivacyOptionsRequired from UMP
            Debug.LogWarning("[Consent:UMP] STUB: SDK not integrated. Reporting canRequestAds=false.");
            IsComplete = true;
            CanRequestAds = false;
            TrackingAllowed = false;
            onComplete?.Invoke();
        }

        public void ShowPrivacyOptions(Action onClosed)
        {
            // TODO(HC_CONSENT): ConsentForm.ShowPrivacyOptionsForm(err => onClosed?.Invoke());
            //                   or MaxSdk.CmpService.ShowCmpForExistingUser(err => onClosed?.Invoke());
            onClosed?.Invoke();
        }
    }
}
