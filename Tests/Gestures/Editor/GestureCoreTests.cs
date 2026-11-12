using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Threads.TouchController.Testing;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Threads.TouchController.Tests
{
    /// <summary>GestureBase: the state machine, event guarantees, event data, enable / disable.</summary>
    public class GestureCoreTests
    {
        private static readonly Vector2 A = new Vector2(100, 100);
        private static readonly Vector2 B = new Vector2(200, 100);

        // Android defaults: touch slop 8 dp, long press 400 ms.

        private static void AssertClean(GestureEventLog log) =>
            Assert.IsEmpty(log.Violations, string.Join("\n", log.Violations));

        // ---- State machine -------------------------------------------------------------------

        [Test]
        public void Hold_PossibleEnterStayExit_EnterDrivenByTime()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1" }, h.Log.Entries);

            h.Input.Hold(450, stepMs: 50); // no input changes: Enter comes from time alone
            Assert.AreEqual("Enter#1", h.Log.WithoutStays()[1]);
            Assert.Greater(h.Log.Count(GestureEventType.Stay), 0);

            h.Input.Up(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, h.Log.WithoutStays());
            AssertClean(h.Log);
        }

        [Test]
        public void TimeThreshold_ReachedExactlyByWholeFrames_Counts()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step(10);
            h.Input.Hold(400, stepMs: 400.0 / 24.0); // 24 frames summing to exactly 400 ms (with rounding error)
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1" }, h.Log.Entries);
        }

        [Test]
        public void Stay_NotSentInEnterOrExitFrame()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Step(410); // Enter frame
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1" }, h.Log.Entries);

            h.Input.Step(); // one Stay
            h.Input.Up(1);
            h.Input.Step(); // Exit frame
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Stay#1", "Exit(Completed)#1" }, h.Log.Entries);
        }

        [Test]
        public void Fail_EndsInstance_NoFurtherEvents()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Drag(1, A + new Vector2(30, 0), durationMs: 50);
            h.Input.Hold(500);
            h.Input.Up(1);
            h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible#1", "Fail(Moved)#1" }, h.Log.Entries);
            AssertClean(h.Log);
        }

        [Test]
        public void Discrete_EnterAndExitInSameUpdate()
        {
            using var h = new GestureHarness<TestTapGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Up(1);
            h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, h.Log.Entries);
            Assert.AreEqual(h.Log.Events[1].Time, h.Log.Events[2].Time);
            AssertClean(h.Log);
        }

        [Test]
        public void FinishedInstances_RemovedAfterUpdate()
        {
            using var h = new GestureHarness<TestTapGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            Assert.AreEqual(1, h.Gesture.Instances.Count);
            Assert.IsTrue(h.Gesture.IsPossible);
            Assert.IsFalse(h.Gesture.IsActive);

            h.Input.Up(1);
            h.Input.Step();
            Assert.AreEqual(0, h.Gesture.Instances.Count);
            Assert.IsFalse(h.Gesture.IsPossible);
        }

        [Test]
        public void IsActive_TrueBetweenEnterAndExit()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Hold(420);
            Assert.IsTrue(h.Gesture.IsActive);
            Assert.IsTrue(h.Gesture.Instances[0].IsActive);
            h.Input.Up(1);
            h.Input.Step();
            Assert.IsFalse(h.Gesture.IsActive);
        }

        // ---- OS cancel and lost touches ------------------------------------------------------

        [Test]
        public void OsCancel_FailsPossible()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Cancel(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Fail(Cancelled)#1" }, h.Log.Entries);
        }

        [Test]
        public void OsCancel_ExitsActiveAsCancelled()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Hold(420);
            h.Input.Cancel(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Cancelled)#1" }, h.Log.WithoutStays());
            AssertClean(h.Log);
        }

        [Test]
        public void TouchMissingFromFrame_TreatedAsCancelled()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();

            var empty = new TouchFrame();
            empty.Reset(h.Input.Time + 0.02, 1f, new Vector2(400, 800));
            h.Gesture.UpdateGesture(empty);

            CollectionAssert.AreEqual(new[] { "Possible#1", "Fail(Cancelled)#1" }, h.Log.Entries);
        }

        // ---- Instances -----------------------------------------------------------------------

        [Test]
        public void OneInstancePerFinger_Independent()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Down(2, B);
            h.Input.Step();
            h.Input.Drag(2, B + new Vector2(0, 50), durationMs: 50); // finger 2 fails, finger 1 holds
            h.Input.Hold(420);

            CollectionAssert.AreEqual(new[] { "Possible#1", "Possible#2", "Fail(Moved)#2", "Enter#1" }, h.Log.WithoutStays());
            Assert.AreEqual(1, h.Log.Events[0].FingerId);
            Assert.AreEqual(2, h.Log.Events[1].FingerId);
            AssertClean(h.Log);
        }

        [Test]
        public void InstanceIds_UniqueAcrossGesturesSharingAContext()
        {
            var settings = new TouchSettingsData();
            settings.CopyFrom(DefaultTouchSettings.Load(TouchPlatform.Android).Data);
            var context = new GestureContext(settings, new TouchHistory(settings.Fling));
            var tap = ScriptableObject.CreateInstance<TestTapGesture>();
            var hold = ScriptableObject.CreateInstance<TestHoldGesture>();
            try
            {
                tap.Initialise(context, true);
                hold.Initialise(context, true);
                var tapLog = new GestureEventLog(tap);
                var holdLog = new GestureEventLog(hold);
                var input = new ScriptedTouchInput();
                input.FrameProduced += f => { tap.UpdateGesture(f); hold.UpdateGesture(f); };

                input.Down(1, A);
                input.Step();

                Assert.AreEqual(1, tapLog.Events[0].InstanceId);
                Assert.AreEqual(2, holdLog.Events[0].InstanceId);
            }
            finally
            {
                Object.DestroyImmediate(tap);
                Object.DestroyImmediate(hold);
            }
        }

        [Test]
        public void TouchAlreadyDown_AtStart_IsIgnored()
        {
            using var h = new GestureHarness<TestTapGesture>();
            var frame = new TouchFrame();
            // First frame the gesture sees: touch 5 is already down (Stationary), e.g. app started mid-touch.
            frame.Reset(0.1, 1f, new Vector2(400, 800));
            frame.Add(new TouchPoint(5, TouchPointPhase.Stationary, A, A, 0.0));
            h.Gesture.UpdateGesture(frame);
            frame.Reset(0.2, 1f, new Vector2(400, 800));
            frame.Add(new TouchPoint(5, TouchPointPhase.Ended, A, A, 0.0));
            h.Gesture.UpdateGesture(frame);

            Assert.IsEmpty(h.Log.Entries);
        }

        // ---- Event data ----------------------------------------------------------------------

        [Test]
        public void EventData_PositionsTimesAndIds()
        {
            using var h = new GestureHarness<TestHoldGesture>(pixelsPerDp: 2f);
            h.Input.Down(1, A);
            h.Input.Step(100);
            var possible = h.Log.Events[0];

            Assert.AreEqual(A, possible.PositionDp);
            Assert.AreEqual(A * 2f, possible.PositionPx);
            Assert.AreEqual(Vector2.zero, possible.DeltaDp);
            Assert.AreEqual(1, possible.FingerId);
            Assert.AreEqual(0, possible.InputTouchId, "Scripted touches have no Input System ID.");
            Assert.AreEqual(1, possible.FingerCount);
            Assert.AreEqual(h.Input.Time, possible.Time, 1e-9);
            Assert.AreEqual(possible.Time, possible.StartTime, 1e-9);
            Assert.AreSame(h.Gesture, possible.Gesture);
            Assert.AreEqual("TestHoldGesture #1 Possible", possible.ToString());
        }

        [Test]
        public void EventData_DeltaIsSincePreviousEvent()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Move(1, A + new Vector2(3, 0)); // within slop
            h.Input.Step(410);                       // Enter (moved 3 since Possible)
            h.Input.Move(1, A + new Vector2(10, 0)); // after Enter, movement is allowed
            h.Input.Step();                          // Stay

            var enter = h.Log.Events[1];
            var stay = h.Log.Events[2];
            Assert.AreEqual(GestureEventType.Enter, enter.Type);
            Assert.AreEqual(new Vector2(3, 0), enter.DeltaDp);
            Assert.AreEqual(GestureEventType.Stay, stay.Type);
            Assert.AreEqual(new Vector2(7, 0), stay.DeltaDp);
            Assert.AreEqual(A + new Vector2(10, 0), stay.PositionDp);
        }

        [Test]
        public void EventData_VelocityFromHistory()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Hold(420);
            h.Input.Drag(1, A + new Vector2(100, 0), durationMs: 200, stepMs: 20); // 500 dp/s

            var last = h.Log.Events[h.Log.Events.Count - 1];
            Assert.AreEqual(GestureEventType.Stay, last.Type);
            Assert.AreEqual(500f, last.VelocityDps.x, 1f);
        }

        [Test]
        public void AllFingers_CentroidScaleAndFingerCount()
        {
            using var h = new GestureHarness<TestPairGesture>();
            h.Input.Down(1, new Vector2(100, 100));
            h.Input.Step();
            h.Input.Down(2, new Vector2(200, 100));
            h.Input.Step();

            var possible = h.Log.Events[0];
            Assert.AreEqual("Possible#1", h.Log.Entries[0]);
            Assert.AreEqual(2, possible.FingerCount);
            Assert.AreEqual(new Vector2(150, 100), possible.PositionDp);

            h.Input.Move(2, new Vector2(300, 100)); // spread 50 -> 100
            h.Input.Step();
            var enter = h.Log.Events[1];
            Assert.AreEqual(GestureEventType.Enter, enter.Type);
            Assert.AreEqual(2f, enter.Scale, 1e-4f);
            Assert.AreEqual(2f, enter.ScaleDelta, 1e-4f);
            Assert.AreEqual(new Vector2(200, 100), enter.PositionDp);

            h.Input.Move(2, new Vector2(500, 100)); // spread 100 -> 200
            h.Input.Step();
            var stay = h.Log.Events[2];
            Assert.AreEqual(4f, stay.Scale, 1e-4f);
            Assert.AreEqual(2f, stay.ScaleDelta, 1e-4f);

            h.Input.Up(2);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Stay#1", "Exit(Completed)#1" }, h.Log.Entries);
            AssertClean(h.Log);
        }

        [Test]
        public void AllFingers_LateFingerJoins()
        {
            using var h = new GestureHarness<TestPairGesture>();
            h.Input.Down(1, new Vector2(100, 100));
            h.Input.Down(2, new Vector2(200, 100));
            h.Input.Step();
            h.Input.Down(3, new Vector2(150, 200));
            h.Input.Step();
            Assert.AreEqual(3, h.Gesture.Instances[0].FingerIds.Count);
        }

        // ---- Event guarantees are enforced ---------------------------------------------------

        [TestCase(TestMisuseGesture.Misuse.ExitFromPossible, typeof(InvalidOperationException))]
        [TestCase(TestMisuseGesture.Misuse.EnterTwice, typeof(InvalidOperationException))]
        [TestCase(TestMisuseGesture.Misuse.FailWithCompleted, typeof(ArgumentException))]
        [TestCase(TestMisuseGesture.Misuse.ExitWithMoved, typeof(ArgumentException))]
        [TestCase(TestMisuseGesture.Misuse.IneligibleFinger, typeof(ArgumentException))]
        [TestCase(TestMisuseGesture.Misuse.EmptyFingers, typeof(ArgumentException))]
        public void IllegalTransition_Throws(TestMisuseGesture.Misuse misuse, Type expected)
        {
            var asset = ScriptableObject.CreateInstance<TestMisuseGesture>();
            asset.Kind = misuse;
            try
            {
                using var h = new GestureHarness<TestMisuseGesture>(asset);
                h.Gesture.Kind = misuse; // Kind is not serialised; set it on the runtime copy too
                h.Input.Down(1, A);
                Assert.Throws(expected, () => h.Input.Step());
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TransitionOutsideOnFrame_Throws()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Hold(420);
            Assert.Throws<InvalidOperationException>(() => h.Gesture.ExitOutsideFrame(h.Gesture.Instances[0]));
        }

        [Test]
        public void Initialise_Twice_Throws()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            Assert.Throws<InvalidOperationException>(() => h.Gesture.Initialise(h.Context, true));
        }

        [Test]
        public void Enabled_OnUninitialisedAsset_Throws()
        {
            var asset = ScriptableObject.CreateInstance<TestHoldGesture>();
            try
            {
                Assert.IsFalse(asset.IsInitialised);
                Assert.Throws<InvalidOperationException>(() => asset.Enabled = false);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void HandlerException_IsLogged_AndRecognitionContinues()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Gesture.Enter += _ => throw new Exception("boom");
            h.Input.Down(1, A);
            h.Input.Step();

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            h.Input.Hold(450);
            h.Input.Up(1);
            h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, h.Log.WithoutStays());
        }

        [Test]
        public void HandlerException_DoesNotStopLaterHandlers()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            var later = 0;
            Action<GestureEventData> thrower = _ => throw new Exception("boom");
            h.Gesture.Enter += thrower;
            h.Gesture.Enter += _ => later++;
            h.Gesture.OnDisabled += _ => throw new Exception("disabled boom");
            h.Gesture.OnDisabled += _ => later++;
            h.Input.Down(1, A);
            h.Input.Step();

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            h.Input.Hold(450);
            Assert.AreEqual(1, later, "The handler after the throwing one still runs");

            LogAssert.Expect(LogType.Exception, new Regex("disabled boom"));
            h.Gesture.Enabled = false;
            Assert.AreEqual(2, later);

            h.Gesture.Enter -= thrower; // unsubscribing still works
            h.Gesture.Enabled = true;
            h.Input.Up(1);
            h.Input.Step();
            h.Input.Down(2, A);
            h.Input.Step();
            h.Input.Hold(450);
            Assert.AreEqual(3, later);
        }

        // ---- Enable / disable (rule 8) -------------------------------------------------------

        [Test]
        public void DisableFromAHandler_LaterHandlersGetOnlyOnDisabled()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            var later = new System.Collections.Generic.List<string>();
            h.Gesture.Enter += _ => h.Gesture.Enabled = false;   // first handler disables
            h.Gesture.Enter += e => later.Add("Enter");
            h.Gesture.OnDisabled += _ => later.Add("OnDisabled");
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Hold(450);

            CollectionAssert.AreEqual(new[] { "OnDisabled" }, later, "Never an Enter for an instance already discarded");
        }


        [Test]
        public void Disable_WhileActive_OnlyOnDisabled_NoExit()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Hold(420);

            h.Gesture.Enabled = false;

            Assert.AreEqual(GestureRunState.Disabled, h.Gesture.RunState);
            Assert.AreEqual(0, h.Gesture.Instances.Count);
            Assert.IsFalse(h.Gesture.IsActive);
            Assert.AreEqual(1, h.Gesture.DiscardCount);

            h.Input.Up(1);
            h.Input.Step();
            h.Input.Down(2, A);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "OnDisabled" }, h.Log.WithoutStays());
            AssertClean(h.Log);
        }

        [Test]
        public void Disabled_IsSilent()
        {
            using var h = new GestureHarness<TestTapGesture>(enabled: false);
            h.Input.Down(1, A);
            h.Input.Step();
            h.Input.Up(1);
            h.Input.Step();
            Assert.IsEmpty(h.Log.Entries);
            Assert.AreEqual(GestureRunState.Disabled, h.Gesture.RunState);
        }

        [Test]
        public void Enable_WithFingerDown_WaitsUntilScreenClear()
        {
            using var h = new GestureHarness<TestTapGesture>(enabled: false);
            h.Input.Down(1, A);
            h.Input.Step();

            h.Gesture.Enabled = true;
            Assert.AreEqual(GestureRunState.WaitingForClear, h.Gesture.RunState);

            h.Input.Down(2, B);  // a new finger while finger 1 is still down: still waiting (strict rule)
            h.Input.Step();
            h.Input.Up(2);
            h.Input.Step();
            Assert.AreEqual(GestureRunState.WaitingForClear, h.Gesture.RunState);

            h.Input.Up(1);       // screen clear
            h.Input.Step();
            Assert.AreEqual(GestureRunState.Running, h.Gesture.RunState);

            h.Input.Down(3, A);  // a new touch sequence
            h.Input.Step();
            h.Input.Up(3);
            h.Input.Step();

            CollectionAssert.AreEqual(new[] { "OnEnabled", "Possible#1", "Enter#1", "Exit(Completed)#1" }, h.Log.Entries);
            AssertClean(h.Log);
        }

        [Test]
        public void Enable_WithScreenClear_RunsImmediately()
        {
            using var h = new GestureHarness<TestTapGesture>(enabled: false);
            h.Input.Step(); // no fingers

            h.Gesture.Enabled = true;
            Assert.AreEqual(GestureRunState.Running, h.Gesture.RunState);

            h.Input.Down(1, A); // touch begins in the very next frame: recognised
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "OnEnabled", "Possible#1" }, h.Log.Entries);
        }

        [Test]
        public void Enable_ResetsGestureState()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Gesture.Enabled = false;
            h.Gesture.Enabled = true;
            Assert.AreEqual(2, h.Gesture.DiscardCount);
            CollectionAssert.AreEqual(new[] { "OnDisabled", "OnEnabled" }, h.Log.Entries);
        }

        [Test]
        public void SettingEnabledToSameValue_SendsNothing()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Gesture.Enabled = true;
            Assert.IsEmpty(h.Log.Entries);
        }

        [Test]
        public void Disable_FromEventHandler_StopsEventsImmediately()
        {
            using var h = new GestureHarness<TestHoldGesture>();
            h.Gesture.Possible += e =>
            {
                if (e.FingerId == 1) h.Gesture.Enabled = false; // during processing
            };
            h.Input.Down(1, A);
            h.Input.Down(2, B); // its Possible comes after the disable in the same update: suppressed
            h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible#1", "OnDisabled" }, h.Log.Entries);
            Assert.AreEqual(0, h.Gesture.Instances.Count);
            AssertClean(h.Log);
        }
    }
}
