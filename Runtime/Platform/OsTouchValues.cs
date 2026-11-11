namespace Threads.TouchController
{
    /// <summary>
    /// Raw values read from the operating system, in the OS's own units (pixels, milliseconds).
    /// A null field means the OS did not provide that value. <see cref="TouchSettingsResolver"/>
    /// converts these to density-independent units and ignores values that are not positive.
    /// </summary>
    public sealed class OsTouchValues
    {
        /// <summary>The OS density convention as DPI (Android densityDpi; Windows display-scaling DPI).</summary>
        public float? densityDpi;

        public float? touchSlopPx;
        public float? doubleTapSlopPx;
        public float? edgeSlopPx;
        public float? minScalingSpanPx;
        public float? minFlingVelocityPxPerSecond;
        public float? maxFlingVelocityPxPerSecond;

        public float? longPressTimeoutMs;
        public float? doubleTapTimeoutMs;
    }

    /// <summary>Reads touch-related values from the operating system.</summary>
    public interface IOsTouchConfigProvider
    {
        /// <summary>
        /// Reads the current values. Must not throw; fields it cannot read are left null.
        /// Called again on refresh, so accessibility changes made while the app runs are picked up.
        /// </summary>
        OsTouchValues Read();
    }

    /// <summary>Provides nothing; used where the OS exposes no values (iOS for now) and as a safe default.</summary>
    public sealed class NullOsTouchConfigProvider : IOsTouchConfigProvider
    {
        public OsTouchValues Read() => new OsTouchValues();
    }

    /// <summary>Chooses the OS reader for a platform.</summary>
    public static class OsTouchConfigProviders
    {
        public static IOsTouchConfigProvider ForPlatform(TouchPlatform platform)
        {
            switch (platform)
            {
                case TouchPlatform.Android: return new AndroidOsTouchConfigProvider();
                case TouchPlatform.Windows: return new WindowsOsTouchConfigProvider();
                default: return new NullOsTouchConfigProvider();
            }
        }
    }
}
