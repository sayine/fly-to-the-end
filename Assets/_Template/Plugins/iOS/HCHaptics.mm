// Minimal iOS haptics bridge used by Template.Services.Haptics.HapticService.
#import <UIKit/UIKit.h>

extern "C" {
    // style: 0 = light, 1 = medium, 2 = heavy (matches HapticType)
    void HCHaptics_Impact(int style)
    {
        UIImpactFeedbackStyle s = style == 0 ? UIImpactFeedbackStyleLight
                                : style == 1 ? UIImpactFeedbackStyleMedium
                                             : UIImpactFeedbackStyleHeavy;
        dispatch_async(dispatch_get_main_queue(), ^{
            UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
            [generator prepare];
            [generator impactOccurred];
        });
    }
}
