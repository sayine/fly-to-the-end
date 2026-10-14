using System.Runtime.InteropServices;

namespace Template.Services.Consent
{
    public enum AttStatus
    {
        NotDetermined = 0,
        Restricted = 1,
        Denied = 2,
        Authorized = 3,
        /// <summary>iOS &lt; 14, Editor, or non-iOS: no ATT prompt exists.</summary>
        Unavailable = 4,
    }

    /// <summary>Apple App Tracking Transparency via Assets/_Template/Plugins/iOS/HCTracking.mm (no SDK).</summary>
    public static class AttBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int HCATT_GetStatus();
        [DllImport("__Internal")] private static extern void HCATT_Request();

        public static AttStatus Status => (AttStatus)HCATT_GetStatus();
        public static void Request() => HCATT_Request();
#else
        public static AttStatus Status => AttStatus.Unavailable;
        public static void Request() { }
#endif

        /// <summary>Personalized tracking allowed (authorized, or the OS has no ATT).</summary>
        public static bool TrackingAllowed => Status == AttStatus.Authorized || Status == AttStatus.Unavailable;
    }
}
