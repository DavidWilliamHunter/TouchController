using NUnit.Framework;
using UnityEngine;

namespace Threads.TouchController.Tests
{
    public class DensityConversionTests
    {
        private TouchSettings _asset;
        private TouchEnvironment _env;

        [TearDown]
        public void TearDown()
        {
            _env?.Dispose();
            Object.DestroyImmediate(_asset);
        }

        private void ResolveWith(TouchPlatform platform, float dpi)
        {
            _asset = TestSettings.CopyOfDefault(platform);
            var os = new FakeOsTouchConfigProvider();
            os.Values.densityDpi = dpi;
            _env = TouchSettingsResolver.Resolve(_asset, platform, os, 0f);
        }

        [TestCase(TouchPlatform.Android, 160f, 1f)]   // mdpi
        [TestCase(TouchPlatform.Android, 480f, 3f)]   // xxhdpi
        [TestCase(TouchPlatform.iOS, 326f, 2f)]       // @2x iPhone
        [TestCase(TouchPlatform.iOS, 489f, 3f)]       // @3x iPhone
        [TestCase(TouchPlatform.Windows, 96f, 1f)]    // 100% scaling
        [TestCase(TouchPlatform.Windows, 192f, 2f)]   // 200% scaling (Surface)
        public void PixelsPerDp_FollowsPlatformReference(TouchPlatform platform, float dpi, float expected)
        {
            ResolveWith(platform, dpi);
            Assert.AreEqual(expected, _env.PixelsPerDp, 1e-4f);
        }

        [Test]
        public void DpToPx_And_PxToDp_RoundTrip()
        {
            ResolveWith(TouchPlatform.Android, 420f);
            const float dp = 8f;
            var px = _env.DpToPx(dp);
            Assert.AreEqual(21f, px, 1e-4f);
            Assert.AreEqual(dp, _env.PxToDp(px), 1e-4f);
        }

        [Test]
        public void PixelsPerDp_TracksRuntimeReferenceDpi()
        {
            // Derived, not stored: changing the reference changes the conversion immediately.
            ResolveWith(TouchPlatform.Android, 320f);
            _env.Settings.Data.Density.referenceDpi = 80f;
            Assert.AreEqual(4f, _env.PixelsPerDp, 1e-4f);
        }
    }
}
