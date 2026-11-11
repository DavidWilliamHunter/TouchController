using System;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>Loads the platform default settings assets shipped in the package's Resources folder.</summary>
    public static class DefaultTouchSettings
    {
        /// <summary>Folder under <c>Resources</c> holding the shipped assets (namespaced to avoid clashes).</summary>
        public const string ResourceFolder = "Threads.TouchController";

        public static string AssetName(TouchPlatform platform)
        {
            switch (platform)
            {
                case TouchPlatform.Android: return "TouchSettings_Android";
                case TouchPlatform.iOS: return "TouchSettings_iOS";
                case TouchPlatform.Windows: return "TouchSettings_Windows";
                default: throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }
        }

        public static string ResourcePath(TouchPlatform platform) => ResourceFolder + "/" + AssetName(platform);

        /// <summary>
        /// Loads the default asset for <paramref name="platform"/>, falling back to the Android asset.
        /// Do not modify the returned asset; resolve it with <see cref="TouchSettingsResolver"/>.
        /// </summary>
        public static TouchSettings Load(TouchPlatform platform)
        {
            var asset = Resources.Load<TouchSettings>(ResourcePath(platform));
            if (asset == null && platform != TouchPlatform.Android)
                asset = Resources.Load<TouchSettings>(ResourcePath(TouchPlatform.Android));
            if (asset == null)
                throw new InvalidOperationException($"TouchController: default settings not found at Resources/{ResourcePath(platform)}.");
            return asset;
        }
    }
}
