using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

namespace Threads.TouchController
{
    /// <summary>
    /// Reads Windows values by calling user32.dll directly (no native plugin needed):
    /// the display-scaling DPI and the double-click time.
    /// Returns empty values when not running on Windows.
    /// </summary>
    /// <remarks>
    /// The touch-specific accessibility settings (press-and-hold time, touch double-tap speed and
    /// spatial tolerance, under HKCU\Software\Microsoft\Wisp\Touch) are not read yet: their value
    /// names and units must first be checked on a Surface (plan.md Q9).
    /// The DPI is only correct if the process is DPI-aware; also to be checked on a Surface.
    /// </remarks>
    public sealed class WindowsOsTouchConfigProvider : IOsTouchConfigProvider
    {
        public OsTouchValues Read()
        {
            var values = new OsTouchValues();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
                return values;

            try
            {
                uint dpi = 0;
                var window = GetActiveWindow();
                if (window != IntPtr.Zero)
                    dpi = GetDpiForWindow(window);
                if (dpi == 0)
                    dpi = GetDpiForSystem();
                if (dpi > 0)
                    values.densityDpi = dpi;
            }
            catch (Exception e) when (e is EntryPointNotFoundException || e is DllNotFoundException)
            {
                // Older Windows without per-window DPI APIs: Screen.dpi / fallback are used instead.
            }

            try
            {
                values.doubleTapTimeoutMs = GetDoubleClickTime();
            }
            catch (Exception e) when (e is EntryPointNotFoundException || e is DllNotFoundException)
            {
            }
#endif
            return values;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
#endif
    }
}
