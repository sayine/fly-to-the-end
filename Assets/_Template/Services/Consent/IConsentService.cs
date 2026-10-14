using System;

namespace Template.Services.Consent
{
    /// <summary>
    /// GDPR/US-state consent (Google UMP) + Apple ATT. Must complete before ads are initialized.
    /// Implementations must ALWAYS invoke the callback (also on error), never block the game.
    /// </summary>
    public interface IConsentService
    {
        bool IsComplete { get; }

        /// <summary>UMP canRequestAds(). If false, do not initialize the ad SDK.</summary>
        bool CanRequestAds { get; }

        /// <summary>True when personalized tracking is allowed (ATT authorized on iOS / consent given).</summary>
        bool TrackingAllowed { get; }

        /// <summary>UMP requires a "Privacy options" entry point in Settings when this is true.</summary>
        bool PrivacyOptionsRequired { get; }

        void RequestConsent(Action onComplete);
        void ShowPrivacyOptions(Action onClosed);
    }
}
