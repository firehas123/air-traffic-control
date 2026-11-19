#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace AirTrafficControl
{
    /// <summary>
    /// Vibration feedback (M2 step 4). On iOS it uses the system haptics (Assets/Plugins/iOS/AtcHaptics.mm).
    /// iPads have no haptic engine, so nothing is felt there; iPhones do. Other platforms do nothing for now
    /// (Android comes in M4).
    /// </summary>
    public static class Haptics
    {
        /// <summary>Switch for players who do not want vibration. On by default.</summary>
        public static bool Enabled { get; set; } = true;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void AtcHapticImpact(int style);
        [DllImport("__Internal")] static extern void AtcHapticNotification(int type);
#endif

        /// <summary>A light tap, for a touchdown.</summary>
        public static void Light()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Enabled) AtcHapticImpact(0);
#endif
        }

        /// <summary>A warning buzz, for a danger warning.</summary>
        public static void Warning()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Enabled) AtcHapticNotification(1);
#endif
        }

        /// <summary>A heavy error buzz, for a collision.</summary>
        public static void Error()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Enabled) AtcHapticNotification(2);
#endif
        }
    }
}
