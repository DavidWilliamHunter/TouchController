using UnityEngine;

namespace Threads.TouchController.Tests
{
    /// <summary>OS reader whose values are set by the test.</summary>
    internal sealed class FakeOsTouchConfigProvider : IOsTouchConfigProvider
    {
        public OsTouchValues Values = new OsTouchValues();
        public int ReadCount;

        public OsTouchValues Read()
        {
            ReadCount++;
            return Values;
        }
    }

    /// <summary>Host whose platform, screen and clock are set by the test.</summary>
    internal sealed class FakeTouchHost : ITouchHost
    {
        public TouchPlatform Platform { get; set; } = TouchPlatform.Android;
        public float ScreenDpi { get; set; }
        public Vector2 ScreenSizePx { get; set; } = new Vector2(1080, 1920);
        public double Time { get; set; }
    }

    /// <summary>Touch input that records calls and copies a scripted frame into the system's frame.</summary>
    internal sealed class FakeTouchInput : ITouchInput
    {
        public int EnableCount, DisableCount, CaptureCount;
        public bool ThrowOnEnable;
        public float LastPixelsPerDp;
        public double LastTime;
        public Vector2 LastScreenSizePx;

        /// <summary>Touches (in dp) to put in the next captured frame.</summary>
        public readonly System.Collections.Generic.List<TouchPoint> NextTouches = new System.Collections.Generic.List<TouchPoint>();

        public bool IsEnabled => EnableCount > DisableCount;

        public void Enable()
        {
            if (ThrowOnEnable) throw new System.InvalidOperationException("Enable failed (test).");
            EnableCount++;
        }

        public void Disable() => DisableCount++;

        public void Capture(TouchFrame frame, float pixelsPerDp, double time, Vector2 screenSizePx)
        {
            CaptureCount++;
            LastPixelsPerDp = pixelsPerDp;
            LastTime = time;
            LastScreenSizePx = screenSizePx;
            frame.Reset(time, pixelsPerDp, screenSizePx / pixelsPerDp);
            foreach (var touch in NextTouches)
                frame.Add(touch);
        }
    }

    internal static class TestSettings
    {
        /// <summary>An editable copy of a shipped asset (destroy with Object.DestroyImmediate).</summary>
        public static TouchSettings CopyOfDefault(TouchPlatform platform)
        {
            var copy = Object.Instantiate(DefaultTouchSettings.Load(platform));
            copy.hideFlags = HideFlags.DontSave;
            return copy;
        }
    }
}
