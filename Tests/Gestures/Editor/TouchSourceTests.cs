using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Object = UnityEngine.Object;

namespace Threads.TouchController.Tests
{
    /// <summary>
    /// The EnhancedTouch → TouchFrame adapter, against the Input System's simulated touchscreen.
    /// This is the only place the Input System is faked; gesture tests use TouchFrames directly.
    /// </summary>
    public class TouchSourceTests : InputTestFixture
    {
        private static readonly Vector2 ScreenPx = new Vector2(1000, 2000);
        private const float PixelsPerDp = 2f;

        private TouchSource _source;
        private TouchFrame _frame;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Touchscreen>();
            _source = new TouchSource(simulateTouchInEditor: false);
            _source.Enable();
            _frame = new TouchFrame();
        }

        public override void TearDown()
        {
            _source.Dispose();
            base.TearDown();
        }

        private TouchFrame Capture()
        {
            _source.Capture(_frame, PixelsPerDp, currentTime, ScreenPx);
            return _frame;
        }

        private TouchPoint Only(TouchFrame frame)
        {
            Assert.AreEqual(1, frame.Count, "Expected exactly one touch.");
            return frame.Touches[0];
        }

        [Test]
        public void Capture_WhenNotEnabled_Throws()
        {
            var source = new TouchSource(simulateTouchInEditor: false);
            Assert.Throws<InvalidOperationException>(() => source.Capture(_frame, 1f, 0, Vector2.one));
        }

        [Test]
        public void NoTouches_EmptyFrameWithScale()
        {
            InputSystem.Update();
            var frame = Capture();
            Assert.AreEqual(0, frame.Count);
            Assert.AreEqual(PixelsPerDp, frame.PixelsPerDp);
            Assert.AreEqual(new Vector2(500, 1000), frame.ScreenSizeDp);
            Assert.AreEqual(currentTime, frame.Time);
        }

        [Test]
        public void Began_PositionConvertedToDp()
        {
            BeginTouch(1, new Vector2(100, 200));
            var touch = Only(Capture());
            Assert.AreEqual(TouchPointPhase.Began, touch.phase);
            Assert.AreEqual(new Vector2(50, 100), touch.positionDp);
            Assert.AreEqual(new Vector2(50, 100), touch.startPositionDp);
        }

        [Test]
        public void Moved_ThenStationary()
        {
            BeginTouch(1, new Vector2(100, 200));
            Capture();

            MoveTouch(1, new Vector2(140, 200));
            var moved = Only(Capture());
            Assert.AreEqual(TouchPointPhase.Moved, moved.phase);
            Assert.AreEqual(new Vector2(70, 100), moved.positionDp);
            Assert.AreEqual(new Vector2(50, 100), moved.startPositionDp);

            InputSystem.Update();
            Assert.AreEqual(TouchPointPhase.Stationary, Only(Capture()).phase);
        }

        [Test]
        public void Ended_ThenGone()
        {
            BeginTouch(1, new Vector2(100, 200));
            Capture();
            EndTouch(1, new Vector2(100, 200));
            Assert.AreEqual(TouchPointPhase.Ended, Only(Capture()).phase);

            InputSystem.Update();
            Assert.AreEqual(0, Capture().Count);
        }

        [Test]
        public void Canceled_IsReported()
        {
            BeginTouch(1, new Vector2(100, 200));
            Capture();
            CancelTouch(1, new Vector2(100, 200));
            Assert.AreEqual(TouchPointPhase.Canceled, Only(Capture()).phase);
        }

        // ---- Several Input System updates between captures ------------------------------------------------
        // EnhancedTouch reports Began / Moved / Ended only in the update in which they happen. When the Input
        // System updates more than once per frame, TouchSource must still report them.

        [Test]
        public void MissedBegan_FirstSightIsBegan()
        {
            BeginTouch(1, new Vector2(100, 200));
            InputSystem.Update(); // EnhancedTouch now says Stationary
            var touch = Only(Capture());
            Assert.AreEqual(TouchPointPhase.Began, touch.phase);
            Assert.AreEqual(new Vector2(50, 100), touch.startPositionDp);
        }

        [Test]
        public void MissedMoved_PositionChangeIsMoved()
        {
            BeginTouch(1, new Vector2(100, 200));
            Capture();
            MoveTouch(1, new Vector2(140, 200));
            InputSystem.Update();
            var touch = Only(Capture());
            Assert.AreEqual(TouchPointPhase.Moved, touch.phase);
            Assert.AreEqual(new Vector2(70, 100), touch.positionDp);
        }

        [Test]
        public void MissedEnded_VanishedContactEnds()
        {
            BeginTouch(1, new Vector2(100, 200));
            var id = Only(Capture()).touchId;
            MoveTouch(1, new Vector2(140, 200));
            EndTouch(1, new Vector2(140, 200));
            InputSystem.Update(); // the touch is gone from activeTouches
            var touch = Only(Capture());
            Assert.AreEqual(TouchPointPhase.Ended, touch.phase);
            Assert.AreEqual(id, touch.touchId);
            Assert.AreEqual(0, Capture().Count);
        }

        [Test]
        public void BeganAndEndedBetweenCaptures_BeganThenEndedNextFrame()
        {
            Capture();
            BeginTouch(1, new Vector2(100, 200));
            EndTouch(1, new Vector2(100, 200));
            var began = Only(Capture());
            Assert.AreEqual(TouchPointPhase.Began, began.phase, "Never begins and ends in one frame");
            var ended = Only(Capture());
            Assert.AreEqual(TouchPointPhase.Ended, ended.phase);
            Assert.AreEqual(began.touchId, ended.touchId);
            InputSystem.Update();
            Assert.AreEqual(0, Capture().Count);
        }

        [Test]
        public void TwoCapturesInOneInputUpdate_EndedContactIsNotRevived()
        {
            BeginTouch(1, new Vector2(100, 200));
            Capture();
            EndTouch(1, new Vector2(100, 200));
            Assert.AreEqual(TouchPointPhase.Ended, Only(Capture()).phase);
            Assert.AreEqual(0, Capture().Count, "Still listed as Ended by EnhancedTouch, but already reported");
            Assert.AreEqual(0, Capture().Count);
        }

        [Test]
        public void IdReusedBeforeTheOldEndWasSeen_OldEndsNewBegins()
        {
            BeginTouch(1, new Vector2(100, 200));
            var first = Only(Capture()).touchId;
            EndTouch(1, new Vector2(100, 200));
            InputSystem.Update();
            BeginTouch(1, new Vector2(300, 300), time: currentTime + 0.5);
            var frame = Capture();

            Assert.AreEqual(2, frame.Count);
            var old = frame.Touches.Single(t => t.touchId == first);
            var fresh = frame.Touches.Single(t => t.touchId != first);
            Assert.AreEqual(TouchPointPhase.Ended, old.phase);
            Assert.AreEqual(TouchPointPhase.Began, fresh.phase);
        }

        [Test]
        public void TouchId_IsStableForAContact()
        {
            BeginTouch(1, new Vector2(100, 200));
            var id = Only(Capture()).touchId;
            MoveTouch(1, new Vector2(150, 200));
            Assert.AreEqual(id, Only(Capture()).touchId);
            EndTouch(1, new Vector2(150, 200));
            Assert.AreEqual(id, Only(Capture()).touchId);
        }

        [Test]
        public void TouchId_IsNotReused_WhenDeviceReusesId()
        {
            BeginTouch(1, new Vector2(100, 200));
            var first = Only(Capture()).touchId;
            EndTouch(1, new Vector2(100, 200));
            Capture();
            InputSystem.Update();
            Capture();

            BeginTouch(1, new Vector2(300, 300));
            var second = Only(Capture()).touchId;

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void TwoTouches_HaveDistinctIds()
        {
            BeginTouch(1, new Vector2(100, 200));
            BeginTouch(2, new Vector2(300, 400));
            var frame = Capture();
            Assert.AreEqual(2, frame.Count);
            Assert.AreNotEqual(frame.Touches[0].touchId, frame.Touches[1].touchId);
        }

        [Test]
        public void CarriesInputSystemIds_ForUnityApis()
        {
            BeginTouch(5, new Vector2(100, 200));
            var touch = Only(Capture());
            Assert.AreEqual(5, touch.inputTouchId, "Must match TouchControl.touchId so EventSystem.IsPointerOverGameObject works.");
            Assert.AreEqual(Touchscreen.current.deviceId, touch.inputDeviceId);
            Assert.AreSame(Touchscreen.current, InputSystem.GetDeviceById(touch.inputDeviceId));
        }

        [Test]
        public void SameDeviceIdOnTwoScreens_HaveDistinctIds()
        {
            // e.g. a Surface in the Editor: real touchscreen + simulated touchscreen.
            var first = Touchscreen.current;
            var second = InputSystem.AddDevice<Touchscreen>(); // may become Touchscreen.current, so pass screens explicitly
            BeginTouch(1, new Vector2(100, 200), screen: first);
            BeginTouch(1, new Vector2(300, 400), screen: second);

            var frame = Capture();

            Assert.AreEqual(2, frame.Count);
            Assert.AreNotEqual(frame.Touches[0].touchId, frame.Touches[1].touchId);
            Assert.AreEqual(1, frame.Touches[0].inputTouchId);
            Assert.AreEqual(1, frame.Touches[1].inputTouchId);
            Assert.AreNotEqual(frame.Touches[0].inputDeviceId, frame.Touches[1].inputDeviceId);
        }

        [Test]
        public void Disable_ReleasesEnhancedTouch()
        {
            Assert.IsTrue(EnhancedTouchSupport.enabled);
            _source.Disable();
            Assert.IsFalse(EnhancedTouchSupport.enabled);
            Assert.IsFalse(_source.IsEnabled);
        }

        [Test]
        public void Disable_LeavesOtherUsersOfEnhancedTouchEnabled()
        {
            EnhancedTouchSupport.Enable(); // someone else
            _source.Disable();
            Assert.IsTrue(EnhancedTouchSupport.enabled);
            EnhancedTouchSupport.Disable();
        }

        [Test]
        public void EnableTwice_DisableOnce_Releases()
        {
            _source.Enable();
            _source.Disable();
            Assert.IsFalse(EnhancedTouchSupport.enabled);
        }

        // ---- Touch simulation in the Editor (mouse -> touch) -----------------------------------

        private static bool SimulationOn => TouchSimulation.instance != null && TouchSimulation.instance.enabled;

        private static void DestroySimulation()
        {
            // TouchSimulation.Destroy() uses Object.Destroy, which is not allowed in edit mode.
            if (TouchSimulation.instance != null)
                Object.DestroyImmediate(TouchSimulation.instance.gameObject);
        }

        [Test]
        public void Simulation_EnabledBySource_AndReleasedOnDisable()
        {
            DestroySimulation();
            var source = new TouchSource(simulateTouchInEditor: true);
            try
            {
                source.Enable();
                Assert.IsTrue(SimulationOn);

                source.Disable();
                Assert.IsFalse(SimulationOn, "The source enabled it, so it must turn it off again.");
            }
            finally
            {
                source.Dispose();
                DestroySimulation();
            }
        }

        [Test]
        public void Simulation_ReEnabledAfterRestart()
        {
            // Regression: TouchSimulation.Disable() keeps the (disabled) instance; a restart must turn it on again.
            DestroySimulation();
            var source = new TouchSource(simulateTouchInEditor: true);
            try
            {
                source.Enable();
                source.Disable();
                source.Enable();
                Assert.IsTrue(SimulationOn);
            }
            finally
            {
                source.Dispose();
                DestroySimulation();
            }
        }

        [Test]
        public void Simulation_EnabledBySomeoneElse_LeftOn()
        {
            DestroySimulation();
            TouchSimulation.Enable(); // other code owns it
            var source = new TouchSource(simulateTouchInEditor: true);
            try
            {
                source.Enable();
                source.Disable();
                Assert.IsTrue(SimulationOn, "The source didn't enable it, so it must not turn it off.");
            }
            finally
            {
                source.Dispose();
                DestroySimulation();
            }
        }

        [Test]
        public void Simulation_NotRequested_NotEnabled()
        {
            DestroySimulation();
            var source = new TouchSource(simulateTouchInEditor: false);
            try
            {
                source.Enable();
                Assert.IsFalse(SimulationOn);
            }
            finally
            {
                source.Dispose();
            }
        }

        [Test]
        public void TouchSystem_WithDefaultInput_ReadsInputSystemTouches()
        {
            // The default (no Input option) is a TouchSource; startup and shutdown manage EnhancedTouch.
            _source.Disable(); // this fixture's own source; the system must manage EnhancedTouch itself
            var host = new FakeTouchHost { ScreenDpi = 320f, ScreenSizePx = new Vector2(1000, 2000) };
            using (var system = new TouchSystem(new TouchSystemOptions
                   {
                       Host = host,
                       OsProvider = new NullOsTouchConfigProvider(),
                       SimulateTouchInEditor = false,
                   }))
            {
                system.Startup();
                Assert.IsTrue(EnhancedTouchSupport.enabled);

                BeginTouch(3, new Vector2(100, 200));
                host.Time = currentTime;
                system.Update();

                Assert.AreEqual(1, system.Frame.Count);
                Assert.AreEqual(new Vector2(50, 100), system.Frame.Touches[0].positionDp); // 320 dpi / 160 = 2 px per dp
                Assert.AreEqual(3, system.Frame.Touches[0].inputTouchId);

                system.Shutdown();
                Assert.IsFalse(EnhancedTouchSupport.enabled);
            }
        }

        [Test]
        public void FeedsTouchHistory()
        {
            // End-to-end: Input System touches -> TouchFrame -> velocity.
            var history = new TouchHistory(new FlingSettings { velocityWindowMs = 200f });
            BeginTouch(1, new Vector2(0, 0), time: currentTime);
            history.Update(Capture());
            for (var i = 1; i <= 5; i++)
            {
                currentTime += 0.02;
                MoveTouch(1, new Vector2(20 * i, 0)); // 20 px / 20 ms = 1000 px/s = 500 dp/s
                history.Update(Capture());
            }

            var id = Only(_frame).touchId;
            Assert.AreEqual(500f, history.GetVelocityDps(id).x, 1f);
        }
    }
}
