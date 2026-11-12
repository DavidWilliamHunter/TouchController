using System;
using NUnit.Framework;
using UnityEngine;

namespace Threads.TouchController.Tests
{
    /// <summary>Lifecycle and wiring of the single startup / shutdown point.</summary>
    public class TouchSystemTests
    {
        private FakeTouchHost _host;
        private FakeTouchInput _input;
        private FakeOsTouchConfigProvider _os;
        private TouchSystem _system;

        [SetUp]
        public void SetUp()
        {
            _host = new FakeTouchHost { ScreenDpi = 320f, Time = 10.0 };
            _input = new FakeTouchInput();
            _os = new FakeOsTouchConfigProvider();
        }

        [TearDown]
        public void TearDown() => _system?.Dispose();

        private TouchSystem Create(Action<TouchSystemOptions> configure = null)
        {
            var options = new TouchSystemOptions { Host = _host, Input = _input, OsProvider = _os };
            configure?.Invoke(options);
            return _system = new TouchSystem(options);
        }

        // ---- Construction does nothing -------------------------------------------------------

        [Test]
        public void Constructor_StartsNothing()
        {
            Create();
            Assert.IsFalse(_system.IsRunning);
            Assert.AreEqual(0, _input.EnableCount);
            Assert.AreEqual(0, _os.ReadCount);
        }

        [Test]
        public void BeforeStartup_AccessThrows()
        {
            Create();
            Assert.Throws<InvalidOperationException>(() => _system.Update());
            Assert.Throws<InvalidOperationException>(() => _system.Refresh());
            Assert.Throws<InvalidOperationException>(() => { var _ = _system.Frame; });
            Assert.Throws<InvalidOperationException>(() => { var _ = _system.History; });
            Assert.Throws<InvalidOperationException>(() => { var _ = _system.Environment; });
        }

        // ---- Startup -------------------------------------------------------------------------

        [Test]
        public void Startup_ResolvesSettings_AndEnablesInput()
        {
            Create().Startup();

            Assert.IsTrue(_system.IsRunning);
            Assert.AreEqual(1, _input.EnableCount);
            Assert.AreEqual(1, _os.ReadCount);
            Assert.AreEqual(TouchPlatform.Android, _system.Platform);
            Assert.AreEqual(320f, _system.Environment.Dpi);
            Assert.AreEqual(2f, _system.Environment.PixelsPerDp, 1e-5f);
            Assert.IsEmpty(_system.Settings.Data.Validate());
        }

        [Test]
        public void Startup_Twice_Throws()
        {
            Create().Startup();
            Assert.Throws<InvalidOperationException>(() => _system.Startup());
            Assert.AreEqual(1, _input.EnableCount);
        }

        [Test]
        public void Startup_PlatformFromHost_SelectsDefaultAsset()
        {
            _host.Platform = TouchPlatform.iOS;
            Create().Startup();
            Assert.AreEqual(TouchPlatform.iOS, _system.Platform);
            Assert.AreEqual(163f, _system.Settings.Data.Density.referenceDpi);
        }

        [Test]
        public void Startup_PlatformOption_OverridesHost()
        {
            _host.Platform = TouchPlatform.iOS;
            Create(o => o.Platform = TouchPlatform.Windows).Startup();
            Assert.AreEqual(TouchPlatform.Windows, _system.Platform);
            Assert.AreEqual(96f, _system.Settings.Data.Density.referenceDpi);
        }

        [Test]
        public void Startup_UsesGivenSettingsAsset_WithoutModifyingIt()
        {
            var asset = TestSettings.CopyOfDefault(TouchPlatform.Android);
            try
            {
                asset.Data.Motion.touchSlopDp = 12.5f;
                _os.Values.touchSlopPx = 40f;
                Create(o => o.Settings = asset).Startup();

                Assert.AreSame(asset, _system.Environment.Defaults);
                Assert.AreNotSame(asset, _system.Settings);
                Assert.AreEqual(20f, _system.Settings.Data.Motion.touchSlopDp, 1e-4f); // OS value, 40 px / 2
                Assert.AreEqual(12.5f, asset.Data.Motion.touchSlopDp);
            }
            finally
            {
                _system.Dispose();
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Startup_InputFails_LeavesNothingRunning()
        {
            _input.ThrowOnEnable = true;
            Create();

            Assert.Throws<InvalidOperationException>(() => _system.Startup());

            Assert.IsFalse(_system.IsRunning);
            Assert.AreEqual(0, _input.DisableCount, "Input that never enabled must not be disabled.");
            Assert.Throws<InvalidOperationException>(() => { var _ = _system.Environment; });

            _input.ThrowOnEnable = false;
            _system.Startup(); // can retry cleanly
            Assert.IsTrue(_system.IsRunning);
        }

        [Test]
        public void Options_AreCopied_AtConstruction()
        {
            var options = new TouchSystemOptions { Host = _host, Input = _input, OsProvider = _os, Platform = TouchPlatform.Android };
            _system = new TouchSystem(options);
            options.Platform = TouchPlatform.iOS; // later changes to the options object have no effect

            _system.Startup();

            Assert.AreEqual(TouchPlatform.Android, _system.Platform);
        }

        // ---- Update --------------------------------------------------------------------------

        [Test]
        public void Update_CapturesWithResolvedScale_HostTimeAndScreen()
        {
            Create().Startup();
            _host.Time = 12.5;
            _host.ScreenSizePx = new Vector2(1000, 2000);

            _system.Update();

            Assert.AreEqual(1, _input.CaptureCount);
            Assert.AreEqual(2f, _input.LastPixelsPerDp, 1e-5f);
            Assert.AreEqual(12.5, _input.LastTime);
            Assert.AreEqual(new Vector2(1000, 2000), _input.LastScreenSizePx);
            Assert.AreEqual(12.5, _system.Frame.Time);
            Assert.AreEqual(new Vector2(500, 1000), _system.Frame.ScreenSizeDp);
        }

        [Test]
        public void Update_FeedsHistory()
        {
            Create().Startup();
            for (var i = 0; i <= 5; i++)
            {
                _host.Time = 10.0 + 0.02 * i;
                _input.NextTouches.Clear();
                var phase = i == 0 ? TouchPointPhase.Began : TouchPointPhase.Moved;
                _input.NextTouches.Add(new TouchPoint(1, phase, new Vector2(10 * i, 0), Vector2.zero, 10.0));
                _system.Update();
            }

            Assert.AreEqual(500f, _system.History.GetVelocityDps(1).x, 0.01f);
        }

        // ---- Refresh -------------------------------------------------------------------------

        [Test]
        public void Refresh_RereadsOsAndScreen()
        {
            Create().Startup();
            _os.Values.longPressTimeoutMs = 1500f;
            _host.ScreenDpi = 480f;

            _system.Refresh();

            Assert.AreEqual(1500f, _system.Settings.Data.LongPress.longPressTimeoutMs);
            Assert.AreEqual(480f, _system.Environment.Dpi);
        }

        // ---- Shutdown ------------------------------------------------------------------------

        [Test]
        public void Shutdown_ReleasesEverything()
        {
            Create().Startup();
            var settingsCopy = _system.Settings;

            _system.Shutdown();

            Assert.IsFalse(_system.IsRunning);
            Assert.AreEqual(1, _input.DisableCount);
            Assert.IsFalse(_input.IsEnabled);
            Assert.IsTrue(settingsCopy == null, "Runtime settings copy should be destroyed.");
            Assert.Throws<InvalidOperationException>(() => _system.Update());
        }

        [Test]
        public void Shutdown_IsIdempotent_AndSafeBeforeStartup()
        {
            Create();
            Assert.DoesNotThrow(() => _system.Shutdown());
            _system.Startup();
            _system.Shutdown();
            _system.Shutdown();
            Assert.AreEqual(1, _input.DisableCount);
        }

        [Test]
        public void Dispose_IsShutdown()
        {
            Create().Startup();
            _system.Dispose();
            Assert.IsFalse(_system.IsRunning);
            Assert.AreEqual(1, _input.DisableCount);
        }

        [Test]
        public void CanRestart_AfterShutdown()
        {
            Create().Startup();
            var first = _system.Settings;
            _system.Shutdown();

            _system.Startup();

            Assert.IsTrue(_system.IsRunning);
            Assert.AreEqual(2, _input.EnableCount);
            Assert.AreNotSame(first, _system.Settings);
        }

        [Test]
        public void TwoSystems_AreIndependent()
        {
            // No global state: two systems with different platforms run side by side.
            var other = new TouchSystem(new TouchSystemOptions
            {
                Host = new FakeTouchHost { Platform = TouchPlatform.iOS, ScreenDpi = 326f },
                Input = new FakeTouchInput(),
                OsProvider = new NullOsTouchConfigProvider(),
            });
            try
            {
                Create().Startup();
                other.Startup();

                Assert.AreEqual(TouchPlatform.Android, _system.Platform);
                Assert.AreEqual(TouchPlatform.iOS, other.Platform);
                Assert.AreNotSame(_system.Settings, other.Settings);

                other.Shutdown();
                Assert.IsTrue(_system.IsRunning);
            }
            finally
            {
                other.Dispose();
            }
        }

        // ---- Default host --------------------------------------------------------------------

        [Test]
        public void UnityTouchHost_ReportsPlausibleValues()
        {
            var host = new UnityTouchHost();
            Assert.IsTrue(Enum.IsDefined(typeof(TouchPlatform), host.Platform));
            Assert.GreaterOrEqual(host.Time, 0.0);
            Assert.GreaterOrEqual(host.ScreenSizePx.x, 0f);
        }
    }
}
