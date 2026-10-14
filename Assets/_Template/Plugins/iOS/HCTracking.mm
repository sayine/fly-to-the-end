// App Tracking Transparency bridge used by Template.Services.Consent.AttBridge.
// Linked against AppTrackingTransparency.framework (set by Tools/Template/Setup).
#import <Foundation/Foundation.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

extern "C" {
    // 0 = not determined, 1 = restricted, 2 = denied, 3 = authorized, 4 = unavailable (iOS < 14)
    int HCATT_GetStatus()
    {
        if (@available(iOS 14, *))
            return (int)[ATTrackingManager trackingAuthorizationStatus];
        return 4;
    }

    void HCATT_Request()
    {
        if (@available(iOS 14, *))
        {
            dispatch_async(dispatch_get_main_queue(), ^{
                [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {}];
            });
        }
    }
}
