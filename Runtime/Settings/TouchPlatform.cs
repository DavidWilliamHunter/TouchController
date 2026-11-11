namespace Threads.TouchController
{
    /// <summary>
    /// The touch platform whose conventions (units, default thresholds, gesture behaviour) apply.
    /// Unknown platforms are treated as <see cref="Android"/>.
    /// </summary>
    public enum TouchPlatform
    {
        /// <summary>Android. Distances in dp (1/160 inch).</summary>
        Android = 0,

        /// <summary>iPhone / iOS. Distances in points (1/163 inch).</summary>
        iOS = 1,

        /// <summary>Windows touch devices such as Microsoft Surface. Distances in effective pixels (1/96 inch at 100% scaling).</summary>
        Windows = 2,
    }

    /// <summary>Where a resolved setting value came from.</summary>
    public enum SettingSource
    {
        /// <summary>The value comes from the settings asset (a shipped platform default or a custom asset).</summary>
        Asset = 0,

        /// <summary>The value was read from the operating system, e.g. an accessibility setting.</summary>
        OperatingSystem = 1,
    }

    /// <summary>Where the resolved screen density came from.</summary>
    public enum DensitySource
    {
        /// <summary>The operating system's own density convention (Android densityDpi, Windows display scaling).</summary>
        OperatingSystem = 0,

        /// <summary><c>UnityEngine.Screen.dpi</c>.</summary>
        ScreenDpi = 1,

        /// <summary><see cref="DensitySettings.fallbackDpi"/> from the settings asset.</summary>
        Fallback = 2,
    }
}
