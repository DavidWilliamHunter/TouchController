using System.Linq;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using Threads.TouchController.Intentions.Testing;
using UnityEngine;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>
    /// What intention events carry: every value of <see cref="IntentionEventData"/> must reflect the gesture
    /// that drove it. (The lifecycle tests only check which events happen.)
    /// </summary>
    public class IntentionEventDataTests
    {
        private const float PixelsPerDp = 2f;
        private IntentionSystemHarness _h;

        [SetUp]
        public void SetUp() => _h = new IntentionSystemHarness(pixelsPerDp: PixelsPerDp);

        [TearDown]
        public void TearDown() => _h?.Dispose();

        private (Intention intention, IntentionEventLog log) Enable<T>(string name) where T : class, IGesture
        {
            var intention = _h.Enable(new Intention(name, _h.Intentions).Bind<T>());
            return (intention, _h.Log(intention));
        }

        private static IntentionEventData First(IntentionEventLog log, IntentionEventType type) => log.Events.First(e => e.Type == type);
        private static IntentionEventData Last(IntentionEventLog log, IntentionEventType type) => log.Events.Last(e => e.Type == type);

        [Test]
        public void Drag_PositionsTimesFingersAndSource()
        {
            var (drag, log) = Enable<DragGesture>("Drag");
            var down = new Vector2(100, 100);
            _h.Input.Down(1, down);
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(200, 150), 200);
            _h.Input.Up(1);
            _h.Input.Step();

            var possible = First(log, IntentionEventType.Possible);
            Assert.AreSame(drag, possible.Intention);
            Assert.AreSame(drag, possible.Instance.Intention);
            Assert.AreSame(_h.Touch.System.Get<DragGesture>(), possible.Gesture);
            Assert.AreEqual(IntentionSourceKind.Gesture, possible.Source);
            Assert.AreEqual(1, possible.FingerId);
            CollectionAssert.AreEqual(new[] { 1 }, possible.FingerIds);
            Assert.AreEqual(1, possible.FingerCount);
            Assert.AreEqual(down, possible.PositionDp);
            Assert.AreEqual(down, possible.StartPositionDp);
            Assert.AreEqual(down * PixelsPerDp, possible.PositionPx);
            Assert.AreEqual(Vector2.zero, possible.DeltaDp);
            Assert.AreEqual(possible.Time, possible.StartTime);
            Assert.AreEqual(1f, possible.Scale);
            Assert.AreEqual(1f, possible.ScaleDelta);
            Assert.AreEqual(0f, possible.AngleDeltaDeg);

            var started = First(log, IntentionEventType.Started);
            Assert.AreEqual(Vector2.zero, started.DeltaDp, "Started carries no movement");
            Assert.Greater(started.Time, started.StartTime);
            Assert.AreEqual(possible.StartTime, started.StartTime);

            var moving = log.Events.Where(e => e.Type == IntentionEventType.Performed).ElementAt(3);
            Assert.AreEqual(moving.DeltaDp * PixelsPerDp, moving.DeltaPx);
            Assert.AreEqual(moving.PositionDp * PixelsPerDp, moving.PositionPx);
            Assert.AreEqual(down, moving.StartPositionDp);
            Assert.Greater(moving.VelocityDps.x, 0f);
            Assert.Greater(moving.VelocityDps.y, 0f);

            var ended = Last(log, IntentionEventType.Ended);
            Assert.AreEqual(new Vector2(200, 150), ended.PositionDp);
            Assert.AreEqual(Vector2.zero, ended.DeltaDp);
            Assert.AreEqual(IntentionReason.Completed, ended.Reason);
            Assert.AreEqual($"Drag #{ended.InstanceId} Ended(Completed)", ended.ToString());
            Assert.AreEqual($"Drag #{possible.InstanceId} Possible", possible.ToString());
            Assert.AreEqual($"Drag #{ended.InstanceId} Finished", ended.Instance.ToString());
        }

        [Test]
        public void Drag_MovementInTheLastFrameIsReported()
        {
            var (_, log) = Enable<DragGesture>("Drag");
            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(150, 100), 100);
            _h.Input.Move(1, new Vector2(180, 100)); // moves and lifts in the same frame
            _h.Input.Up(1);
            _h.Input.Step();

            var moved = log.Events.Aggregate(Vector2.zero, (sum, e) => sum + e.DeltaDp);
            Assert.AreEqual(80f, moved.x, 0.01f, "The final 30 dp arrives in a Performed before Ended");
            Assert.AreEqual(IntentionEventType.Performed, log.Events[log.Events.Count - 2].Type);
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Pinch_ScaleDeltasMultiplyToTheScale()
        {
            var (_, log) = Enable<PinchGesture>("Zoom");
            _h.Input.Down(1, new Vector2(200, 150));
            _h.Input.Down(2, new Vector2(200, 650));
            _h.Input.Step();
            for (var k = 1; k <= 10; k++)
            {
                _h.Input.Move(1, new Vector2(200, 150 - 5 * k));
                _h.Input.Move(2, new Vector2(200, 650 + 5 * k));
                _h.Input.Step();
            }

            var performed = log.Events.Where(e => e.Type == IntentionEventType.Performed).ToList();
            Assert.IsNotEmpty(performed, "The pinch started");
            var product = performed.Aggregate(1f, (p, e) => p * e.ScaleDelta);
            Assert.Greater(performed.Last().Scale, 1.1f);
            Assert.AreEqual(performed.Last().Scale, product, 1e-4f);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, performed.Last().FingerIds);
            Assert.AreEqual(2, performed.Last().FingerCount);
            Assert.AreEqual(new Vector2(200, 400), performed.Last().PositionDp, "Centroid of the fingers");
        }

        [Test]
        public void Rotate_AngleDeltasAddUpToTheAngle()
        {
            var (_, log) = Enable<RotateGesture>("Turn");
            var centre = new Vector2(200, 400);
            Vector2 At(float degrees) => centre + 150f * new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
            _h.Input.Down(1, At(0));
            _h.Input.Down(2, At(180));
            _h.Input.Step();
            for (var k = 1; k <= 10; k++)
            {
                _h.Input.Move(1, At(3 * k));
                _h.Input.Move(2, At(180 + 3 * k));
                _h.Input.Step();
            }

            var performed = log.Events.Where(e => e.Type == IntentionEventType.Performed).ToList();
            Assert.IsNotEmpty(performed, "The rotation started");
            Assert.AreEqual(30f, performed.Last().AngleDeg, 0.1f, "Counter-clockwise is positive");
            Assert.AreEqual(performed.Last().AngleDeg, performed.Sum(e => e.AngleDeltaDeg), 1e-3f);
        }

        [Test]
        public void Tap_And_DoubleTap_TapCount()
        {
            var (_, taps) = Enable<TapGesture>("Tap");
            var (_, doubles) = Enable<DoubleTapGesture>("Double tap");
            for (var i = 1; i <= 2; i++)
            {
                _h.Input.Down(i, new Vector2(100, 100));
                _h.Input.Step();
                _h.Input.Up(i);
                _h.Input.Step();
                _h.Input.Hold(80);
            }

            Assert.AreEqual(1, First(taps, IntentionEventType.Performed).TapCount);
            Assert.AreEqual(2, First(doubles, IntentionEventType.Started).TapCount);
        }

        [Test]
        public void Swipe_DirectionAndVelocity()
        {
            var (_, log) = Enable<SwipeGesture>("Fling");
            _h.Input.Down(1, new Vector2(100, 400));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(300, 400), 60);
            _h.Input.Up(1);
            _h.Input.Step();

            var swipe = First(log, IntentionEventType.Performed);
            Assert.AreEqual(Vector2.right, swipe.Direction);
            Assert.Greater(swipe.VelocityDps.x, 1000f);
        }

        [Test]
        public void EdgeSwipe_Edge()
        {
            var (_, log) = Enable<EdgeSwipeGesture>("Back");
            _h.Input.Down(1, new Vector2(4, 400));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(120, 400), 150);

            var edge = First(log, IntentionEventType.Started);
            Assert.AreEqual(ScreenEdge.Left, edge.Edge);
            Assert.AreEqual(ScreenEdge.Left, First(log, IntentionEventType.Possible).Edge, "Known from touch-down");
        }

        [Test]
        public void FingerIds_IsLive_CopyItToKeepIt()
        {
            var (_, log) = Enable<TwoFingerPanGesture>("Pan");
            _h.Input.Down(1, new Vector2(100, 300));
            _h.Input.Down(2, new Vector2(300, 300));
            _h.Input.Step();
            for (var k = 1; k <= 5; k++)
            {
                _h.Input.Move(1, new Vector2(100, 300 + 5 * k));
                _h.Input.Move(2, new Vector2(300, 300 + 5 * k));
                _h.Input.Step();
            }

            var started = First(log, IntentionEventType.Started);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, started.FingerIds);
            Assert.AreSame(started.Instance.FingerIds, started.FingerIds, "The instance's own list (as GestureEventData)");
        }
    }
}
