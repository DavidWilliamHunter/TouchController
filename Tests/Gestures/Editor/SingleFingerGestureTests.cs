using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Threads.TouchController.Testing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Threads.TouchController.Tests
{
    /// <summary>
    /// Press, Tap, LongPress, Drag, Swipe and EdgeSwipe with Android thresholds:
    /// slop 8 dp, long press 400 ms, fling 50–8000 dp/s over 100 ms, swipe ±30°, edge strip 12 dp.
    /// Screen 400 × 800 dp.
    /// </summary>
    public class SingleFingerGestureTests
    {
        internal static readonly Vector2 P = new Vector2(200, 400); // away from every edge

        internal static string[] Kinds(GestureEventLog log) =>
            log.WithoutStays().Select(e => Regex.Replace(e, "#\\d+$", "")).ToArray();

        internal static void AssertClean(GestureEventLog log) =>
            Assert.IsEmpty(log.Violations, string.Join("\n", log.Violations));

        // ---- Press ---------------------------------------------------------------------------

        [Test]
        public void Press_EntersOnDown_ExitsOnUp()
        {
            using var h = new GestureHarness<PressGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Step();
            h.Input.Up(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Stay#1", "Exit(Completed)#1" }, h.Log.Entries);
            AssertClean(h.Log);
        }

        [Test]
        public void Press_OsCancel_ExitsCancelled()
        {
            using var h = new GestureHarness<PressGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Cancel(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Cancelled)" }, Kinds(h.Log));
        }

        // ---- Tap -----------------------------------------------------------------------------

        [Test]
        public void Tap_QuickTap_Recognised()
        {
            using var h = new GestureHarness<TapGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Move(1, P + new Vector2(5, 0)); // within slop
            h.Input.Step(100);
            h.Input.Up(1);
            h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, h.Log.Entries);
            Assert.AreEqual(1, h.Log.Events[2].TapCount);
            AssertClean(h.Log);
        }

        [Test]
        public void Tap_MovedBeyondSlop_Fails()
        {
            using var h = new GestureHarness<TapGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Drag(1, P + new Vector2(20, 0), durationMs: 50);
            h.Input.Up(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Moved)" }, Kinds(h.Log));
        }

        [Test]
        public void Tap_HeldPastLongPress_FailsWhileStillDown()
        {
            using var h = new GestureHarness<TapGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Hold(450);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Held)" }, Kinds(h.Log));
        }

        [Test]
        public void Tap_TwoFingersAtOnce_TwoTaps()
        {
            using var h = new GestureHarness<TapGesture>();
            h.Input.Down(1, P);
            h.Input.Down(2, P + new Vector2(100, 0));
            h.Input.Step();
            h.Input.Up(1);
            h.Input.Up(2);
            h.Input.Step();
            Assert.AreEqual(2, h.Log.Count(GestureEventType.Exit));
        }

        // ---- LongPress -----------------------------------------------------------------------

        [Test]
        public void LongPress_EntersAfterTimeout_ThenMayMove_ExitsOnUp()
        {
            using var h = new GestureHarness<LongPressGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Hold(420);
            Assert.IsTrue(h.Gesture.IsActive);

            h.Input.Drag(1, P + new Vector2(100, 0), durationMs: 100); // moving after Enter is allowed
            Assert.IsTrue(h.Gesture.IsActive);
            Assert.AreEqual(P + new Vector2(100, 0), h.Log.Events.Last().PositionDp);

            h.Input.Up(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
            AssertClean(h.Log);
        }

        [Test]
        public void LongPress_ReleasedEarly_Fails()
        {
            using var h = new GestureHarness<LongPressGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Hold(200);
            h.Input.Up(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Released)" }, Kinds(h.Log));
        }

        [Test]
        public void LongPress_MovedBeforeTimeout_Fails()
        {
            using var h = new GestureHarness<LongPressGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Drag(1, P + new Vector2(0, 20), durationMs: 50);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Moved)" }, Kinds(h.Log));
        }

        [Test]
        public void LongPress_FollowsAccessibilityTimeout()
        {
            using var h = new GestureHarness<LongPressGesture>();
            h.Settings.LongPress.longPressTimeoutMs = 1000f; // e.g. Android "Touch & hold delay: Medium"
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Hold(500);
            Assert.IsFalse(h.Gesture.IsActive, "Not yet: the user asked for a longer delay.");
            h.Input.Hold(520);
            Assert.IsTrue(h.Gesture.IsActive);
        }

        // ---- Drag ----------------------------------------------------------------------------

        [Test]
        public void Drag_EntersBeyondSlop_DeltasAddUpToMovement()
        {
            using var h = new GestureHarness<DragGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Move(1, P + new Vector2(5, 0));
            h.Input.Step();
            Assert.IsTrue(h.Gesture.IsPossible, "5 dp is within the slop.");

            h.Input.Drag(1, P + new Vector2(60, -30), durationMs: 100);
            h.Input.Up(1);
            h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
            var total = h.Log.Events.Aggregate(Vector2.zero, (sum, e) => sum + e.DeltaDp);
            Assert.AreEqual(60f, total.x, 1e-3f);
            Assert.AreEqual(-30f, total.y, 1e-3f);
            AssertClean(h.Log);
        }

        [Test]
        public void Drag_ReleasedWithoutMoving_Fails()
        {
            using var h = new GestureHarness<DragGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Up(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Released)" }, Kinds(h.Log));
        }

        [Test]
        public void Drag_OsCancel_ExitsCancelled()
        {
            using var h = new GestureHarness<DragGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Drag(1, P + new Vector2(50, 0), durationMs: 50);
            h.Input.Cancel(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Cancelled)" }, Kinds(h.Log));
        }

        [Test]
        public void Drag_TwoThumbs_Independent()
        {
            using var h = new GestureHarness<DragGesture>();
            h.Input.Down(1, new Vector2(100, 200));
            h.Input.Down(2, new Vector2(300, 200));
            h.Input.Step();
            for (var i = 1; i <= 5; i++)
            {
                h.Input.Move(1, new Vector2(100, 200 + 10 * i));
                h.Input.Move(2, new Vector2(300 - 10 * i, 200));
                h.Input.Step();
            }
            Assert.AreEqual(2, h.Log.Count(GestureEventType.Enter));
            Assert.AreEqual(2, h.Gesture.Instances.Count(i => i.IsActive));
        }

        // ---- Swipe ---------------------------------------------------------------------------

        private static void Flick(ScriptedTouchInput input, Vector2 from, Vector2 to, double durationMs)
        {
            input.Down(1, from);
            input.Step();
            input.Drag(1, to, durationMs);
            input.Up(1);
            input.Step();
        }

        [TestCase(150f, 0f, 1f, 0f)]
        [TestCase(-150f, 0f, -1f, 0f)]
        [TestCase(0f, 150f, 0f, 1f)]
        [TestCase(0f, -150f, 0f, -1f)]
        public void Swipe_FastAlongAxis_RecognisedWithDirection(float dx, float dy, float ex, float ey)
        {
            using var h = new GestureHarness<SwipeGesture>();
            Flick(h.Input, P, P + new Vector2(dx, dy), durationMs: 100); // 1500 dp/s

            CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, h.Log.Entries);
            var exit = h.Log.Events.Last();
            Assert.AreEqual(new Vector2(ex, ey), exit.Direction);
            Assert.Greater(exit.VelocityDps.magnitude, 500f);
            AssertClean(h.Log);
        }

        [Test]
        public void Swipe_Slow_FailsTooSlow()
        {
            using var h = new GestureHarness<SwipeGesture>();
            Flick(h.Input, P, P + new Vector2(30, 0), durationMs: 1000); // 30 dp/s < 50
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(TooSlow)" }, Kinds(h.Log));
        }

        [Test]
        public void Swipe_PausedBeforeLift_FailsTooSlow()
        {
            using var h = new GestureHarness<SwipeGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Drag(1, P + new Vector2(150, 0), durationMs: 100);
            h.Input.Hold(200); // finger stops: no fling, as on Android
            h.Input.Up(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(TooSlow)" }, Kinds(h.Log));
        }

        [Test]
        public void Swipe_Diagonal_FailsDirection()
        {
            using var h = new GestureHarness<SwipeGesture>();
            Flick(h.Input, P, P + new Vector2(120, 120), durationMs: 100); // 45° > 30° tolerance
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Direction)" }, Kinds(h.Log));
        }

        [Test]
        public void Swipe_WithinTolerance_Recognised()
        {
            using var h = new GestureHarness<SwipeGesture>();
            Flick(h.Input, P, P + new Vector2(150, 60), durationMs: 100); // ~21.8°
            Assert.AreEqual(new Vector2(1, 0), h.Log.Events.Last().Direction);
        }

        [Test]
        public void Swipe_Tap_FailsReleased()
        {
            using var h = new GestureHarness<SwipeGesture>();
            Flick(h.Input, P, P, durationMs: 50);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Released)" }, Kinds(h.Log));
        }

        [Test]
        public void Swipe_VelocityClampedToMaximum()
        {
            using var h = new GestureHarness<SwipeGesture>();
            h.Settings.Fling.maxFlingVelocityDps = 500f;
            Flick(h.Input, P, P + new Vector2(150, 0), durationMs: 100);
            Assert.AreEqual(500f, h.Log.Events.Last().VelocityDps.magnitude, 0.01f);
        }

        // ---- EdgeSwipe -----------------------------------------------------------------------

        [TestCase(5f, 400f, 60f, 400f, ScreenEdge.Left, 1f, 0f)]
        [TestCase(395f, 400f, 340f, 400f, ScreenEdge.Right, -1f, 0f)]
        [TestCase(200f, 5f, 200f, 60f, ScreenEdge.Bottom, 0f, 1f)]
        [TestCase(200f, 795f, 200f, 740f, ScreenEdge.Top, 0f, -1f)]
        public void EdgeSwipe_FromEachEdge_Inwards(float x0, float y0, float x1, float y1, ScreenEdge edge, float dx, float dy)
        {
            using var h = new GestureHarness<EdgeSwipeGesture>();
            h.Input.Down(1, new Vector2(x0, y0));
            h.Input.Step();
            h.Input.Drag(1, new Vector2(x1, y1), durationMs: 100);
            h.Input.Up(1);
            h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(h.Log));
            var possible = h.Log.Events[0];
            Assert.AreEqual(edge, possible.Edge, "The edge is known from Possible onwards.");
            Assert.AreEqual(new Vector2(dx, dy), possible.Direction);
            AssertClean(h.Log);
        }

        [Test]
        public void EdgeSwipe_AwayFromEdges_NeverPossible()
        {
            using var h = new GestureHarness<EdgeSwipeGesture>();
            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Drag(1, P + new Vector2(100, 0), durationMs: 100);
            Assert.IsEmpty(h.Log.Entries);
        }

        [Test]
        public void EdgeSwipe_AlongTheEdge_FailsDirection()
        {
            using var h = new GestureHarness<EdgeSwipeGesture>();
            h.Input.Down(1, new Vector2(5, 400));
            h.Input.Step();
            h.Input.Drag(1, new Vector2(8, 460), durationMs: 100);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Direction)" }, Kinds(h.Log));
        }

        [Test]
        public void EdgeSwipe_ReleasedWithoutMoving_Fails()
        {
            using var h = new GestureHarness<EdgeSwipeGesture>();
            h.Input.Down(1, new Vector2(5, 400));
            h.Input.Step();
            h.Input.Up(1);
            h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Released)" }, Kinds(h.Log));
        }

        [Test]
        public void EdgeSwipe_DisabledEdge_Ignored()
        {
            var asset = ScriptableObject.CreateInstance<EdgeSwipeGesture>();
            asset.left = false;
            try
            {
                using var h = new GestureHarness<EdgeSwipeGesture>(asset);
                h.Input.Down(1, new Vector2(5, 400));
                h.Input.Step();
                Assert.IsEmpty(h.Log.Entries);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
