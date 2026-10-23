using System.Runtime.InteropServices;
using UnityEngine;

namespace VanishingDays
{
    // Web keyboard inputs live outside the canvas. Install their CSS before the
    // browser focuses them, then restore the viewport after the keyboard animates out.
    public static class MobileInputViewport
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void VD_InstallMobileInputViewport();
        [DllImport("__Internal")] static extern void VD_RestoreMobileInputViewport();
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            VD_InstallMobileInputViewport();
#endif
        }
        public static void Restore()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            VD_RestoreMobileInputViewport();
#endif
        }
    }
}
