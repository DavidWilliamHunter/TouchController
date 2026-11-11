using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Decides which <see cref="TouchPlatform"/> conventions apply. Pure functions: callers supply the
    /// runtime platform (see <see cref="UnityTouchHost.Platform"/> for the live value).
    /// </summary>
    public static class PlatformDetector
    {
        /// <summary>
        /// In the Editor the active build target is used (Android when it is not a touch platform);
        /// in a player the runtime platform is used (Android when unknown).
        /// </summary>
        public static TouchPlatform Detect(RuntimePlatform runtimePlatform, TouchPlatform? editorBuildTarget)
        {
            if (IsEditor(runtimePlatform))
                return editorBuildTarget ?? TouchPlatform.Android;
            return FromRuntimePlatform(runtimePlatform) ?? TouchPlatform.Android;
        }

        /// <summary>Maps a player platform to a touch platform, or null if it is not one we support.</summary>
        public static TouchPlatform? FromRuntimePlatform(RuntimePlatform runtimePlatform)
        {
            switch (runtimePlatform)
            {
                case RuntimePlatform.Android: return TouchPlatform.Android;
                case RuntimePlatform.IPhonePlayer: return TouchPlatform.iOS;
                case RuntimePlatform.WindowsPlayer: return TouchPlatform.Windows;
                default: return null;
            }
        }

        private static bool IsEditor(RuntimePlatform runtimePlatform) =>
            runtimePlatform == RuntimePlatform.WindowsEditor ||
            runtimePlatform == RuntimePlatform.OSXEditor ||
            runtimePlatform == RuntimePlatform.LinuxEditor;
    }
}
