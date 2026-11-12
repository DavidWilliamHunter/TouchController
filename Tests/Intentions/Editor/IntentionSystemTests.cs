using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using Threads.TouchController.Intentions.Testing;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>The intention system on the full Android lineup, driven by scripted touches.</summary>
    public class IntentionSystemTests
    {
        private IntentionSystemHarness _h;

        [SetUp]
        public void SetUp() => _h = new IntentionSystemHarness();

        [TearDown]
        public void TearDown() => _h?.Dispose();

        private Intention New(string name) => new Intention(name, _h.Intentions);

        /// <summary>Entries without instance IDs (IDs depend on creation order, which these tests don't care about).</summary>
        private static List<string> Names(IEnumerable<string> entries) => entries.Select(e => Regex.Replace(e, "#\\d+$", "")).ToList();

        private void Tap(int id, Vector2 at)
        {
            _h.Input.Down(id, at);
            _h.Input.Step();
            _h.Input.Up(id);
            _h.Input.Step();
        }

        private void DragFinger(int id, Vector2 from, Vector2 to, double durationMs = 200)
        {
            _h.Input.Down(id, from);
            _h.Input.Step();
            _h.Input.Drag(id, to, durationMs);
            _h.Input.Up(id);
            _h.Input.Step();
        }

        // ---- Lifecycle -----------------------------------------------------------------------------

        [Test]
        public void Drag_PossibleStartedPerformedEnded_DeltasAddUpToTheMovement()
        {
            var drag = _h.Enable(New("Drag").Bind<DragGesture>());
            var log = _h.Log(drag);

            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));

            CollectionAssert.AreEqual(new[] { "Possible#1", "Started#1", "Ended(Completed)#1" }, log.WithoutPerformed());
            Assert.Greater(log.Count(IntentionEventType.Performed), 2);
            var moved = log.Events.Aggregate(Vector2.zero, (sum, e) => sum + e.DeltaDp);
            Assert.AreEqual(100f, moved.x, 0.01f);
            Assert.AreEqual(0f, moved.y, 0.01f);
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Tap_StartedPerformedEnded_InOneUpdate()
        {
            var tap = _h.Enable(New("Tap").Bind<TapGesture>());
            var log = _h.Log(tap);

            Tap(1, new Vector2(100, 100));

            CollectionAssert.AreEqual(new[] { "Possible#1", "Started#1", "Performed#1", "Ended(Completed)#1" }, log.Entries);
            Assert.AreEqual(log.Events[1].Time, log.Events[3].Time);
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Disabled_IsSilent_AndDisablingMidDragEndsIt()
        {
            var drag = New("Drag").Bind<DragGesture>();
            var log = _h.Log(drag);
            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));
            Assert.IsEmpty(log.Entries, "Not enabled yet");

            drag.Enabled = true;
            CollectionAssert.Contains(_h.Intentions.Intentions, drag);
            _h.Input.Down(2, new Vector2(100, 100));
            _h.Input.Step();
            _h.Input.Drag(2, new Vector2(150, 100), 100);
            drag.Enabled = false;
            _h.Input.Drag(2, new Vector2(200, 100), 100);
            _h.Input.Up(2);
            _h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Disabled)" }, Names(log.WithoutPerformed()));
            CollectionAssert.DoesNotContain(_h.Intentions.Intentions, drag);
            Assert.IsEmpty(drag.Instances);
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void GestureDisabled_EndsItsIntentions()
        {
            var drag = _h.Enable(New("Drag").Bind<DragGesture>());
            var log = _h.Log(drag);
            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(150, 100), 100);

            _h.Touch.System.Get<DragGesture>().Enabled = false;
            _h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Disabled)" }, Names(log.WithoutPerformed()));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void EnabledBeforeStartup_RegistersAtStartup_ShutdownEndsInstancesButKeepsEnabled()
        {
            _h.Intentions.Shutdown();
            var drag = New("Drag").Bind<DragGesture>();
            drag.Enabled = true;
            var log = _h.Log(drag);
            _h.Intentions.Startup();

            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(150, 100), 100);
            _h.Intentions.Shutdown();

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Disabled)" }, Names(log.WithoutPerformed()));
            Assert.IsTrue(drag.Enabled);

            _h.Input.Up(1);
            _h.Input.Step();
            _h.Intentions.Startup();
            log.Clear();
            DragFinger(2, new Vector2(100, 100), new Vector2(200, 100));
            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(log.WithoutPerformed()));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void BindingAGestureNotInTheLineup_ThrowsOnEnable_AndStaysDisabled()
        {
            var intention = New("Missing").Bind<WindowsDoubleTap>(); // the Android lineup has AndroidDoubleTap only
            Assert.Throws<InvalidOperationException>(() => intention.Enabled = true);
            Assert.IsFalse(intention.Enabled);
            CollectionAssert.IsEmpty(_h.Intentions.Intentions);
        }

        [Test]
        public void ChangingBindingsWhileEnabled_Throws()
        {
            var drag = _h.Enable(New("Drag").Bind<DragGesture>());
            Assert.Throws<InvalidOperationException>(() => drag.Bind<TapGesture>());
        }

        [Test]
        public void BindByAsset_UsesTheRunningCopy()
        {
            var asset = DefaultTouchSettings.Load(TouchPlatform.Android).Lineup.First(e => e.gesture is DragGesture).gesture;
            var drag = _h.Enable(New("Drag").Bind(asset));
            var log = _h.Log(drag);

            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(log.WithoutPerformed()));
        }

        [Test]
        public void HandlerException_IsLogged_AndEventsContinue()
        {
            var drag = _h.Enable(New("Drag").Bind<DragGesture>());
            drag.Started += _ => throw new InvalidOperationException("boom");
            var log = _h.Log(drag);

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(log.WithoutPerformed()));
        }

        [Test]
        public void Unsubscribe_StopsThatHandlerOnly()
        {
            var tap = _h.Enable(New("Tap").Bind<TapGesture>());
            int a = 0, b = 0;
            Action<IntentionEventData> countA = _ => a++;
            Action<IntentionEventData> countB = _ => b++;
            tap.Performed += countA;
            tap.Performed += countB;

            Tap(1, new Vector2(100, 100));
            tap.Performed -= countA;
            _h.Input.Hold(400);
            Tap(2, new Vector2(300, 300));

            Assert.AreEqual(1, a);
            Assert.AreEqual(2, b);
        }

        // ---- Areas ---------------------------------------------------------------------------------

        [Test]
        public void Area_CheckedAtTouchDown_LeavingItLaterDoesNotMatter()
        {
            var drag = _h.Enable(New("Drag").Bind<DragGesture>().InArea(ScreenRectArea.Dp(new Rect(0, 0, 200, 200))));
            var log = _h.Log(drag);

            DragFinger(1, new Vector2(300, 300), new Vector2(100, 100));
            Assert.IsEmpty(log.Entries, "Started outside the area");

            DragFinger(2, new Vector2(100, 100), new Vector2(350, 100));
            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(log.WithoutPerformed()));
        }

        [Test]
        public void Area_Continuous_LeavingEndsIt()
        {
            var drag = _h.Enable(New("Drag").Bind<DragGesture>().InArea(ScreenRectArea.Dp(new Rect(0, 0, 200, 200)), AreaCheck.Continuous));
            var log = _h.Log(drag);

            DragFinger(1, new Vector2(100, 100), new Vector2(350, 100));

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(LeftArea)" }, Names(log.WithoutPerformed()));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Area_TargetIsReported()
        {
            var button = new object();
            var tap = _h.Enable(New("Tap").Bind<TapGesture>().InArea(Area.From(p => p.PositionDp.x < 200, p => button)));
            var log = _h.Log(tap);

            Tap(1, new Vector2(100, 100));

            Assert.IsTrue(log.Events.All(e => e.Target == button));
        }

        // ---- Priority and claims -------------------------------------------------------------------

        /// <summary>The frame from plan.md §9.5: draggable from anywhere inside, with a button inside it.</summary>
        private (Intention frame, Intention button) FrameWithButton()
        {
            var frame = _h.Enable(New("Frame drag").Bind<DragGesture>()
                .InArea(ScreenRectArea.Dp(new Rect(50, 50, 300, 300))).WithPriority(10).Exclusive());
            var button = _h.Enable(New("Button").Bind<TapGesture>()
                .InArea(ScreenRectArea.Dp(new Rect(100, 100, 80, 40))));
            return (frame, button);
        }

        [Test]
        public void Frame_DragFromAButton_MovesTheFrame_ButtonIsRejected()
        {
            var (frame, button) = FrameWithButton();
            var frameLog = _h.Log(frame);
            var buttonLog = _h.Log(button);

            DragFinger(1, new Vector2(120, 120), new Vector2(250, 250));

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(frameLog.WithoutPerformed()));
            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(GestureFailed)" }, Names(buttonLog.Entries));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Frame_TapOnAButton_PressesIt_FrameIsRejected()
        {
            var (frame, button) = FrameWithButton();
            var frameLog = _h.Log(frame);
            var buttonLog = _h.Log(button);

            Tap(1, new Vector2(120, 120));

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Performed", "Ended(Completed)" }, Names(buttonLog.Entries));
            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(GestureFailed)" }, Names(frameLog.Entries));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Frame_ResultDoesNotDependOnLineupOrder()
        {
            List<string> Run(IntentionSystemHarness h)
            {
                var frame = h.Enable(new Intention("Frame", h.Intentions).Bind<DragGesture>().Bind<TapGesture>()
                    .InArea(ScreenRectArea.Dp(new Rect(50, 50, 300, 300))).WithPriority(10).Exclusive());
                var button = h.Enable(new Intention("Button", h.Intentions).Bind<TapGesture>().Bind<DragGesture>()
                    .InArea(ScreenRectArea.Dp(new Rect(100, 100, 80, 40))));
                var frameLog = h.Log(frame);
                var buttonLog = h.Log(button);
                h.Input.Down(1, new Vector2(120, 120));
                h.Input.Step();
                h.Input.Drag(1, new Vector2(250, 250), 200);
                h.Input.Up(1);
                h.Input.Step();
                return frameLog.Entries.Select(e => "frame " + e).Concat(buttonLog.Entries.Select(e => "button " + e)).ToList();
            }

            var reversed = Object.Instantiate(DefaultTouchSettings.Load(TouchPlatform.Android));
            reversed.hideFlags = HideFlags.DontSave;
            reversed.LineupList.Reverse();
            try
            {
                List<string> normal, backwards;
                using (var h = new IntentionSystemHarness()) normal = Run(h);
                using (var h = new IntentionSystemHarness(settings: reversed)) backwards = Run(h);

                CollectionAssert.AreEqual(Names(normal), Names(backwards));
                CollectionAssert.Contains(Names(normal), "button Rejected(LostToPriority)");
            }
            finally
            {
                Object.DestroyImmediate(reversed);
            }
        }

        [Test]
        public void Exclusive_HigherPriorityTakesTheFinger()
        {
            var low = _h.Enable(New("Low").Bind<DragGesture>());
            var high = _h.Enable(New("High").Bind<DragGesture>().WithPriority(10).Exclusive());
            var lowLog = _h.Log(low);
            var highLog = _h.Log(high);

            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(highLog.WithoutPerformed()));
            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(LostToPriority)" }, Names(lowLog.Entries));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Shared_IntentionsOverlap()
        {
            var a = _h.Enable(New("A").Bind<DragGesture>());
            var b = _h.Enable(New("B").Bind<DragGesture>().WithPriority(5));
            var aLog = _h.Log(a);
            var bLog = _h.Log(b);

            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(aLog.WithoutPerformed()));
            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(bLog.WithoutPerformed()));
        }

        [Test]
        public void Exclusive_EqualPriority_FirstEnabledWins()
        {
            var first = _h.Enable(New("First").Bind<DragGesture>().Exclusive());
            var second = _h.Enable(New("Second").Bind<DragGesture>().Exclusive());
            var firstLog = _h.Log(first);
            var secondLog = _h.Log(second);

            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(firstLog.WithoutPerformed()));
            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(LostToPriority)" }, Names(secondLog.Entries));
        }

        private void MoveTogether(int steps, Vector2 by, params (int id, Vector2 from)[] fingers)
        {
            for (var k = 1; k <= steps; k++)
            {
                foreach (var (id, from) in fingers)
                    _h.Input.Move(id, from + by * k);
                _h.Input.Step();
            }
        }

        [Test]
        public void Exclusive_TwoFingerPanPreemptsADrag()
        {
            var card = _h.Enable(New("Card drag").Bind<DragGesture>());
            var pan = _h.Enable(New("Pan").Bind<TwoFingerPanGesture>().WithPriority(20).Exclusive());
            var cardLog = _h.Log(card);
            var panLog = _h.Log(pan);

            _h.Input.Down(1, new Vector2(100, 300));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(150, 300), 100);
            _h.Input.Down(2, new Vector2(300, 300));
            _h.Input.Step();
            MoveTogether(10, new Vector2(0, 5), (1, new Vector2(150, 300)), (2, new Vector2(300, 300)));
            _h.Input.Up(1);
            _h.Input.Up(2);
            _h.Input.Step();

            var cardNames = Names(cardLog.WithoutPerformed());
            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Possible", "Ended(Preempted)", "Rejected(LostToPriority)" }, cardNames);
            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(panLog.WithoutPerformed()));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void Exclusive_ActiveIntentionKeepsLowerOnesOffItsFinger()
        {
            var frame = _h.Enable(New("Frame").Bind<DragGesture>().WithPriority(10).Exclusive());
            var pan = _h.Enable(New("Pan").Bind<TwoFingerPanGesture>());
            var panLog = _h.Log(pan);

            _h.Input.Down(1, new Vector2(100, 300));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(150, 300), 100);
            Assert.IsTrue(frame.IsActive);
            _h.Input.Down(2, new Vector2(300, 300));
            _h.Input.Step();
            MoveTogether(10, new Vector2(0, 5), (1, new Vector2(150, 300)), (2, new Vector2(300, 300)));

            Assert.IsEmpty(panLog.Entries, "Finger 1 belongs to the frame: the pan never becomes a candidate");
            Assert.IsTrue(frame.IsActive);
        }

        [Test]
        public void TwoThumbs_TwoIndependentInstances()
        {
            var drag = _h.Enable(New("Drag").Bind<DragGesture>().Exclusive());
            var log = _h.Log(drag);

            _h.Input.Down(1, new Vector2(100, 100));
            _h.Input.Down(2, new Vector2(300, 100));
            _h.Input.Step();
            MoveTogether(10, new Vector2(0, 5), (1, new Vector2(100, 100)), (2, new Vector2(300, 100)));
            Assert.AreEqual(2, drag.Instances.Count(i => i.IsActive));
            _h.Input.Up(1);
            _h.Input.Up(2);
            _h.Input.Step();

            Assert.AreEqual(2, log.Count(IntentionEventType.Started));
            Assert.AreEqual(2, log.Count(IntentionEventType.Ended));
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, log.Events.Where(e => e.Type == IntentionEventType.Started).Select(e => e.FingerId));
            Assert.IsEmpty(_h.Violations());
        }

        // ---- Requirements --------------------------------------------------------------------------

        [Test]
        public void RequireFailure_SingleTapWaitsForDoubleTapToFail()
        {
            var tap = _h.Enable(New("Move unit").Bind<TapGesture>().RequireFailure<DoubleTapGesture>());
            var log = _h.Log(tap);

            Tap(1, new Vector2(100, 100));
            CollectionAssert.AreEqual(new[] { "Possible" }, Names(log.Entries));
            Assert.AreEqual(IntentionState.Waiting, tap.Instances[0].State);

            _h.Input.Hold(350); // past the 300 ms double-tap time-out

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Performed", "Ended(Completed)" }, Names(log.Entries));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void RequireFailure_DoubleTapRejectsBothTaps()
        {
            var tap = _h.Enable(New("Move unit").Bind<TapGesture>().RequireFailure<DoubleTapGesture>());
            var log = _h.Log(tap);

            Tap(1, new Vector2(100, 100));
            _h.Input.Hold(100);
            Tap(2, new Vector2(105, 100));
            _h.Input.Hold(400);

            CollectionAssert.AreEqual(
                new[] { "Possible", "Possible", "Rejected(RequiredGestureEntered)", "Rejected(RequiredGestureEntered)" },
                Names(log.Entries));
            Assert.IsEmpty(_h.Violations());
        }

        [Test]
        public void RequireActive_DragOnlyAfterALongPress()
        {
            var dragUnit = _h.Enable(New("Drag unit").Bind<DragGesture>().RequireActive<LongPressGesture>());
            var log = _h.Log(dragUnit);

            DragFinger(1, new Vector2(100, 100), new Vector2(200, 100));
            CollectionAssert.AreEqual(new[] { "Possible", "Rejected(RequirementNotMet)" }, Names(log.Entries));

            log.Clear();
            _h.Input.Down(2, new Vector2(100, 100));
            _h.Input.Step();
            _h.Input.Hold(450); // long press enters at 400 ms
            _h.Input.Drag(2, new Vector2(200, 100), 200);
            _h.Input.Up(2);
            _h.Input.Step();

            CollectionAssert.AreEqual(new[] { "Possible", "Started", "Ended(Completed)" }, Names(log.WithoutPerformed()));
            Assert.IsEmpty(_h.Violations());
        }
    }
}
