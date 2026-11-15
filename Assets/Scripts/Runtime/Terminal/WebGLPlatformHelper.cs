using UnityEngine;
using UnityEngine.InputSystem;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Platform & browser UX utilities shared by MainMenuController and MissionFlowController
    /// (WebGL exit hiding, first-gesture WebAudio unlock gate, and touch/mobile notice detection).
    /// </summary>
    public static class WebGLPlatformHelper
    {
        public static bool AudioUnlocked { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionFlags()
        {
            AudioUnlocked = false;
        }

        public static bool IsWebGLPlatform
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return Application.platform == RuntimePlatform.WebGLPlayer;
#endif
            }
        }

        public static void MarkAudioUnlocked()
        {
            AudioUnlocked = true;
        }

        public static bool HasUnlockGestureThisFrame()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame)
                return true;

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                return true;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                return true;

            return false;
        }

        public static bool ShouldShowTouchDeviceNotice()
        {
            bool hasTouch = Touchscreen.current != null || Input.touchSupported;
            bool hasKb = Keyboard.current != null;
            bool hasMouse = Mouse.current != null;
            return ShouldShowTouchDeviceNotice(Application.isMobilePlatform, hasTouch, hasKb, hasMouse);
        }

        public static bool ShouldShowTouchDeviceNotice(bool isMobilePlatform, bool hasTouch, bool hasKeyboard, bool hasMouse)
        {
            if (isMobilePlatform)
                return true;
            return hasTouch && (!hasKeyboard || !hasMouse);
        }
    }
}
