using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Threads.TouchController.Testing;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Threads.TouchController.Tests
{
    /// <summary>The gesture lineup in TouchSettings, run by TouchSystem.</summary>
    public class TouchSystemGestureTests
    {
        private TouchSettings _settings;
        private TestTapGesture _tapAsset;
        private TestHoldGesture _holdAsset;
        private FakeTouchHost _host;
        private FakeTouchInput _input;
        private TouchSystem _system;

        [SetUp]
        public void SetUp()
        {
            _settings = TestSettings.CopyOfDefault(TouchPlatform.Android);
            _settings.LineupList.Clear(); // these tests use their own test gestures only
            _tapAsset = ScriptableObject.CreateInstance<TestTapGesture>();
            _tapAsset.name = "Tap";
            _holdAsset = ScriptableObject.CreateInstance<TestHoldGesture>();
            _holdAsset.name = "Hold";
            _host = new FakeTouchHost { ScreenDpi = 160f, Time = 1.0 };
            _input = new FakeTouchInput();
        }

        [TearDown]
        public void TearDown()
        {
            _system?.Dispose();
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_tapAsset);
            Object.DestroyImmediate(_holdAsset);
        }

        private TouchSystem Start(params GestureEntry[] lineup)
        {
            _settings.LineupList.AddRange(lineup);
            _system = new TouchSystem(new TouchSystemOptions
            {
                Settings = _settings,
                Host = _host,
                Input = _input,
                OsProvider = new NullOsTouchConfigProvider(),
            });
            _system.Startup();
            return _system;
        }

        private void Frame(params TouchPoint[] touches)
        {
            _host.Time += 0.02;
            _input.NextTouches.Clear();
            _input.NextTouches.AddRange(touches);
            _system.Update();
        }

        private static TouchPoint Touch(int id, TouchPointPhase phase) =>
            new TouchPoint(id, phase, new Vector2(100, 100), new Vector2(100, 100), 0.0);

        // ---- Runtime copies ------------------------------------------------------------------

        [Test]
        public void Startup_RunsCopiesOfLineupAssets()
        {
            Start(new GestureEntry(_tapAsset));

            var runtime = _system.Get<TestTapGesture>();
            Assert.AreNotSame(_tapAsset, runtime);
            Assert.IsTrue(runtime.IsInitialised);
            Assert.IsFalse(_tapAsset.IsInitialised, "The asset itself must never run.");
            Assert.AreEqual("Tap", runtime.Name);
            Assert.AreEqual(1, _system.Gestures.Count);
        }

        [Test]
        public void RuntimeSettingsLineup_PointsAtRuntimeCopies_AssetUnchanged()
        {
            Start(new GestureEntry(_tapAsset));

            Assert.AreSame(_system.Get<TestTapGesture>(), _system.Settings.Lineup[0].gesture);
            Assert.AreSame(_tapAsset, _settings.Lineup[0].gesture);
        }

        [Test]
        public void Lineup_EnabledFlag_SetsInitialState()
        {
            Start(new GestureEntry(_tapAsset, enabled: false), new GestureEntry(_holdAsset));
            Assert.IsFalse(_system.Get<TestTapGesture>().Enabled);
            Assert.IsTrue(_system.Get<TestHoldGesture>().Enabled);
        }

        [Test]
        public void Update_DrivesGestures_InLineupOrder_WithSharedInstanceIds()
        {
            Start(new GestureEntry(_tapAsset), new GestureEntry(_holdAsset));
            var tapLog = new GestureEventLog(_system.Get<TestTapGesture>());
            var holdLog = new GestureEventLog(_system.Get<TestHoldGesture>());

            Frame(Touch(1, TouchPointPhase.Began));
            Frame(Touch(1, TouchPointPhase.Ended));

            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, tapLog.Entries);
            CollectionAssert.AreEqual(new[] { "Possible#2", "Fail(Released)#2" }, holdLog.Entries);
        }

        [Test]
        public void GestureVelocity_ComesFromSystemHistory()
        {
            Start(new GestureEntry(_holdAsset));
            var log = new GestureEventLog(_system.Get<TestHoldGesture>());
            Frame(new TouchPoint(1, TouchPointPhase.Began, new Vector2(0, 0), Vector2.zero, 1.0));
            for (var i = 1; i <= 25; i++) // hold until active, then move at 500 dp/s
            {
                var x = i > 21 ? (i - 21) * 10f : 0f;
                Frame(new TouchPoint(1, x > 0 ? TouchPointPhase.Moved : TouchPointPhase.Stationary, new Vector2(x, 0), Vector2.zero, 1.0));
            }

            var last = log.Events[log.Events.Count - 1];
            Assert.AreEqual(GestureEventType.Stay, last.Type);
            Assert.Greater(last.VelocityDps.x, 300f);
        }

        // ---- Get / TryGet --------------------------------------------------------------------

        [Test]
        public void Get_MissingType_Throws_TryGetFalse()
        {
            Start(new GestureEntry(_tapAsset));
            Assert.Throws<InvalidOperationException>(() => _system.Get<TestHoldGesture>());
            Assert.IsFalse(_system.TryGet<TestHoldGesture>(out var none));
            Assert.IsNull(none);
        }

        [Test]
        public void Get_ByInterfaceOrBaseType_ReturnsFirst()
        {
            Start(new GestureEntry(_tapAsset), new GestureEntry(_holdAsset));
            Assert.AreSame(_system.Get<TestTapGesture>(), _system.Get<IGesture>());
            Assert.AreSame(_system.Get<TestTapGesture>(), _system.Get<GestureBase>());
        }

        [Test]
        public void Get_BeforeStartup_Throws()
        {
            _system = new TouchSystem();
            Assert.Throws<InvalidOperationException>(() => _system.Get<IGesture>());
        }

        // ---- Add / Remove --------------------------------------------------------------------

        [Test]
        public void Add_CreatesRunningCopy_InLineupAndSettings()
        {
            Start();
            var runtime = _system.Add(_holdAsset);

            Assert.AreNotSame(_holdAsset, runtime);
            Assert.IsTrue(runtime.IsInitialised);
            Assert.AreSame(runtime, _system.Get<TestHoldGesture>());
            Assert.AreSame(runtime, _system.Settings.Lineup[_system.Settings.Lineup.Count - 1].gesture);
            Assert.IsEmpty(_settings.Lineup, "The settings asset must not change.");

            var log = new GestureEventLog(runtime);
            Frame(Touch(1, TouchPointPhase.Began));
            Assert.AreEqual("Possible#1", log.Entries[0]);
        }

        [Test]
        public void Add_RunningGesture_Throws()
        {
            Start(new GestureEntry(_tapAsset));
            Assert.Throws<ArgumentException>(() => _system.Add(_system.Get<TestTapGesture>()));
        }

        [Test]
        public void Remove_DisablesThenDestroys()
        {
            Start(new GestureEntry(_tapAsset));
            var runtime = _system.Get<TestTapGesture>();
            var log = new GestureEventLog(runtime);
            Frame(Touch(1, TouchPointPhase.Began));

            Assert.IsTrue(_system.Remove(runtime));

            CollectionAssert.AreEqual(new[] { "Possible#1", "OnDisabled" }, log.Entries);
            Assert.IsTrue(runtime == null, "Removed gesture should be destroyed.");
            Assert.AreEqual(0, _system.Gestures.Count);
            Assert.AreEqual(0, _system.Settings.Lineup.Count);
            Assert.IsFalse(_system.Remove(runtime));
        }

        [Test]
        public void Remove_FromEventHandler_IsSafe()
        {
            Start(new GestureEntry(_tapAsset), new GestureEntry(_holdAsset));
            var tap = _system.Get<TestTapGesture>();
            var hold = _system.Get<TestHoldGesture>();
            var holdLog = new GestureEventLog(hold);
            tap.Possible += _ => _system.Remove(hold); // remove the *next* gesture mid-update

            Assert.DoesNotThrow(() => Frame(Touch(1, TouchPointPhase.Began)));

            CollectionAssert.AreEqual(new[] { "OnDisabled" }, holdLog.Entries, "A removed gesture must not run.");
            Assert.IsTrue(hold == null, "Destroyed after the update.");
            Assert.AreEqual(1, _system.Gestures.Count);
        }

        // ---- Lifecycle -----------------------------------------------------------------------

        [Test]
        public void Shutdown_DestroysRuntimeGestures_RestartMakesNewOnes()
        {
            Start(new GestureEntry(_tapAsset));
            var first = _system.Get<TestTapGesture>();

            _system.Shutdown();
            Assert.IsTrue(first == null);
            Assert.IsTrue(_tapAsset != null, "The asset must survive.");

            _system.Startup();
            var second = _system.Get<TestTapGesture>();
            Assert.IsTrue(second != null);
            Assert.AreNotSame(first, second);
        }

        [Test]
        public void Refresh_KeepsGestures()
        {
            Start(new GestureEntry(_tapAsset));
            var runtime = _system.Get<TestTapGesture>();
            _system.Refresh();
            Assert.AreSame(runtime, _system.Get<TestTapGesture>());
            Assert.AreSame(runtime, _system.Settings.Lineup[0].gesture);
        }

        [Test]
        public void NullEntry_IsSkippedWithWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Lineup\\[0\\] has no gesture"));
            Start(new GestureEntry(null), new GestureEntry(_tapAsset));
            Assert.AreEqual(1, _system.Gestures.Count);
        }

        // ---- Validation ----------------------------------------------------------------------

        [Test]
        public void ValidateLineup_ReportsMissingAndRepeatedGestures()
        {
            _settings.LineupList.Add(new GestureEntry(_tapAsset));
            _settings.LineupList.Add(null);
            _settings.LineupList.Add(new GestureEntry(_tapAsset));

            var errors = _settings.ValidateLineup();

            Assert.AreEqual(2, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("Lineup[1] has no gesture", errors[0]);
            StringAssert.Contains("Lineup[2] repeats gesture 'Tap'", errors[1]);
        }
    }
}
