using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Threads.TouchController
{
    /// <summary>
    /// Produces the active settings for this device: copies a settings asset, resolves the screen
    /// density, and (if <see cref="PolicySettings.useOsValues"/>) replaces asset values with the
    /// operating system's values.
    /// </summary>
    /// <remarks>
    /// Density order: OS convention, then <c>Screen.dpi</c>, then <see cref="DensitySettings.fallbackDpi"/>.
    /// Density is always taken from the OS when available, whatever the policy, because it describes
    /// the hardware rather than a preference.
    /// Stateless: everything it uses is passed in. Normally called by <see cref="TouchSystem"/>.
    /// </remarks>
    public static class TouchSettingsResolver
    {
        /// <summary>
        /// Resolves settings from <paramref name="settingsAsset"/>. The asset is copied and never modified.
        /// </summary>
        /// <param name="screenDpi">Usually <c>Screen.dpi</c>; 0 or less means unknown.</param>
        public static TouchEnvironment Resolve(TouchSettings settingsAsset, TouchPlatform platform, IOsTouchConfigProvider osProvider, float screenDpi)
        {
            if (settingsAsset == null) throw new ArgumentNullException(nameof(settingsAsset));

            var copy = Object.Instantiate(settingsAsset);
            copy.name = settingsAsset.name + " (Runtime)";
            copy.hideFlags = HideFlags.DontSave;

            var environment = new TouchEnvironment(settingsAsset, copy, platform, osProvider ?? new NullOsTouchConfigProvider());
            Refresh(environment, screenDpi);
            return environment;
        }

        /// <summary>
        /// Re-reads the OS values in place, e.g. when the app regains focus after the user changed an
        /// accessibility setting. Values are reset to the asset's first, so runtime edits are discarded.
        /// Section instances are kept, so references held by gestures stay valid.
        /// </summary>
        public static void Refresh(TouchEnvironment environment, float screenDpi)
        {
            if (environment == null) throw new ArgumentNullException(nameof(environment));

            var data = environment.Settings.Data;
            data.CopyFrom(environment.Defaults.Data);
            environment.ClearSources();

            var os = environment.OsProvider.Read() ?? new OsTouchValues();

            if (IsUsable(os.densityDpi))
                environment.SetDensity(os.densityDpi.Value, DensitySource.OperatingSystem);
            else if (IsUsable(screenDpi))
                environment.SetDensity(screenDpi, DensitySource.ScreenDpi);
            else
                environment.SetDensity(data.Density.fallbackDpi, DensitySource.Fallback);

            if (!data.Policy.useOsValues)
                return;

            var pixelsPerDp = environment.PixelsPerDp;

            ApplyPixels(environment, os.touchSlopPx, pixelsPerDp, SettingKeys.TouchSlop, v => data.Motion.touchSlopDp = v);
            ApplyPixels(environment, os.doubleTapSlopPx, pixelsPerDp, SettingKeys.DoubleTapSlop, v => data.Tap.doubleTapSlopDp = v);
            ApplyPixels(environment, os.edgeSlopPx, pixelsPerDp, SettingKeys.EdgeSlop, v => data.Edge.edgeSlopDp = v);
            ApplyPixels(environment, os.minScalingSpanPx, pixelsPerDp, SettingKeys.MinScalingSpan, v => data.Pinch.minScalingSpanDp = v);
            ApplyPixels(environment, os.minFlingVelocityPxPerSecond, pixelsPerDp, SettingKeys.MinFlingVelocity, v => data.Fling.minFlingVelocityDps = v);
            ApplyPixels(environment, os.maxFlingVelocityPxPerSecond, pixelsPerDp, SettingKeys.MaxFlingVelocity, v => data.Fling.maxFlingVelocityDps = v);

            Apply(environment, os.longPressTimeoutMs, SettingKeys.LongPressTimeout, v => data.LongPress.longPressTimeoutMs = v);
            Apply(environment, os.doubleTapTimeoutMs, SettingKeys.DoubleTapTimeout, v => data.Tap.doubleTapTimeoutMs = v);
        }

        private static void ApplyPixels(TouchEnvironment environment, float? pixels, float pixelsPerDp, string key, Action<float> set)
        {
            if (!IsUsable(pixels) || !IsUsable(pixelsPerDp)) return;
            set(pixels.Value / pixelsPerDp);
            environment.MarkFromOperatingSystem(key);
        }

        private static void Apply(TouchEnvironment environment, float? value, string key, Action<float> set)
        {
            if (!IsUsable(value)) return;
            set(value.Value);
            environment.MarkFromOperatingSystem(key);
        }

        private static bool IsUsable(float? value) =>
            value.HasValue && value.Value > 0f && !float.IsInfinity(value.Value);
    }
}
