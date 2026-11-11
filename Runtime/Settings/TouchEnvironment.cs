using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Threads.TouchController
{
    /// <summary>
    /// The resolved, active touch settings for this device: a runtime copy of a settings asset with
    /// OS values applied, plus the screen density. Created by <see cref="TouchSettingsResolver"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Settings"/> is the one set of values in use. Pixel values are derived from it and
    /// the density on demand and are never stored. Dispose to destroy the runtime copy.
    /// </remarks>
    public sealed class TouchEnvironment : IDisposable
    {
        private readonly HashSet<string> _osKeys = new HashSet<string>();
        private TouchSettings _settings;

        internal TouchEnvironment(TouchSettings defaults, TouchSettings settings, TouchPlatform platform, IOsTouchConfigProvider osProvider)
        {
            Defaults = defaults;
            _settings = settings;
            Platform = platform;
            OsProvider = osProvider;
        }

        /// <summary>The asset the runtime copy was made from. Never modified.</summary>
        public TouchSettings Defaults { get; }

        /// <summary>The runtime copy holding the active values.</summary>
        public TouchSettings Settings => _settings != null ? _settings : throw new ObjectDisposedException(nameof(TouchEnvironment));

        public TouchPlatform Platform { get; }

        internal IOsTouchConfigProvider OsProvider { get; }

        /// <summary>The resolved screen density in DPI.</summary>
        public float Dpi { get; private set; }

        /// <summary>Where <see cref="Dpi"/> came from.</summary>
        public DensitySource DensitySource { get; private set; }

        /// <summary>Pixels per density-independent unit (dp / pt / epx).</summary>
        public float PixelsPerDp => Dpi / Settings.Data.Density.referenceDpi;

        public float DpToPx(float dp) => dp * PixelsPerDp;

        public float PxToDp(float px) => px / PixelsPerDp;

        /// <summary>Where the value of the setting named by <paramref name="settingKey"/> (see <see cref="SettingKeys"/>) came from.</summary>
        public SettingSource GetSource(string settingKey) =>
            _osKeys.Contains(settingKey) ? SettingSource.OperatingSystem : SettingSource.Asset;

        /// <summary>The settings whose values currently come from the operating system.</summary>
        public IReadOnlyCollection<string> OperatingSystemKeys => _osKeys;

        internal void SetDensity(float dpi, DensitySource source)
        {
            Dpi = dpi;
            DensitySource = source;
        }

        internal void ClearSources() => _osKeys.Clear();

        internal void MarkFromOperatingSystem(string settingKey) => _osKeys.Add(settingKey);

        public void Dispose()
        {
            if (_settings == null) return;
            if (Application.isPlaying)
                Object.Destroy(_settings);
            else
                Object.DestroyImmediate(_settings);
            _settings = null;
        }
    }
}
