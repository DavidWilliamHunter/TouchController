using System;
using System.Linq;
using NUnit.Framework;
using Threads.TouchController.Testing;
using UnityEngine;
using static Threads.TouchController.Tests.SingleFingerGestureTests;

namespace Threads.TouchController.Tests
{
    /// <summary>The shipped platform lineups, and plan.md's example traces across a whole TouchSystem.</summary>
    public class LineupAndTraceTests
    {
        private static readonly TouchPlatform[] AllPlatforms = { TouchPlatform.Android, TouchPlatform.iOS, TouchPlatform.Windows };

        private static Type DoubleTapFor(TouchPlatform platform) => platform switch
        {
            TouchPlatform.Android => typeof(AndroidDoubleTap),
            TouchPlatform.iOS => typeof(IosDoubleTap),
            _ => typeof(WindowsDoubleTap),
        };

        // ---- Shipped lineups -----------------------------------------------------------------

        [TestCaseSource(nameof(AllPlatforms))]
        public void DefaultLineup_HasEveryGesture_WithThePlatformsDoubleTap(TouchPlatform platform)
        {
            var types = DefaultTouchSettings.Load(platform).Lineup.Select(e => e.gesture.GetType()).ToArray();
            var expected = new[]
            {
                typeof(PressGesture), typeof(TapGesture), DoubleTapFor(platform), typeof(LongPressGesture),
                typeof(DragGesture), typeof(SwipeGesture), typeof(PinchGesture), typeof(RotateGesture),
                typeof(TwoFingerPanGesture), typeof(TwoFingerTapGesture), typeof(EdgeSwipeGesture),
            };
            CollectionAssert.AreEqual(expected, types);
        }

        [TestCaseSource(nameof(AllPlatforms))]
        public void DefaultLineup_AllEnabled_SharedAssets(TouchPlatform platform)
        {
            var lineup = DefaultTouchSettings.Load(platform).Lineup;
            Assert.IsTrue(lineup.All(e => e.enabled));
            var tap = lineup.First(e => e.gesture is TapGesture).gesture;
            var androidTap = DefaultTouchSettings.Load(TouchPlatform.Android).Lineup.First(e => e.gesture is TapGesture).gesture;
            Assert.AreSame(androidTap, tap, "Gestures that don't differ by platform are one shared asset.");
        }

        [Test]
        public void WindowsDoubleTapAsset_NotesTheQ11Guess()
        {
            var asset = DefaultTouchSettings.Load(TouchPlatform.Windows).Lineup.First(e => e.gesture is WindowsDoubleTap).gesture;
            StringAssert.Contains("Q11", asset.Notes);
            StringAssert.Contains("GUESS", asset.Notes);
        }

        [TestCaseSource(nameof(AllPlatforms))]
        public void System_GetDoubleTapGesture_ReturnsPlatformVariant(TouchPlatform platform)
        {
            using var h = new TouchSystemHarness(platform);
            Assert.IsInstanceOf(DoubleTapFor(platform), h.System.Get<DoubleTapGesture>());
            Assert.AreEqual(11, h.System.Gestures.Count);
        }

        // ---- plan.md trace A: one finger, quick drag, released fast ----------------------------

        [Test]
        public void TraceA_FlickOnAndroid_EveryGestureReportsItsView()
        {
            using var h = new TouchSystemHarness(TouchPlatform.Android);
            var press = h.Log<PressGesture>();
            var tap = h.Log<TapGesture>();
            var doubleTap = h.Log<DoubleTapGesture>();
            var longPress = h.Log<LongPressGesture>();
            var drag = h.Log<DragGesture>();
            var swipe = h.Log<SwipeGesture>();
            var pinch = h.Log<PinchGesture>();
            var twoFingerTap = h.Log<TwoFingerTapGesture>();
            var edge = h.Log<EdgeSwipeGesture>();

            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Drag(1, P + new Vector2(120, 0), durationMs: 80);
            h.Input.Up(1);
            h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(press));
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Moved)" }, Kinds(tap));
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Moved)" }, Kinds(doubleTap));
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Moved)" }, Kinds(longPress));
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(drag));
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(swipe));
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Moved)" }, Kinds(twoFingerTap));
            Assert.IsEmpty(pinch.Entries);
            Assert.IsEmpty(edge.Entries);

            // The drag ends and the swipe is recognised in the same update: the context code sees both.
            Assert.AreEqual(drag.Events.Last().Time, swipe.Events.Last().Time);
            Assert.IsEmpty(h.Violations());
        }

        // ---- plan.md trace B: tap, tap ---------------------------------------------------------

        [TestCase(TouchPlatform.Android, false)]
        [TestCase(TouchPlatform.iOS, true)]
        public void TraceB_DoubleTap_TapsAndDoubleTap(TouchPlatform platform, bool entersOnSecondUp)
        {
            using var h = new TouchSystemHarness(platform);
            var tap = h.Log<TapGesture>();
            var doubleTap = h.Log<DoubleTapGesture>();
            var drag = h.Log<DragGesture>();

            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Up(1);
            h.Input.Step();
            h.Input.Step(100);
            h.Input.Down(2, P + new Vector2(5, 5));
            h.Input.Step();
            var secondDownTime = h.Input.Time;
            h.Input.Up(2);
            h.Input.Step();

            Assert.AreEqual(2, tap.Count(GestureEventType.Exit), "Each tap is reported at once.");
            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(doubleTap));
            var enter = doubleTap.Events.First(e => e.Type == GestureEventType.Enter);
            Assert.AreEqual(entersOnSecondUp, enter.Time > secondDownTime);
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Released)", "Possible", "Fail(Released)" }, Kinds(drag));
            Assert.IsEmpty(h.Violations());
        }

        [Test]
        public void SingleTapConfirmed_IsDoubleTapTimeout()
        {
            using var h = new TouchSystemHarness(TouchPlatform.Android);
            var tap = h.Log<TapGesture>();
            var doubleTap = h.Log<DoubleTapGesture>();

            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Up(1);
            h.Input.Step();
            h.Input.Hold(350);

            CollectionAssert.AreEqual(new[] { "Possible", "Enter", "Exit(Completed)" }, Kinds(tap));
            CollectionAssert.AreEqual(new[] { "Possible", "Fail(Timeout)" }, Kinds(doubleTap));
        }

        // ---- Combinations from overlapping gestures (rule 5) --------------------------------

        [Test]
        public void LongPressThenDrag_LongPressActiveWhenDragEnters()
        {
            using var h = new TouchSystemHarness(TouchPlatform.Android);
            var longPress = h.System.Get<LongPressGesture>();
            var dragLog = h.Log<DragGesture>();
            var longPressActiveAtDragEnter = false;
            h.System.Get<DragGesture>().Enter += _ => longPressActiveAtDragEnter = longPress.IsActive;

            h.Input.Down(1, P);
            h.Input.Step();
            h.Input.Hold(450);
            h.Input.Drag(1, P + new Vector2(0, 100), durationMs: 200);

            Assert.AreEqual(1, dragLog.Count(GestureEventType.Enter));
            Assert.IsTrue(longPressActiveAtDragEnter);
        }

        [Test]
        public void PinchRotateAndPan_CanBeActiveTogether()
        {
            using var h = new TouchSystemHarness(TouchPlatform.Android);
            var centre = new Vector2(200, 400);
            h.Input.Down(1, centre + new Vector2(-150, 0));
            h.Input.Down(2, centre + new Vector2(150, 0));
            h.Input.Step();
            for (var i = 1; i <= 10; i++) // spread, turn and move together
            {
                var shift = new Vector2(0, 5 * i);
                var radius = 150 + 5 * i;
                var angle = 2f * i * Mathf.Deg2Rad;
                var offset = radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                h.Input.Move(1, centre + shift - offset);
                h.Input.Move(2, centre + shift + offset);
                h.Input.Step();
            }

            Assert.IsTrue(h.System.Get<PinchGesture>().IsActive);
            Assert.IsTrue(h.System.Get<RotateGesture>().IsActive);
            Assert.IsTrue(h.System.Get<TwoFingerPanGesture>().IsActive);
        }
    }
}
