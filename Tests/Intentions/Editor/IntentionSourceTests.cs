using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using Threads.TouchController.Intentions.Testing;
using UnityEngine;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>Manual sources (IntentionSource) and the same-intention rule.</summary>
    public class IntentionSourceTests
    {
        private IntentionSystemHarness _h;

        [SetUp]
        public void SetUp() => _h = new IntentionSystemHarness();

        [TearDown]
        public void TearDown() => _h?.Dispose();

        private static List<string> Names(IEnumerable<string> entries) => entries.Select(e => Regex.Replace(e, "#-?\\d+$", "")).ToList();

        private Intention Enable(Intention intention) => _h.Enable(intention);

        [Test]
        public void BeginMoveEnd_DrivesAnIntentionLikeAFinger()
        {
            var mouse = _h.Intentions.CreateSource("Mouse");
            var drag = Enable(new Intention("Drag", _h.Intentions).Bind<DragGesture>().Bind(mouse));
            var log = _h.Log(drag);

            mouse.Begin(new Vector2(100, 100));
            _h.Input.Step();
            Assert.IsTrue(drag.IsActive, "A source press starts at once (like Press)");
            mouse.MoveBy(new Vector2(30, 0));
            _h.Input.Step();
            mouse.MoveTo(new Vector2(150, 120));
            mouse.End();
            _h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(log.WithoutPerformed()));
            var possible = log.Events[0];
            Assert.AreEqual(IntentionSourceKind.Manual, possible.Source);
            Assert.IsNull(possible.Gesture);
            Assert.AreEqual(mouse.PointerId, possible.FingerId);
            Assert.Less(mouse.PointerId, 0, "Never a touch's ID");
            Assert.Less(possible.Instance.GestureInstanceId, 0, "The source's own instance IDs never clash with gesture instances");
            Assert.AreEqual(new Vector2(100, 100), possible.StartPositionDp);
            var moved = log.Events.Aggregate(Vector2.zero, (sum, e) => sum + e.DeltaDp);
            Assert.AreEqual(new Vector2(50, 20), moved);
            Assert.AreEqual(new Vector2(150, 120), log.Events.Last().PositionDp);
            Assert.IsFalse(mouse.IsDown);
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Tap_StartedPerformedEnded_InOneUpdate()
        {
            var keyboard = _h.Intentions.CreateSource("Enter key", IntentionSourceKind.Manual);
            var press = Enable(new Intention("Press", _h.Intentions).Bind(keyboard));
            var log = _h.Log(press);

            keyboard.Tap(new Vector2(50, 50));
            _h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Performed", "Ended(Completed)" }, Names(log.Entries));
            Assert.AreEqual(1, log.Events[2].TapCount);
        }

        [Test]
        public void Area_IsCheckedWhereTheSourceBegins()
        {
            var mouse = _h.Intentions.CreateSource("Mouse");
            var drag = Enable(new Intention("Drag", _h.Intentions).Bind(mouse).InArea(ScreenRectArea.Dp(new Rect(0, 0, 200, 200))));
            var log = _h.Log(drag);

            mouse.Begin(new Vector2(300, 300));
            mouse.End();
            _h.Input.Step();
            Assert.IsEmpty(log.Entries);

            mouse.Begin(new Vector2(100, 100));
            _h.Input.Step();
            Assert.IsTrue(drag.IsActive);
        }

        [Test]
        public void CancelAndBeginWhileDown()
        {
            var mouse = _h.Intentions.CreateSource("Mouse");
            var drag = Enable(new Intention("Drag", _h.Intentions).Bind(mouse));
            var log = _h.Log(drag);

            mouse.Begin(new Vector2(100, 100));
            _h.Input.Step();
            mouse.Begin(new Vector2(200, 200)); // the previous press is cancelled
            _h.Input.Step();
            mouse.Cancel();
            _h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Cancelled)", "Possible", "Started", "Ended(Cancelled)" },
                Names(log.WithoutPerformed()));
            mouse.End(); // not down: ignored
            mouse.MoveBy(Vector2.one);
            _h.Input.Step();
            Assert.AreEqual(6, log.WithoutPerformed().Count);
        }

        [Test]
        public void ScaleAndRotate()
        {
            var wheel = _h.Intentions.CreateSource("Wheel");
            var zoom = Enable(new Intention("Zoom", _h.Intentions).Bind(wheel));
            var log = _h.Log(zoom);

            wheel.Begin(new Vector2(200, 400));
            wheel.ScaleBy(1.5f);
            wheel.RotateBy(10f);
            _h.Input.Step();
            wheel.ScaleBy(2f);
            _h.Input.Step();

            var performed = log.Events.Where(e => e.Type == IntentionEventType.Performed).ToList();
            Assert.AreEqual(1.5f, performed[0].ScaleDelta, 1e-5f, "Movement before the start is reported by the first Performed");
            Assert.AreEqual(10f, performed[0].AngleDeltaDeg, 1e-5f);
            Assert.AreEqual(2f, performed[1].ScaleDelta, 1e-5f);
            Assert.AreEqual(3f, performed[1].Scale, 1e-5f);
            Assert.AreEqual(10f, performed[1].AngleDeg, 1e-5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => wheel.ScaleBy(0f));
        }

        [Test]
        public void SourcesTakePartInPriority()
        {
            var mouse = _h.Intentions.CreateSource("Mouse");
            var low = Enable(new Intention("Low", _h.Intentions).Bind(mouse));
            var high = Enable(new Intention("High", _h.Intentions).Bind(mouse).WithPriority(5).Exclusive());
            var lowLog = _h.Log(low);

            mouse.Begin(new Vector2(100, 100));
            _h.Input.Step();

            Assert.IsTrue(high.IsActive);
            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(LostToPriority)" }, Names(lowLog.Entries));
        }

        [Test]
        public void TouchAndSourceAreDifferentFingers()
        {
            var mouse = _h.Intentions.CreateSource("Mouse");
            var drag = Enable(new Intention("Drag", _h.Intentions).Bind<DragGesture>().Bind(mouse).Exclusive());

            mouse.Begin(new Vector2(300, 300));
            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(200, 100), 100);

            Assert.AreEqual(2, drag.Instances.Count(i => i.IsActive));
        }

        [Test]
        public void Misuse()
        {
            Assert.Throws<ArgumentException>(() => _h.Intentions.CreateSource("No", IntentionSourceKind.Gesture));
            Assert.Throws<ArgumentNullException>(() => new Intention().Bind((IntentionSource)null));

            using var other = new IntentionSystemHarness();
            var foreign = other.Intentions.CreateSource("Elsewhere");
            var intention = new Intention("Mixed", _h.Intentions).Bind(foreign);
            Assert.Throws<InvalidOperationException>(() => intention.Enabled = true);
            Assert.IsFalse(intention.Enabled);
        }

        [Test]
        public void IgnoredWhileTheSystemIsStopped()
        {
            var mouse = _h.Intentions.CreateSource("Mouse");
            var drag = Enable(new Intention("Drag", _h.Intentions).Bind(mouse));
            var log = _h.Log(drag);
            _h.Intentions.Shutdown();

            mouse.Begin(new Vector2(100, 100));
            Assert.IsFalse(mouse.IsDown);
            _h.Intentions.Startup();
            _h.Input.Step();
            Assert.IsEmpty(log.Entries);

            mouse.Tap(new Vector2(100, 100));
            _h.Input.Step();
            Assert.AreEqual(1, log.Count(IntentionEventType.Performed), "Works after restarting");
        }

        // ---- Same intention never competes with itself ---------------------------------------------------

        [Test]
        public void OneIntentionBoundToPanPinchAndRotate_RunsThemTogether_AndPreemptsADrag()
        {
            var drag = Enable(new Intention("Card drag", _h.Intentions).Bind<DragGesture>());
            var manipulate = Enable(new Intention("Card manipulate", _h.Intentions)
                .Bind<TwoFingerPanGesture>().Bind<PinchGesture>().Bind<RotateGesture>().WithPriority(20).Exclusive());
            var dragLog = _h.Log(drag);
            var log = _h.Log(manipulate);

            var centre = new Vector2(200, 400);
            Vector2 At(float degrees, float radius, Vector2 offset) =>
                centre + offset + radius * new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
            _h.Input.Down(1, At(0, 200, Vector2.zero));
            _h.Input.Step();
            _h.Input.Drag(1, At(0, 200, new Vector2(0, 20)), 100);
            _h.Input.Down(2, At(180, 200, new Vector2(0, 20)));
            _h.Input.Step();
            for (var k = 1; k <= 15; k++) // move, spread and turn at once
            {
                var offset = new Vector2(0, 20 + 3 * k);
                _h.Input.Move(1, At(2 * k, 200 + 5 * k, offset));
                _h.Input.Move(2, At(180 + 2 * k, 200 + 5 * k, offset));
                _h.Input.Step();
            }

            var started = log.Events.Where(e => e.Type == IntentionEventType.Started).Select(e => e.Gesture).ToList();
            CollectionAssert.AreEquivalent(new IGesture[]
            {
                _h.Touch.System.Get<TwoFingerPanGesture>(), _h.Touch.System.Get<PinchGesture>(), _h.Touch.System.Get<RotateGesture>(),
            }, started, "All three run together");
            Assert.AreEqual(3, manipulate.Instances.Count(i => i.IsActive));
            CollectionAssert.Contains(Names(dragLog.Entries), "Ended(Preempted)");
            Assert.IsEmpty(_h.Violations());
        }
    }
}
