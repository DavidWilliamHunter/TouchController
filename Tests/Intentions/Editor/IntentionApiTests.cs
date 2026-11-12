using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using Threads.TouchController.Intentions.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>The Intention and IntentionSystem API: properties, misuse, finger rules and edge cases.</summary>
    public class IntentionApiTests
    {
        private IntentionSystemHarness _h;

        [SetUp]
        public void SetUp() => _h = new IntentionSystemHarness();

        [TearDown]
        public void TearDown() => _h?.Dispose();

        private static List<string> Names(IEnumerable<string> entries) => entries.Select(e => Regex.Replace(e, "#\\d+$", "")).ToList();

        private void DragFinger(int id, Vector2 from, Vector2 to, double durationMs = 200)
        {
            _h.Input.Down(id, from);
            _h.Input.Step();
            _h.Input.Drag(id, to, durationMs);
            _h.Input.Up(id);
            _h.Input.Step();
        }

        // ---- Intention: construction, properties, misuse -------------------------------------------------

        [Test]
        public void Defaults()
        {
            var intention = new Intention();
            Assert.AreEqual("Intention", intention.Name);
            Assert.AreEqual("Intention", intention.ToString());
            Assert.IsNull(intention.System);
            Assert.AreEqual(0, intention.Priority);
            Assert.AreEqual(ClaimMode.Shared, intention.Claim);
            Assert.AreEqual(AreaCheck.AtStart, intention.AreaCheck);
            Assert.AreEqual(FingerRule.Any, intention.FingerRule);
            Assert.IsNull(intention.Area);
            Assert.IsFalse(intention.Enabled);
            Assert.IsFalse(intention.IsActive);
            Assert.IsFalse(intention.IsPossible);
            Assert.IsEmpty(intention.Instances);

            intention.Name = "Renamed";
            Assert.AreEqual("Renamed", intention.Name);
        }

        [Test]
        public void Enabling_WithoutASystemOrBindings_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new Intention("No system").Bind<DragGesture>().Enabled = true);
            Assert.Throws<InvalidOperationException>(() => new Intention("No bindings", _h.Intentions).Enabled = true);
        }

        [Test]
        public void Attach_GivesTheSystem_OnlyWhileDisabled()
        {
            var drag = new Intention("Drag").Bind<DragGesture>().Attach(_h.Intentions);
            Assert.AreSame(_h.Intentions, drag.System);
            drag.Enabled = true;
            Assert.Throws<InvalidOperationException>(() => drag.Attach(_h.Intentions));

            var log = _h.Log(drag);
            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));
            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(log.WithoutPerformed()));
        }

        [Test]
        public void Properties_WorkLikeTheFluentCalls()
        {
            var low = _h.Enable(new Intention("Low", _h.Intentions).Bind<DragGesture>());
            var high = new Intention("High", _h.Intentions).Bind<DragGesture>();
            high.Priority = 10;
            high.Claim = ClaimMode.Exclusive;
            high.Area = ScreenRectArea.Dp(new Rect(0, 0, 200, 200));
            high.AreaCheck = AreaCheck.Continuous;
            high.FingerRule = FingerRule.Primary;
            high.Enabled = true;
            var lowLog = _h.Log(low);
            var highLog = _h.Log(high);

            DragFinger(1, new Vector2(100, 100), new Vector2(350, 100));

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(LeftArea)" }, Names(highLog.WithoutPerformed()));
            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(LostToPriority)" }, Names(lowLog.Entries));
        }

        [Test]
        public void Shared_UndoesExclusive()
        {
            var a = _h.Enable(new Intention("A", _h.Intentions).Bind<DragGesture>());
            var b = _h.Enable(new Intention("B", _h.Intentions).Bind<DragGesture>().WithPriority(10).Exclusive().Shared());
            var aLog = _h.Log(a);

            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));

            Assert.AreEqual(ClaimMode.Shared, b.Claim);
            Assert.AreEqual(1, aLog.Count(IntentionEventType.Started), "B no longer takes the finger");
        }

        [Test]
        public void IsPossible_AndIsActive_FollowTheInstances()
        {
            var drag = _h.Enable(new Intention("Drag", _h.Intentions).Bind<DragGesture>());
            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();
            Assert.IsTrue(drag.IsPossible);
            Assert.IsFalse(drag.IsActive);

            _h.Input.Drag(1, new Vector2(150, 100), 100);
            Assert.IsFalse(drag.IsPossible);
            Assert.IsTrue(drag.IsActive);

            _h.Input.Up(1);
            _h.Input.Step();
            Assert.IsFalse(drag.IsPossible);
            Assert.IsFalse(drag.IsActive);
            Assert.IsEmpty(drag.Instances, "Finished instances are removed at the end of the update");
        }

        [Test]
        public void Bind_ARunningGesture_AndInAreaWithAFunction()
        {
            var drag = _h.Enable(new Intention("Drag", _h.Intentions)
                .Bind(_h.Touch.System.Get<DragGesture>())
                .InArea(p => p.PositionDp.x < 200));
            var log = _h.Log(drag);

            DragFinger(1, new Vector2(300, 100), new Vector2(350, 100));
            DragFinger(2, new Vector2(100, 100), new Vector2(150, 100));

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(log.WithoutPerformed()));
            Assert.Throws<ArgumentNullException>(() => new Intention().Bind((IGesture)null));
            Assert.Throws<ArgumentNullException>(() => new Intention().Bind((GestureBase)null));
        }

        // ---- FingerRule ----------------------------------------------------------------------------

        /// <summary>A two-finger pan; the area is the left half. Returns whether the intention became a candidate.</summary>
        private bool PanJoins(FingerRule rule, Vector2 first, Vector2 second, out object target)
        {
            var pan = _h.Enable(new Intention($"Pan {rule}", _h.Intentions).Bind<TwoFingerPanGesture>()
                .InArea(Area.From(p => p.PositionDp.x < 200, p => p.FingerId)).WithFingerRule(rule));
            var log = _h.Log(pan);
            _h.Input.Down(1, first);
            _h.Input.Step();
            _h.Input.Down(2, second);
            _h.Input.Step();
            _h.Input.Up(1);
            _h.Input.Up(2);
            _h.Input.Step();
            pan.Enabled = false;
            target = log.Events.Count > 0 ? log.Events[0].Target : null;
            return log.Count(IntentionEventType.Possible) == 1;
        }

        private static readonly Vector2 Left = new Vector2(100, 300);
        private static readonly Vector2 Right = new Vector2(300, 300);

        [Test]
        public void FingerRule_Any_PrefersThePrimaryFingersTarget()
        {
            Assert.IsTrue(PanJoins(FingerRule.Any, Left, Right, out var primaryIn));
            Assert.AreEqual(1, primaryIn);
            Assert.IsTrue(PanJoins(FingerRule.Any, Right, Left, out var secondIn), "The second finger is enough");
            Assert.AreEqual(2, secondIn, "The target is the finger that was in the area");
        }

        [Test]
        public void FingerRule_All()
        {
            Assert.IsFalse(PanJoins(FingerRule.All, Left, Right, out _));
            Assert.IsTrue(PanJoins(FingerRule.All, Left, Left + new Vector2(50, 0), out var target));
            Assert.AreEqual(1, target, "The primary finger's target");
        }

        [Test]
        public void FingerRule_Primary()
        {
            Assert.IsTrue(PanJoins(FingerRule.Primary, Left, Right, out _));
            Assert.IsFalse(PanJoins(FingerRule.Primary, Right, Left, out _));
        }

        [Test]
        public void AFingerWhoseTouchDownWasMissed_UsesTheGesturePosition()
        {
            // The IntentionSystem was not running when finger 1 touched down, so it never saw where.
            _h.Intentions.Shutdown();
            _h.Input.Down(1, new Vector2(100, 300));
            _h.Input.Step();
            _h.Intentions.Startup();
            var pan = _h.Enable(new Intention("Pan", _h.Intentions).Bind<TwoFingerPanGesture>()
                .InArea(Area.From(p => p.PositionDp.x < 250)).WithFingerRule(FingerRule.All));
            var log = _h.Log(pan);

            _h.Input.Down(2, new Vector2(200, 300)); // the pan's centroid: x = 150
            _h.Input.Step();

            Assert.AreEqual(1, log.Count(IntentionEventType.Possible));
        }

        // ---- Requirements: waiting, then cancelled ---------------------------------------------------------

        [Test]
        public void RequireFailure_DragUnlessItBecomesASwipe()
        {
            var drag = _h.Enable(new Intention("Slow drag", _h.Intentions).Bind<DragGesture>().RequireFailure<SwipeGesture>());
            var log = _h.Log(drag);

            // Slow: the swipe fails on release, so the drag starts then, carrying all its movement at once.
            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(140, 100), 800);
            _h.Input.Hold(200);
            Assert.AreEqual(IntentionState.Waiting, drag.Instances[0].State);
            _h.Input.Up(1);
            _h.Input.Step();
            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Performed", "Ended(Completed)" }, Names(log.Entries));
            Assert.AreEqual(40f, log.Events[2].DeltaDp.x, 0.01f);

            // Fast: it was a swipe.
            log.Clear();
            DragFinger(2, new Vector2(100, 100), new Vector2(300, 100), 60);
            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(RequiredGestureEntered)" }, Names(log.Entries));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Waiting_ThenTheOsCancelsTheTouch_IsRejected()
        {
            var drag = _h.Enable(new Intention("Slow drag", _h.Intentions).Bind<DragGesture>().RequireFailure<SwipeGesture>());
            var log = _h.Log(drag);
            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(140, 100), 300);
            _h.Input.Cancel(1);
            _h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(Cancelled)" }, Names(log.Entries));
        }

        [Test]
        public void ActiveDrag_OsCancel_EndsCancelled()
        {
            var drag = _h.Enable(new Intention("Drag", _h.Intentions).Bind<DragGesture>());
            var log = _h.Log(drag);
            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(150, 100), 100);
            _h.Input.Cancel(1);
            _h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Cancelled)" }, Names(log.WithoutPerformed()));
        }

        // ---- IntentionSystem lifecycle and misuse ----------------------------------------------------------

        [Test]
        public void Lifecycle_Misuse_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new IntentionSystem(null));
            Assert.Throws<InvalidOperationException>(() => _h.Intentions.Startup(), "Already running");

            _h.Intentions.Shutdown();
            _h.Intentions.Shutdown(); // safe twice
            Assert.IsFalse(_h.Intentions.IsRunning);
            Assert.Throws<InvalidOperationException>(() => _h.Intentions.Update(), "Not running");

            var notStarted = new IntentionSystem(new TouchSystem());
            Assert.Throws<InvalidOperationException>(() => notStarted.Startup(), "TouchSystem not started");
            Assert.IsFalse(notStarted.IsRunning);
        }

        [Test]
        public void Startup_WithABadIntention_LeavesNothingRunning()
        {
            _h.Intentions.Shutdown();
            var bad = new Intention("Bad", _h.Intentions).Bind<WindowsDoubleTap>(); // not in the Android lineup
            bad.Enabled = true; // not running: registers without resolving

            Assert.Throws<InvalidOperationException>(() => _h.Intentions.Startup());
            Assert.IsFalse(_h.Intentions.IsRunning);

            bad.Enabled = false;
            _h.Intentions.Startup();
            Assert.IsTrue(_h.Intentions.IsRunning);
        }

        [Test]
        public void Update_FromAnIntentionEvent_IsRefused()
        {
            var drag = _h.Enable(new Intention("Drag", _h.Intentions).Bind<DragGesture>());
            drag.Possible += _ => _h.Intentions.Update();

            LogAssert.Expect(LogType.Exception, new Regex("cannot be called from an intention event"));
            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();
        }

        [Test]
        public void Shutdown_WhileGestureEventsAreQueued_DropsThem()
        {
            var drag = _h.Enable(new Intention("Drag", _h.Intentions).Bind<DragGesture>().Bind<PressGesture>());
            var log = _h.Log(drag);
            // Gestures send events during TouchSystem.Update; shut down in the middle of it (Press is before Drag in the lineup).
            _h.Touch.System.Get<DragGesture>().Possible += _ => _h.Intentions.Shutdown();

            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();

            Assert.IsFalse(_h.Intentions.IsRunning);
            Assert.IsEmpty(log.Entries, "The queued Press event was dropped, never handled");
        }

        [Test]
        public void Shutdown_FromAnIntentionEvent_EndsEverythingSafely()
        {
            var drag = _h.Enable(new Intention("Drag", _h.Intentions).Bind<DragGesture>());
            var other = _h.Enable(new Intention("Other", _h.Intentions).Bind<DragGesture>());
            drag.Started += _ => _h.Intentions.Shutdown();
            var dragLog = _h.Log(drag);
            var otherLog = _h.Log(other);

            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));

            Assert.IsFalse(_h.Intentions.IsRunning);
            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Disabled)" }, Names(dragLog.WithoutPerformed()));
            Assert.AreEqual("Rejected(Disabled)", Names(otherLog.Entries).Last());
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Startup_FromAnIntentionEvent_IsRefused_AfterShutdown()
        {
            var drag = _h.Enable(new Intention("Drag", _h.Intentions).Bind<DragGesture>());
            var once = true;
            drag.Started += _ =>
            {
                if (!once) return;
                once = false;
                _h.Intentions.Shutdown();
                _h.Intentions.Startup(); // throws: the update that sent this event is still running
            };

            LogAssert.Expect(LogType.Exception, new Regex("Startup cannot be called from an intention event"));
            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));
            Assert.IsFalse(_h.Intentions.IsRunning);

            _h.Intentions.Startup(); // fine afterwards, and works
            var log = _h.Log(drag);
            DragFinger(2, new Vector2(100, 100), new Vector2(200, 100));
            Assert.AreEqual(1, log.Count(IntentionEventType.Started));
        }
    }
}
