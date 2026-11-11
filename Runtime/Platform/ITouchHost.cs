using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Threads.TouchController
{
    /// <summary>
    /// The environment the touch system runs in: platform, screen and clock. The only place the
    /// package reads Unity's global state, so it can be replaced (tests, recordings, custom hosts).
    /// </summary>
    public interface ITouchHost
    {
        /// <summary>The platform whose conventions apply.</summary>
        TouchPlatform Platform { get; }

        /// <summary>Screen DPI as reported by the engine; 0 or less if unknown.</summary>
        float ScreenDpi { get; }

        /// <summary>Screen size in pixels.</summary>
        Vector2 ScreenSizePx { get; }

        /// <summary>Current time in seconds, on the same clock as touch timestamps.</summary>
        double Time { get; }
    }

    /// <summary>The default host: reads Unity's <c>Application</c>, <c>Screen</c> and the Input System clock.</summary>
    public sealed class UnityTouchHost : ITouchHost
    {
        /// <summary>The running player's platform, or the active build target in the Editor (see <see cref="PlatformDetector"/>).</summary>
        public TouchPlatform Platform => PlatformDetector.Detect(Application.platform, EditorBuildTarget());

        public float ScreenDpi => Screen.dpi;

        public Vector2 ScreenSizePx => new Vector2(Screen.width, Screen.height);

        public double Time => InputState.currentTime;

        private static TouchPlatform? EditorBuildTarget()
        {
#if UNITY_EDITOR
            switch (UnityEditor.EditorUserBuildSettings.activeBuildTarget)
            {
                case UnityEditor.BuildTarget.Android: return TouchPlatform.Android;
                case UnityEditor.BuildTarget.iOS: return TouchPlatform.iOS;
                case UnityEditor.BuildTarget.StandaloneWindows:
                case UnityEditor.BuildTarget.StandaloneWindows64: return TouchPlatform.Windows;
                default: return null;
            }
#else
            return null;
#endif
        }
    }
}
