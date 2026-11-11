using NUnit.Framework;
using UnityEngine;

namespace Threads.TouchController.Tests
{
    public class TouchSettingsResolverTests
    {
        private TouchSettings _asset;
        private FakeOsTouchConfigProvider _os;
        private TouchEnvironment _env;

        [SetUp]
        public void SetUp()
        {
            _asset = TestSettings.CopyOfDefault(TouchPlatform.Android);
            _os = new FakeOsTouchConfigProvider();
        }

        [TearDown]
        public void TearDown()
        {
            _env?.Dispose();
            Object.DestroyImmediate(_asset);
        }

        private TouchEnvironment Resolve(float screenDpi = 0f) =>
            _env = TouchSettingsResolver.Resolve(_asset, TouchPlatform.Android, _os, screenDpi);

        // ---- Copy semantics ------------------------------------------------------------------

        [Test]
        public void Resolve_ReturnsCopy_NotTheAsset()
        {
            Resolve();
            Assert.AreNotSame(_asset, _env.Settings);
            Assert.AreSame(_asset, _env.Defaults);
            Assert.AreEqual(TouchPlatform.Android, _env.Platform);
        }

        [Test]
        public void Resolve_DoesNotModifyAsset()
        {
            _os.Values.densityDpi = 480f;
            _os.Values.touchSlopPx = 48f;
            _os.Values.longPressTimeoutMs = 1500f;

            Resolve();

            Assert.AreEqual(8f, _asset.Data.Motion.touchSlopDp);
            Assert.AreEqual(400f, _asset.Data.LongPress.longPressTimeoutMs);
        }

        [Test]
        public void Resolve_NullAsset_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                TouchSettingsResolver.Resolve(null, TouchPlatform.Android, _os, 0f));
        }

        [Test]
        public void Resolve_NullProvider_UsesAssetValues()
        {
            _env = TouchSettingsResolver.Resolve(_asset, TouchPlatform.Android, null, 0f);
            Assert.AreEqual(8f, _env.Settings.Data.Motion.touchSlopDp);
            Assert.IsEmpty(_env.OperatingSystemKeys);
        }

        [Test]
        public void Dispose_DestroysRuntimeCopy()
        {
            Resolve();
            var copy = _env.Settings;
            _env.Dispose();
            Assert.IsTrue(copy == null, "Runtime copy should be destroyed.");
            Assert.Throws<System.ObjectDisposedException>(() => { var _ = _env.Settings; });
            Assert.IsTrue(_asset != null, "The asset must not be destroyed.");
        }

        // ---- Density ---------------------------------------------------------------------------

        [Test]
        public void Density_OsValue_WinsOverScreenDpi()
        {
            _os.Values.densityDpi = 480f;
            Resolve(screenDpi: 401f);
            Assert.AreEqual(480f, _env.Dpi);
            Assert.AreEqual(DensitySource.OperatingSystem, _env.DensitySource);
        }

        [Test]
        public void Density_NoOsValue_UsesScreenDpi()
        {
            Resolve(screenDpi: 401f);
            Assert.AreEqual(401f, _env.Dpi);
            Assert.AreEqual(DensitySource.ScreenDpi, _env.DensitySource);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Density_UnusableScreenDpi_UsesFallback(float screenDpi)
        {
            Resolve(screenDpi);
            Assert.AreEqual(420f, _env.Dpi);
            Assert.AreEqual(DensitySource.Fallback, _env.DensitySource);
        }

        [Test]
        public void Density_UnusableOsValue_IsIgnored()
        {
            _os.Values.densityDpi = 0f;
            Resolve(screenDpi: 401f);
            Assert.AreEqual(DensitySource.ScreenDpi, _env.DensitySource);
        }

        [Test]
        public void Density_TakenFromOs_EvenWhenOsValuesDisabled()
        {
            _asset.Data.Policy.useOsValues = false;
            _os.Values.densityDpi = 480f;
            Resolve();
            Assert.AreEqual(DensitySource.OperatingSystem, _env.DensitySource);
        }

        // ---- OS values -------------------------------------------------------------------------

        [Test]
        public void OsPixels_ConvertedToDp_WithOsDensity()
        {
            _os.Values.densityDpi = 480f; // 3 px per dp at 160 reference
            _os.Values.touchSlopPx = 30f;
            _os.Values.doubleTapSlopPx = 330f;
            _os.Values.edgeSlopPx = 45f;
            _os.Values.minScalingSpanPx = 600f;
            _os.Values.minFlingVelocityPxPerSecond = 150f;
            _os.Values.maxFlingVelocityPxPerSecond = 27000f;

            Resolve();
            var d = _env.Settings.Data;

            Assert.AreEqual(10f, d.Motion.touchSlopDp, 1e-4f);
            Assert.AreEqual(110f, d.Tap.doubleTapSlopDp, 1e-4f);
            Assert.AreEqual(15f, d.Edge.edgeSlopDp, 1e-4f);
            Assert.AreEqual(200f, d.Pinch.minScalingSpanDp, 1e-4f);
            Assert.AreEqual(50f, d.Fling.minFlingVelocityDps, 1e-4f);
            Assert.AreEqual(9000f, d.Fling.maxFlingVelocityDps, 1e-4f);
        }

        [Test]
        public void OsTimes_AppliedDirectly()
        {
            _os.Values.longPressTimeoutMs = 1000f; // e.g. Android "Touch & hold delay: Medium"
            _os.Values.doubleTapTimeoutMs = 350f;

            Resolve();

            Assert.AreEqual(1000f, _env.Settings.Data.LongPress.longPressTimeoutMs);
            Assert.AreEqual(350f, _env.Settings.Data.Tap.doubleTapTimeoutMs);
        }

        [Test]
        public void OsValues_RecordedAsOperatingSystemSource()
        {
            _os.Values.densityDpi = 320f;
            _os.Values.touchSlopPx = 16f;
            _os.Values.longPressTimeoutMs = 500f;

            Resolve();

            Assert.AreEqual(SettingSource.OperatingSystem, _env.GetSource(SettingKeys.TouchSlop));
            Assert.AreEqual(SettingSource.OperatingSystem, _env.GetSource(SettingKeys.LongPressTimeout));
            Assert.AreEqual(SettingSource.Asset, _env.GetSource(SettingKeys.DoubleTapSlop));
            Assert.AreEqual(SettingSource.Asset, _env.GetSource(SettingKeys.MultiFingerTapWindow));
            CollectionAssert.AreEquivalent(new[] { SettingKeys.TouchSlop, SettingKeys.LongPressTimeout }, _env.OperatingSystemKeys);
        }

        [Test]
        public void OsValues_Disabled_AssetValuesKept()
        {
            _asset.Data.Policy.useOsValues = false;
            _os.Values.densityDpi = 480f;
            _os.Values.touchSlopPx = 60f;
            _os.Values.longPressTimeoutMs = 1500f;

            Resolve();

            Assert.AreEqual(8f, _env.Settings.Data.Motion.touchSlopDp);
            Assert.AreEqual(400f, _env.Settings.Data.LongPress.longPressTimeoutMs);
            Assert.IsEmpty(_env.OperatingSystemKeys);
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void OsValues_Unusable_AreIgnored(float bad)
        {
            _os.Values.densityDpi = 480f;
            _os.Values.touchSlopPx = bad;
            _os.Values.longPressTimeoutMs = bad;

            Resolve();

            Assert.AreEqual(8f, _env.Settings.Data.Motion.touchSlopDp);
            Assert.AreEqual(400f, _env.Settings.Data.LongPress.longPressTimeoutMs);
            Assert.IsEmpty(_env.OperatingSystemKeys);
        }

        [Test]
        public void OsPixels_WithoutOsDensity_ConvertedWithResolvedDensity()
        {
            // Pixel values from the OS are converted with whatever density was resolved.
            _os.Values.touchSlopPx = 21f; // 420 fallback dpi -> 2.625 px/dp
            Resolve();
            Assert.AreEqual(8f, _env.Settings.Data.Motion.touchSlopDp, 1e-4f);
        }

        [Test]
        public void ProviderReturningNull_TreatedAsNoValues()
        {
            _os.Values = null;
            Resolve(screenDpi: 401f);
            Assert.AreEqual(DensitySource.ScreenDpi, _env.DensitySource);
            Assert.AreEqual(8f, _env.Settings.Data.Motion.touchSlopDp);
        }

        // ---- Refresh ---------------------------------------------------------------------------

        [Test]
        public void Refresh_PicksUpChangedAccessibilitySetting()
        {
            _os.Values.longPressTimeoutMs = 400f;
            Resolve();

            _os.Values.longPressTimeoutMs = 1500f; // user changed "Touch & hold delay" while away
            TouchSettingsResolver.Refresh(_env, 0f);

            Assert.AreEqual(1500f, _env.Settings.Data.LongPress.longPressTimeoutMs);
            Assert.AreEqual(2, _os.ReadCount);
        }

        [Test]
        public void Refresh_KeepsSectionInstances()
        {
            Resolve();
            var motion = _env.Settings.Data.Motion;
            var longPress = _env.Settings.Data.LongPress;

            TouchSettingsResolver.Refresh(_env, 0f);

            Assert.AreSame(motion, _env.Settings.Data.Motion);
            Assert.AreSame(longPress, _env.Settings.Data.LongPress);
        }

        [Test]
        public void Refresh_ResetsRuntimeEditsToAssetValues()
        {
            Resolve();
            _env.Settings.Data.Motion.touchSlopDp = 99f;

            TouchSettingsResolver.Refresh(_env, 0f);

            Assert.AreEqual(8f, _env.Settings.Data.Motion.touchSlopDp);
        }

        [Test]
        public void Refresh_OsValueRemoved_RevertsToAssetAndSource()
        {
            _os.Values.touchSlopPx = 21f;
            Resolve();
            Assert.AreEqual(SettingSource.OperatingSystem, _env.GetSource(SettingKeys.TouchSlop));

            _os.Values.touchSlopPx = null;
            TouchSettingsResolver.Refresh(_env, 0f);

            Assert.AreEqual(8f, _env.Settings.Data.Motion.touchSlopDp);
            Assert.AreEqual(SettingSource.Asset, _env.GetSource(SettingKeys.TouchSlop));
        }

        [Test]
        public void Refresh_DensityChange_IsApplied()
        {
            Resolve(screenDpi: 320f);
            TouchSettingsResolver.Refresh(_env, 480f);
            Assert.AreEqual(480f, _env.Dpi);
        }
    }
}
