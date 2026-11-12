using System.Linq;
using NUnit.Framework;
using Threads.TouchController.Testing;
using UnityEngine;
using static Threads.TouchController.Tests.SingleFingerGestureTests;

namespace Threads.TouchController.Tests
{
    /// <summary>
    /// Pinch, Rotate, TwoFingerPan and TwoFingerTap with Android thresholds: slop 8 dp, minimum pinch
    /// span 170 dp, rotation 5°, two-finger tap window 150 ms, long press 400 ms.
    /// Span = twice the mean distance of the fingers from their centroid, as Android (for two fingers: their distance apart).
    /// </summary>
    public class MultiFingerGestureTests
    {
        private static void Two(ScriptedTouchInput input, Vector2 a, Vector2 b)
        {
            input.Down(1, a);
            input.Down(2, b);
            input.Step();
        }

        // ---- Pinch ---------------------------------------------------------------------------

        [Test]
        public void Pinch_Spread_EntersAndReportsScale()
        {
            using var h = new GestureHarness<PinchGesture>();
            Two(h.Input, new Vector2(0, 400), new Vector2(400, 400)); // span 400
            Assert.AreEqual(2, h.Log.Events[0].FingerCount);

            h.Input.Move(2, new Vector2(410, 400)); // span 410: changed 10, not more than 2 × slop (16)
            h.Input.Step();
            Assert.AreEqual(1, h.Log.Events.Count, "Still Possible");

            h.Input.Move(2, new Vector2(420, 400)); // span 420: changed 20 > 16
            h.Input.Step();
            var enter = h.Log.Events[1];
            Assert.AreEqual(GestureEventType.Enter, enter.Type);
            Assert.AreEqual(1.05f, enter.Scale, 1e-4f);

            h.Input.Move(2, new Vector2(500, 400)); // span 500
            h.Input.Step();
            var stay = h.Log.Events[2];
            Assert.AreEqual(1.25f, stay.Scale, 1e-4f);
            Assert.AreEqual(1.25f / 1.05f, stay.ScaleDelta, 1e-4f);

            h.Input.Up(2);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
            AssertClean(h.Log);
        }

        [Test]
        public void Pinch_BelowMinimumSpan_NeverEnters()
        {
            using var h = new GestureHarness<PinchGesture>();
            Two(h.Input, new Vector2(150, 400), new Vector2(250, 400)); // span 100
            h.Input.Move(2, new Vector2(300, 400));                       // span 150: changed 50, but below 170
            h.Input.Step();
            h.Input.Up(2);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(FingerCount)" }, Kinds(h.Log));
        }

        [Test]
        public void Pinch_OneFinger_Nothing()
        {
            using var h = new GestureHarness<PinchGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            Assert.IsEmpty(h.Log.Entries);
        }

        [Test]
        public void Pinch_SpanDropsBelowMinimum_Exits()
        {
            using var h = new GestureHarness<PinchGesture>();
            Two(h.Input, new Vector2(0, 400), new Vector2(400, 400));
            h.Input.Move(2, new Vector2(450, 400));
            h.Input.Step();
            h.Input.Move(2, new Vector2(150, 400)); // span 150 < 170 (as Android: the scale ends below the minimum span)
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
        }

        [Test]
        public void Pinch_StartsAtAndroidsMinimumDistance()
        {
            // Device report: a pinch needed the fingers ~340 dp apart, because the span was half their distance.
            using var h = new GestureHarness<PinchGesture>();
            Two(h.Input, new Vector2(100, 400), new Vector2(280, 400)); // 180 dp apart: above the 170 dp minimum
            h.Input.Move(2, new Vector2(300, 400));                     // 200 apart: changed 20 > 16
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter" }, Kinds(h.Log));
        }

        [Test]
        public void Pinch_ZoomingOut_LastsUntilTheFingersAreClose()
        {
            // Device report: pinching in (zoom out) ended as soon as the fingers were closer than ~340 dp.
            using var h = new GestureHarness<PinchGesture>();
            Two(h.Input, new Vector2(0, 400), new Vector2(400, 400));
            h.Input.Move(2, new Vector2(420, 400));
            h.Input.Step();
            for (var x = 400; x >= 180; x -= 20) // pinch in to 180 dp apart
            {
                h.Input.Move(2, new Vector2(x, 400));
                h.Input.Step();
            }
            Assert.AreEqual(GestureEventType.Stay, h.Log.Events.Last().Type, "Still zooming out");
            Assert.AreEqual(180f / 400f, h.Log.Events.Last().Scale, 1e-4f);

            h.Input.Move(2, new Vector2(160, 400)); // 160 < 170: ends, as on Android
            h.Input.Step();
            Assert.AreEqual("Exit(Completed)", Kinds(h.Log).Last());
        }

        [Test]
        public void Pinch_FingerJoins_NoScaleJump()
        {
            using var h = new GestureHarness<PinchGesture>();
            Two(h.Input, new Vector2(0, 400), new Vector2(400, 400));
            h.Input.Move(2, new Vector2(450, 400));
            h.Input.Step();
            h.Input.Down(3, new Vector2(225, 0)); // changes the span a lot
            h.Input.Step();

            var stay = h.Log.Events.Last();
            Assert.AreEqual(GestureEventType.Stay, stay.Type);
            Assert.AreEqual(1f, stay.ScaleDelta, 1e-4f);
            Assert.AreEqual(3, stay.FingerCount);
        }

        // ---- Rotate --------------------------------------------------------------------------

        private static Vector2 Around(Vector2 centre, float radius, float degrees) =>
            centre + radius * new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        [Test]
        public void Rotate_SmallTurn_StaysPossible_ThenEntersPastMinimum()
        {
            using var h = new GestureHarness<RotateGesture>();
            var centre = new Vector2(100, 400);
            Two(h.Input, centre, Around(centre, 200, 0));
            h.Input.Move(2, Around(centre, 200, 3)); // 3° < 5°
            h.Input.Step();
            Assert.IsTrue(h.Gesture.IsPossible);

            h.Input.Move(2, Around(centre, 200, 8));
            h.Input.Step();
            var enter = h.Log.Events.Last();
            Assert.AreEqual(GestureEventType.Enter, enter.Type);
            Assert.AreEqual(8f, enter.AngleDeg, 1e-3f);
        }

        [Test]
        public void Rotate_Clockwise_IsNegative()
        {
            using var h = new GestureHarness<RotateGesture>();
            var centre = new Vector2(100, 400);
            Two(h.Input, centre, Around(centre, 200, 0));
            h.Input.Move(2, Around(centre, 200, -10));
            h.Input.Step();
            Assert.AreEqual(-10f, h.Log.Events.Last().AngleDeg, 1e-3f);
        }

        [Test]
        public void Rotate_AccumulatesBeyond180()
        {
            using var h = new GestureHarness<RotateGesture>();
            var centre = new Vector2(200, 400);
            Two(h.Input, centre, Around(centre, 150, 0));
            for (var degrees = 30; degrees <= 270; degrees += 30)
            {
                h.Input.Move(2, Around(centre, 150, degrees));
                h.Input.Step();
            }
            var last = h.Log.Events.Last();
            Assert.AreEqual(270f, last.AngleDeg, 1e-2f);
            Assert.AreEqual(30f, last.AngleDeltaDeg, 1e-2f);

            h.Input.Up(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
            AssertClean(h.Log);
        }

        private static Vector2 Rotated(Vector2 v, float degrees)
        {
            var r = degrees * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
        }

        [Test]
        public void Rotate_MeasuredPairChanges_AngleContinuesWithoutJump()
        {
            using var h = new GestureHarness<RotateGesture>();
            var f1 = new Vector2(100, 400);
            var f2 = Around(f1, 200, 0);
            Two(h.Input, f1, f2);
            f2 = Around(f1, 200, 10);
            h.Input.Move(2, f2);
            h.Input.Step();                                  // Enter at 10°
            Assert.AreEqual(10f, h.Log.Events.Last().AngleDeg, 1e-3f);

            var f3 = new Vector2(250, 150);
            h.Input.Down(3, f3);                             // joins; measured pair is still (1, 2)
            h.Input.Step();
            Assert.AreEqual(0f, h.Log.Events.Last().AngleDeltaDeg, 1e-3f);

            h.Input.Up(1);                                   // pair becomes (2, 3): a different line
            h.Input.Step();
            var afterSwap = h.Log.Events.Last();
            Assert.AreEqual(GestureEventType.Stay, afterSwap.Type, "Two fingers remain: still rotating.");
            Assert.AreEqual(10f, afterSwap.AngleDeg, 1e-3f, "No jump when the measured pair changes.");
            Assert.AreEqual(0f, afterSwap.AngleDeltaDeg, 1e-3f);

            h.Input.Step();                                  // finger 1 now gone from the frame
            f3 = f2 + Rotated(f3 - f2, 15f);                 // turn the new pair by +15°
            h.Input.Move(3, f3);
            h.Input.Step();
            var turned = h.Log.Events.Last();
            Assert.AreEqual(25f, turned.AngleDeg, 1e-2f);
            Assert.AreEqual(15f, turned.AngleDeltaDeg, 1e-2f);
            AssertClean(h.Log);
        }

        // ---- TwoFingerPan --------------------------------------------------------------------

        [Test]
        public void TwoFingerPan_BothMove_EntersAndReportsDelta()
        {
            using var h = new GestureHarness<TwoFingerPanGesture>();
            Two(h.Input, new Vector2(150, 400), new Vector2(250, 400));
            for (var i = 1; i <= 4; i++)
            {
                h.Input.Move(1, new Vector2(150, 400 + 10 * i));
                h.Input.Move(2, new Vector2(250, 400 + 10 * i));
                h.Input.Step();
            }
            h.Input.Up(1);
            h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
            var total = h.Log.Events.Where(e => e.Type != GestureEventType.Exit).Aggregate(Vector2.zero, (s, e) => s + e.DeltaDp);
            Assert.AreEqual(new Vector2(0, 40), total);
            AssertClean(h.Log);
        }

        [Test]
        public void TwoFingerPan_SymmetricSpread_DoesNotEnter()
        {
            using var h = new GestureHarness<TwoFingerPanGesture>();
            Two(h.Input, new Vector2(150, 400), new Vector2(250, 400));
            h.Input.Move(1, new Vector2(100, 400));
            h.Input.Move(2, new Vector2(300, 400));
            h.Input.Step();
            Assert.IsTrue(h.Gesture.IsPossible, "A pinch moves no centroid: not a pan.");
        }

        [Test]
        public void TwoFingerPan_FingerJoins_NoPositionJump()
        {
            using var h = new GestureHarness<TwoFingerPanGesture>();
            Two(h.Input, new Vector2(150, 400), new Vector2(250, 400));
            h.Input.Move(1, new Vector2(150, 420));
            h.Input.Move(2, new Vector2(250, 420));
            h.Input.Step();
            h.Input.Down(3, new Vector2(350, 100));
            h.Input.Step();

            var stay = h.Log.Events.Last();
            Assert.AreEqual(GestureEventType.Stay, stay.Type);
            Assert.AreEqual(Vector2.zero, stay.DeltaDp);
        }

        // ---- TwoFingerTap --------------------------------------------------------------------

        [Test]
        public void TwoFingerTap_TogetherAndUp_Recognised()
        {
            using var h = new GestureHarness<TwoFingerTapGesture>();
            Two(h.Input, new Vector2(150, 400), new Vector2(250, 400));
            h.Input.Up(1);
            h.Input.Up(2);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, h.Log.Entries);
            AssertClean(h.Log);
        }

        [Test]
        public void TwoFingerTap_SecondWithinWindow_LiftedSeparately_Recognised()
        {
            using var h = new GestureHarness<TwoFingerTapGesture>();
            h.Input.Down(1, new Vector2(150, 400));
            h.Input.Step();
            h.Input.Down(2, new Vector2(250, 400));
            h.Input.Step(100);
            h.Input.Up(1);
            h.Input.Step();
            h.Input.Up(2);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
        }

        [Test]
        public void TwoFingerTap_SecondTooLate_FailsTimeout()
        {
            using var h = new GestureHarness<TwoFingerTapGesture>();
            h.Input.Down(1, new Vector2(150, 400));
            h.Input.Step();
            h.Input.Hold(200);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Timeout)" }, Kinds(h.Log));
        }

        [Test]
        public void TwoFingerTap_FirstLiftsBeforeSecond_FailsReleased()
        {
            using var h = new GestureHarness<TwoFingerTapGesture>();
            h.Input.Down(1, new Vector2(150, 400));
            h.Input.Step();
            h.Input.Up(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Released)" }, Kinds(h.Log));
        }

        [Test]
        public void TwoFingerTap_Moved_FailsMoved()
        {
            using var h = new GestureHarness<TwoFingerTapGesture>();
            Two(h.Input, new Vector2(150, 400), new Vector2(250, 400));
            h.Input.Drag(2, new Vector2(280, 400), durationMs: 50);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Moved)" }, Kinds(h.Log));
        }

        [Test]
        public void TwoFingerTap_ThirdFinger_FailsFingerCount()
        {
            using var h = new GestureHarness<TwoFingerTapGesture>();
            Two(h.Input, new Vector2(150, 400), new Vector2(250, 400));
            h.Input.Down(3, new Vector2(200, 500));
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(FingerCount)" }, Kinds(h.Log));
        }

        [Test]
        public void TwoFingerTap_Held_FailsHeld()
        {
            using var h = new GestureHarness<TwoFingerTapGesture>();
            Two(h.Input, new Vector2(150, 400), new Vector2(250, 400));
            h.Input.Hold(450);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Held)" }, Kinds(h.Log));
        }
    }
}
