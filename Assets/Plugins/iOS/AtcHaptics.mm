// Haptic feedback for iOS (M2 step 4). Called from C# through AirTrafficControl.Haptics.
// iPads have no haptic engine, so these do nothing there; they work on iPhones.
#import <UIKit/UIKit.h>

extern "C" {

void AtcHapticImpact(int style)
{
    UIImpactFeedbackStyle feedbackStyle = style <= 0 ? UIImpactFeedbackStyleLight
                                        : style == 1 ? UIImpactFeedbackStyleMedium
                                                     : UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:feedbackStyle];
    [generator prepare];
    [generator impactOccurred];
}

void AtcHapticNotification(int type)
{
    UINotificationFeedbackType feedbackType = type <= 0 ? UINotificationFeedbackTypeSuccess
                                            : type == 1 ? UINotificationFeedbackTypeWarning
                                                        : UINotificationFeedbackTypeError;
    UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
    [generator prepare];
    [generator notificationOccurred:feedbackType];
}

}
