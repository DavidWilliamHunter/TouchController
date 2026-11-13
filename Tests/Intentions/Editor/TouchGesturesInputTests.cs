using System;
using System.Linq;
using NUnit.Framework;
using Threads.TouchController.Intentions;
using Threads.TouchController.Intentions.Testing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Threads.TouchController.Tests.Intentions
{
    /// <summary>
    /// The TouchGestures device and GestureInputOutput: gestures (Android lineup, scripted touches, 1 px = 1 dp,
    /// screen 400 × 800 dp) read through Input System actions.
    /// </summary>
    public class TouchGesturesInputTests : InputTestFixture
    {
        private IntentionSystemHarness _h;
        private TouchGesturesDevice _device;
        private GestureInputOutput _feed;

        public override void Setup()
        {
            base.Setup();
            _h = new IntentionSystemHarness();
            _device = new TouchGesturesDevice();
        }

        public override void TearDown()
        {
            _feed?.Dispose();
            _device.Dispose();
            _h.Dispose();
            base.TearDown();
        }

        private GestureInputOutput Feed(GestureInputOptions options = null)
        {
            _feed = new GestureInputOutput(_h.Intentions, _device, options) { Enabled = true };
            return _feed;
        }

        private InputAction Action(string binding, InputActionType type = InputActionType.Button)
        {
            var action = new InputAction(binding, type, binding);
            action.Enable();
            return action;
        }

        private void Tap(Vector2 at)
        {
            _h.Input.Down(1, at);
            _h.Input.Step();
            _h.Input.Up(1);
            _h.Input.Step();
        }

        private void SwipeFrom(Vector2 from, Vector2 to)
        {
            _h.Input.Down(1, from);
            _h.Input.Step();
            _h.Input.Drag(1, to, durationMs: 80);
            _h.Input.Up(1);
            _h.Input.Step();
        }

        // ---- Device ------------------------------------------------------------------------------------

        [Test]
        public void Device_IsAddedWithItsLayout_AndRemovedOnDispose()
        {
            var device = _device.Device;
            Assert.IsTrue(device.added);
            Assert.AreEqual(TouchGestures.LayoutName, device.layout);
            Assert.AreSame(device.swipeLeft, device[TouchGestureButton.SwipeLeft]);

            _device.Dispose();
            Assert.IsFalse(device.added);
            Assert.IsNull(_device.Device);
            _device = new TouchGesturesDevice(); // for TearDown
        }

        [Test]
        public void Usage_TellsDevicesApartInBindings()
        {
            using var left = new TouchGesturesDevice("LeftHalf");
            var action = Action("<TouchGestures>{LeftHalf}/tap");
            try
            {
                CollectionAssert.AreEqual(new[] { left.Device.tap }, action.controls.ToArray());
            }
            finally
            {
                action.Dispose();
            }
        }

        [Test]
        public void Pulse_IsDownForExactlyOneUpdate()
        {
            var action = Action("<TouchGestures>/tap");
            var performed = 0;
            action.performed += _ => performed++;
            try
            {
                _device.Pulse(TouchGestureButton.Tap, new Vector2(10, 20));
                InputSystem.Update();
                Assert.IsTrue(action.WasPerformedThisFrame());
                Assert.IsTrue(_device.Device.tap.isPressed);
                Assert.AreEqual(new Vector2(10, 20), _device.Device.position.ReadValue());

                InputSystem.Update();
                Assert.IsFalse(_device.Device.tap.isPressed);
                Assert.AreEqual(new Vector2(10, 20), _device.Device.position.ReadValue(), "the position stays");
                Assert.AreEqual(1, performed);
            }
            finally
            {
                action.Dispose();
            }
        }

        [Test]
        public void PulsesInConsecutiveUpdates_AreTwoPresses()
        {
            var action = Action("<TouchGestures>/doubleTap");
            var performed = 0;
            action.performed += _ => performed++;
            try
            {
                _device.Pulse(TouchGestureButton.DoubleTap, Vector2.zero);
                InputSystem.Update();
                _device.Pulse(TouchGestureButton.DoubleTap, Vector2.zero);
                InputSystem.Update();
                Assert.AreEqual(2, performed);
                Assert.IsTrue(_device.Device.doubleTap.isPressed);
            }
            finally
            {
                action.Dispose();
            }
        }

        [Test]
        public void PressAndReleaseBeforeAnUpdate_IsStillSeenOnce()
        {
            _device.Press(TouchGestureButton.LongPress, Vector2.zero);
            _device.Release(TouchGestureButton.LongPress);
            InputSystem.Update();
            Assert.IsTrue(_device.Device.longPress.isPressed);
            InputSystem.Update();
            Assert.IsFalse(_device.Device.longPress.isPressed);
        }

        [Test]
        public void Deltas_HoldOneUpdatesMovement_ThenZero()
        {
            _device.AddDrag(new Vector2(3, 4));
            _device.AddDrag(new Vector2(1, 1));
            _device.AddPinch(1.1f);
            _device.AddPinch(1.1f);
            _device.AddRotate(5f);
            InputSystem.Update();
            Assert.AreEqual(new Vector2(4, 5), _device.Device.drag.ReadValue());
            Assert.AreEqual(0.21f, _device.Device.pinch.ReadValue(), 1e-4f, "factors multiply: 1.1 × 1.1 − 1");
            Assert.AreEqual(5f, _device.Device.rotate.ReadValue(), 1e-4f);

            InputSystem.Update();
            Assert.AreEqual(Vector2.zero, _device.Device.drag.ReadValue());
            Assert.AreEqual(0f, _device.Device.pinch.ReadValue());
            Assert.AreEqual(0f, _device.Device.rotate.ReadValue());
        }

        // ---- Fed by gestures ---------------------------------------------------------------------------

        [Test]
        public void Feed_NeedsRunningSystems()
        {
            using var stopped = new IntentionSystemHarness();
            stopped.Intentions.Shutdown();
            stopped.Touch.System.Shutdown();
            Assert.Throws<InvalidOperationException>(() => new GestureInputOutput(stopped.Intentions, _device));
        }

        [Test]
        public void Feed_MapsTheGesturesAskedFor_ThatTheLineupHas()
        {
            Assert.AreEqual(TouchGestureKinds.All, Feed().Mapped);
            _feed.Dispose();
            var some = Feed(new GestureInputOptions { Gestures = TouchGestureKinds.Tap | TouchGestureKinds.Swipe });
            Assert.AreEqual(TouchGestureKinds.Tap | TouchGestureKinds.Swipe, some.Mapped);
            Assert.AreEqual(2, some.Intentions.Count);
        }

        [Test]
        public void Tap_ReachesABoundAction_WithItsPosition()
        {
            Feed();
            var select = Action("<TouchGestures>/tap");
            var performed = 0;
            select.performed += _ => performed++;
            try
            {
                Tap(new Vector2(120, 340));
                InputSystem.Update();
                Assert.AreEqual(1, performed);
                Assert.AreEqual(new Vector2(120, 340), _device.Device.position.ReadValue());
                InputSystem.Update();
                Assert.IsFalse(select.IsPressed());
            }
            finally
            {
                select.Dispose();
            }
        }

        [Test]
        public void Swipes_PressTheirDirectionsButton_AndSetTheSwipeVector()
        {
            Feed();
            var next = Action("<TouchGestures>/swipeLeft");
            next.AddBinding("<Keyboard>/rightArrow"); // beside the usual binding
            var previous = Action("<TouchGestures>/swipeRight");
            var nextCount = 0;
            var previousCount = 0;
            next.performed += _ => nextCount++;
            previous.performed += _ => previousCount++;
            try
            {
                SwipeFrom(new Vector2(300, 400), new Vector2(150, 400));
                InputSystem.Update();
                Assert.AreEqual(1, nextCount);
                Assert.AreEqual(0, previousCount);
                Assert.AreEqual(Vector2.left, _device.Device.swipe.ReadValue());

                InputSystem.Update();
                Assert.AreEqual(Vector2.zero, _device.Device.swipe.ReadValue());

                SwipeFrom(new Vector2(100, 400), new Vector2(250, 400));
                InputSystem.Update();
                Assert.AreEqual(1, nextCount);
                Assert.AreEqual(1, previousCount);
            }
            finally
            {
                next.Dispose();
                previous.Dispose();
            }
        }

        [Test]
        public void Drag_HoldsDragging_AndReportsMovementLikeAPointerDelta()
        {
            Feed();
            var look = Action("<TouchGestures>/drag", InputActionType.Value);
            try
            {
                _h.Input.Down(1, new Vector2(100, 400));
                _h.Input.Step();
                _h.Input.Move(1, new Vector2(140, 400)); // beyond the slop: the drag enters
                _h.Input.Step();
                InputSystem.Update();
                Assert.IsTrue(_device.Device.dragging.isPressed);

                _h.Input.Move(1, new Vector2(150, 410));
                _h.Input.Step();
                _h.Input.Move(1, new Vector2(155, 420));
                _h.Input.Step();
                InputSystem.Update();
                Assert.AreEqual(new Vector2(15, 20), look.ReadValue<Vector2>(), "both steps since the last update");

                InputSystem.Update();
                Assert.AreEqual(Vector2.zero, look.ReadValue<Vector2>());
                Assert.IsTrue(_device.Device.dragging.isPressed, "still dragging, not moving");

                _h.Input.Up(1);
                _h.Input.Step();
                InputSystem.Update();
                Assert.IsFalse(_device.Device.dragging.isPressed);
            }
            finally
            {
                look.Dispose();
            }
        }

        [Test]
        public void Pinch_ReportsTheScaleChange()
        {
            Feed();
            _h.Input.Down(1, new Vector2(150, 400));
            _h.Input.Down(2, new Vector2(250, 400));
            _h.Input.Step();
            InputSystem.Update();
            var total = 1f;
            for (var i = 1; i <= 5; i++)
            {
                _h.Input.Move(1, new Vector2(150 - 10 * i, 400));
                _h.Input.Move(2, new Vector2(250 + 10 * i, 400));
                _h.Input.Step();
                InputSystem.Update();
                total *= 1f + _device.Device.pinch.ReadValue();
            }
            Assert.IsTrue(_device.Device.pinching.isPressed);
            Assert.That(total, Is.GreaterThan(1.2f).And.LessThanOrEqualTo(2f), "spread from 100 to 200 dp; the first steps are within the slop");
        }

        [Test]
        public void LongPress_IsHeldUntilTheFingerLifts()
        {
            Feed();
            var hold = Action("<TouchGestures>/longPress");
            try
            {
                _h.Input.Down(1, new Vector2(200, 400));
                _h.Input.Step();
                _h.Input.Hold(1000);
                InputSystem.Update();
                Assert.IsTrue(hold.IsPressed());
                _h.Input.Up(1);
                _h.Input.Step();
                InputSystem.Update();
                Assert.IsFalse(hold.IsPressed());
            }
            finally
            {
                hold.Dispose();
            }
        }

        [Test]
        public void EdgeSwipe_HoldsItsEdgesButton()
        {
            Feed();
            _h.Input.Down(1, new Vector2(2, 400));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(80, 400), durationMs: 200);
            InputSystem.Update();
            Assert.IsTrue(_device.Device.edgeSwipeLeft.isPressed);
            Assert.IsFalse(_device.Device.edgeSwipeRight.isPressed);
            _h.Input.Up(1);
            _h.Input.Step();
            InputSystem.Update();
            Assert.IsFalse(_device.Device.edgeSwipeLeft.isPressed);
        }

        [Test]
        public void Area_LimitsWhereGesturesCount()
        {
            Feed(new GestureInputOptions { Area = ScreenRectArea.Dp(new Rect(0, 0, 200, 800)) }); // left half
            Tap(new Vector2(300, 400));
            InputSystem.Update();
            Assert.IsFalse(_device.Device.tap.isPressed);

            Tap(new Vector2(100, 400));
            InputSystem.Update();
            Assert.IsTrue(_device.Device.tap.isPressed);
        }

        [Test]
        public void AFingerHeldByAnExclusiveIntention_DoesNotReachTheDevice()
        {
            Feed();
            var frameDrag = _h.Enable(new Intention("Frame drag", _h.Intentions).Bind<DragGesture>().WithPriority(10).Exclusive());
            var moved = Vector2.zero;
            frameDrag.Performed += e => moved += e.DeltaDp;

            _h.Input.Down(1, new Vector2(100, 400));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(200, 400), durationMs: 300);
            InputSystem.Update();
            Assert.Greater(moved.x, 50f, "the frame moved");
            Assert.IsFalse(_device.Device.dragging.isPressed);
            Assert.AreEqual(Vector2.zero, _device.Device.drag.ReadValue());
        }

        [Test]
        public void TapWaitsForDoubleTap_MakesADoubleTapOnlyADoubleTap()
        {
            Feed(new GestureInputOptions { TapWaitsForDoubleTap = true });
            var taps = Action("<TouchGestures>/tap");
            var doubles = Action("<TouchGestures>/doubleTap");
            var tapCount = 0;
            var doubleCount = 0;
            taps.performed += _ => tapCount++;
            doubles.performed += _ => doubleCount++;
            try
            {
                Tap(new Vector2(200, 400));
                InputSystem.Update();
                _h.Input.Step(80);
                Tap(new Vector2(200, 400));
                InputSystem.Update();
                InputSystem.Update();
                Assert.AreEqual(1, doubleCount);
                Assert.AreEqual(0, tapCount);

                // A single tap arrives once the double tap can no longer happen.
                Tap(new Vector2(200, 400));
                _h.Input.Hold(1000);
                InputSystem.Update();
                Assert.AreEqual(1, tapCount);
            }
            finally
            {
                taps.Dispose();
                doubles.Dispose();
            }
        }

        [Test]
        public void DisposingTheFeedWhileDragging_ReleasesDragging()
        {
            Feed();
            _h.Input.Down(1, new Vector2(100, 400));
            _h.Input.Step();
            _h.Input.Drag(1, new Vector2(200, 400), durationMs: 200);
            InputSystem.Update();
            Assert.IsTrue(_device.Device.dragging.isPressed);

            _feed.Dispose();
            InputSystem.Update();
            Assert.IsFalse(_device.Device.dragging.isPressed);
            Assert.IsEmpty(_h.Violations());
        }
    }
}
