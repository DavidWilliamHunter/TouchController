using System.Linq;
using NUnit.Framework;
using Threads.TouchController.Testing;
using UnityEngine;
using static Threads.TouchController.Tests.SingleFingerGestureTests;

namespace Threads.TouchController.Tests
{
    /// <summary>
    /// The three double taps, with Android thresholds: time-out 300 ms, minimum gap 40 ms,
    /// distance 100 dp, slop 8 dp, long press 400 ms.
    /// </summary>
    public class DoubleTapGestureTests
    {
        private static void Tap(ScriptedTouchInput input, int id, Vector2 at)
        {
            input.Down(id, at);
            input.Step();
            input.Up(id);
            input.Step();
        }

        // ---- Android: Enter on the second finger down -----------------------------------------

        [Test]
        public void Android_EntersOnSecondDown_ExitsOnSecondUp()
        {
            using var h = new GestureHarness<AndroidDoubleTap>();
            Tap(h.Input, 1, P);
            h.Input.Step(100);
            h.Input.Down(2, P + new Vector2(10, 0));
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1" }, h.Log.Entries);
            Assert.AreEqual(2, h.Log.Events[1].TapCount);

            h.Input.Up(2);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, h.Log.Entries);
            AssertClean(h.Log);
        }

        [Test]
        public void Android_HeldSecondFinger_StaysActive_DoubleTapDrag()
        {
            using var h = new GestureHarness<AndroidDoubleTap>();
            Tap(h.Input, 1, P);
            h.Input.Down(2, P);
            h.Input.Step(50);
            h.Input.Drag(2, P + new Vector2(0, 150), durationMs: 600); // well past every time-out
            Assert.IsTrue(h.Gesture.IsActive, "No time-out once entered: the held finger keeps it open.");
            Assert.AreEqual(P + new Vector2(0, 150), h.Log.Events.Last().PositionDp);

            h.Input.Up(2);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
        }

        [Test]
        public void NoSecondTap_FailsTimeout_WithoutInput()
        {
            using var h = new GestureHarness<AndroidDoubleTap>();
            Tap(h.Input, 1, P);
            var firstUp = h.Input.Time;
            h.Input.Hold(350); // nothing touches the screen

            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Timeout)" }, Kinds(h.Log));
            Assert.GreaterOrEqual((h.Log.Events[1].Time - firstUp) * 1000.0, 300.0 - 1e-6, "The single-tap-confirmed signal comes after the time-out.");
        }

        [Test]
        public void SecondTapTooFar_FailsTooFar_AndStartsANewOne()
        {
            using var h = new GestureHarness<AndroidDoubleTap>();
            Tap(h.Input, 1, P);
            h.Input.Down(2, P + new Vector2(150, 0));
            h.Input.Step(50);
            CollectionAssert.AreEqual(new[] { "Possible#1", "Fail(TooFar)#1", "Possible#2" }, h.Log.Entries);
        }

        [Test]
        public void SecondTapTooFast_FailsTooFast()
        {
            using var h = new GestureHarness<AndroidDoubleTap>();
            Tap(h.Input, 1, P);
            h.Input.Down(2, P);
            h.Input.Step(10); // 10 ms after the first finger lifted: < 40 ms
            CollectionAssert.AreEqual(new[] { "Possible#1", "Fail(TooFast)#1", "Possible#2" }, h.Log.Entries);
        }

        [Test]
        public void FirstTapMoved_FailsMoved()
        {
            using var h = new GestureHarness<AndroidDoubleTap>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Drag(1, P + new Vector2(20, 0), durationMs: 50);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Moved)" }, Kinds(h.Log));
        }

        [Test]
        public void FirstTapHeld_FailsHeld()
        {
            using var h = new GestureHarness<AndroidDoubleTap>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Hold(450); // a long press can never start a double tap
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Held)" }, Kinds(h.Log));
        }

        [Test]
        public void AnotherFinger_FailsFingerCount()
        {
            using var h = new GestureHarness<AndroidDoubleTap>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Down(2, P + new Vector2(50, 0));
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Fail(FingerCount)#1" }, h.Log.Entries);
        }

        [Test]
        public void TwoFingersTogether_NoDoubleTap()
        {
            using var h = new GestureHarness<AndroidDoubleTap>();
            h.Input.Down(1, P);
            h.Input.Down(2, P + new Vector2(50, 0));
            h.Input.Step();
            Assert.IsEmpty(h.Log.Entries);
        }

        // ---- iOS and Windows: recognised on the second finger up -----------------------------

        [Test]
        public void Ios_RecognisedOnSecondUp()
        {
            using var h = new GestureHarness<IosDoubleTap>();
            Tap(h.Input, 1, P);
            h.Input.Down(2, P);
            h.Input.Step(100);
            CollectionAssert.AreEqual(new[] { "Possible#1" }, h.Log.Entries, "Not yet: iOS waits for the second finger to lift.");

            h.Input.Up(2);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, h.Log.Entries);
            Assert.AreEqual(h.Log.Events[1].Time, h.Log.Events[2].Time);
            Assert.AreEqual(2, h.Log.Events[2].TapCount);
            AssertClean(h.Log);
        }

        [Test]
        public void Ios_SecondTapHeld_FailsHeld()
        {
            using var h = new GestureHarness<IosDoubleTap>();
            Tap(h.Input, 1, P);
            h.Input.Down(2, P);
            h.Input.Step(100);
            h.Input.Hold(450);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Held)" }, Kinds(h.Log));
        }

        [Test]
        public void Ios_SecondTapMoved_FailsMoved()
        {
            using var h = new GestureHarness<IosDoubleTap>();
            Tap(h.Input, 1, P);
            h.Input.Down(2, P);
            h.Input.Step(100);
            h.Input.Drag(2, P + new Vector2(30, 0), durationMs: 50);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Moved)" }, Kinds(h.Log));
        }

        [Test]
        public void Windows_BehavesLikeIos_ForNow()
        {
            using var h = new GestureHarness<WindowsDoubleTap>();
            Tap(h.Input, 1, P);
            h.Input.Down(2, P);
            h.Input.Step(100);
            Assert.AreEqual(1, h.Log.Entries.Count);
            h.Input.Up(2);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
        }

        [Test]
        public void TimeoutFollowsSettings()
        {
            using var h = new GestureHarness<IosDoubleTap>();
            h.Settings.Tap.doubleTapTimeoutMs = 500f;
            Tap(h.Input, 1, P);
            h.Input.Hold(400);
            Assert.IsTrue(h.Gesture.IsPossible, "Still waiting: the time-out is 500 ms.");
            h.Input.Down(2, P);
            h.Input.Step();
            h.Input.Up(2);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
        }
    }
}
