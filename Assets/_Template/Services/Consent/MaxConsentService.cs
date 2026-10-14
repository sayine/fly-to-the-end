#if HC_MAX
using System;

namespace Template.Services.Consent
{
    /// <summary>
    /// Consent via MAX's built-in "Terms and Privacy Policy Flow": MAX shows the Google UMP form (GDPR regions)
    /// and the ATT prompt itself during MaxSdk.InitializeSdk, so RequestConsent completes immediately and
    /// the status properties read the live SDK configuration (valid once the ad SDK has initialized).
    /// Enable the flow in AppLovin > Integration Manager > Privacy Settings (see Docs/SDK.md).
    /// </summary>
    public sealed class MaxConsentService : IConsentService
    {
        public bool IsComplete { get; private set; }

        /// <summary>MAX decides internally (UMP canRequestAds) during its own init.</summary>
        public bool CanRequestAds => true;

        public bool TrackingAllowed
        {
            get
            {
                if (!MaxSdk.IsInitialized()) return false;
                var status = MaxSdk.GetSdkConfiguration().AppTrackingStatus;
                return status == MaxSdkBase.AppTrackingStatus.Authorized
                       || status == MaxSdkBase.AppTrackingStatus.Unavailable; // iOS < 14.5
            }
        }

        public bool PrivacyOptionsRequired =>
            MaxSdk.IsInitialized() &&
            MaxSdk.GetSdkConfiguration().ConsentFlowUserGeography == MaxSdkBase.ConsentFlowUserGeography.Gdpr;

        public void RequestConsent(Action onComplete)
        {
            IsComplete = true;
            onComplete?.Invoke();
        }

        public void ShowPrivacyOptions(Action onClosed)
        {
            if (!MaxSdk.IsInitialized())
            {
                onClosed?.Invoke();
                return;
            }
            MaxSdk.CmpService.ShowCmpForExistingUser(error =>
            {
                if (error != null) UnityEngine.Debug.LogWarning($"[Consent:MAX] CMP error: {error.Message}");
                onClosed?.Invoke();
            });
        }
    }
}
#endif
