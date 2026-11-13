using System;
using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController.Intentions
{
    /// <summary>Which gestures a <see cref="GestureInputOutput"/> feeds to the device.</summary>
    [Flags]
    public enum TouchGestureKinds
    {
        None = 0,
        Tap = 1 << 0,
        DoubleTap = 1 << 1,
        TwoFingerTap = 1 << 2,
        LongPress = 1 << 3,
        Swipe = 1 << 4,
        EdgeSwipe = 1 << 5,
        Drag = 1 << 6,
        Pinch = 1 << 7,
        Rotate = 1 << 8,
        TwoFingerPan = 1 << 9,
        All = (1 << 10) - 1,
    }

    /// <summary>Settings of a <see cref="GestureInputOutput"/>. Read when it is created.</summary>
    public sealed class GestureInputOptions
    {
        /// <summary>Prefix of its intentions' names (for logs).</summary>
        public string Name = "Gesture input";

        /// <summary>The gestures to feed. Those missing from the touch system's lineup are skipped (see <see cref="GestureInputOutput.Mapped"/>).</summary>
        public TouchGestureKinds Gestures = TouchGestureKinds.All;

        /// <summary>Where gestures count (e.g. <c>Area.Not(new UiRaycastArea(eventSystem))</c>: not over UI). Null: anywhere.</summary>
        public IIntentionArea Area;

        /// <summary>
        /// Priority of its intentions. They share their fingers (never block anything), and an exclusive intention of
        /// equal or higher priority (a joystick, a frame drag) takes the fingers from them.
        /// </summary>
        public int Priority;

        /// <summary>
        /// A tap waits until a double tap is no longer possible, so the second tap of a double tap is not also a
        /// tap. Taps then come later (by the double-tap timeout). Off: every tap pulses at once.
        /// </summary>
        public bool TapWaitsForDoubleTap;
    }

    /// <summary>
    /// Feeds touch gestures to a <see cref="TouchGesturesDevice"/>, so Input System actions bound to
    /// <c>&lt;TouchGestures&gt;/…</c> read them and input code needs no changes.
    /// </summary>
    /// <remarks>
    /// <para>It goes through the intention system (one shared intention per gesture), so areas apply, and a finger
    /// that an exclusive intention holds (a joystick, a dragged frame) does not reach the device.</para>
    /// <para>Needs a running <see cref="IntentionSystem"/> (it looks at the gesture lineup). Starts disabled.</para>
    /// <code>
    /// var gestures = new TouchGesturesDevice();
    /// var feed = new GestureInputOutput(intentions, gestures) { Enabled = true };
    /// // "Next" action bound to &lt;Keyboard&gt;/rightArrow and &lt;TouchGestures&gt;/swipeLeft
    /// </code>
    /// </remarks>
    public sealed class GestureInputOutput : IDisposable
    {
        private readonly TouchGesturesDevice _device;
        private readonly List<Intention> _intentions = new List<Intention>();
        private bool _enabled;
        private bool _disposed;

        public GestureInputOutput(IntentionSystem system, TouchGesturesDevice device, GestureInputOptions options = null)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            if (device.Device == null) throw new ArgumentException("The device has been disposed.", nameof(device));
            if (!system.Touch.IsRunning)
                throw new InvalidOperationException("Start the touch and intention systems first: the gestures are taken from the running lineup.");
            options ??= new GestureInputOptions();

            var want = options.Gestures;
            var touch = system.Touch;

            Intention Make<T>(TouchGestureKinds kind) where T : class, IGesture
            {
                if ((want & kind) == 0 || !touch.TryGet<T>(out _)) return null;
                var intention = new Intention($"{options.Name}: {kind}", system).Bind<T>().WithPriority(options.Priority).Shared();
                if (options.Area != null) intention.InArea(options.Area);
                _intentions.Add(intention);
                Mapped |= kind;
                return intention;
            }

            var tap = Make<TapGesture>(TouchGestureKinds.Tap);
            if (tap != null)
            {
                if (options.TapWaitsForDoubleTap && touch.TryGet<DoubleTapGesture>(out _)) tap.RequireFailure<DoubleTapGesture>();
                tap.Started += e => _device.Pulse(TouchGestureButton.Tap, e.PositionPx);
            }

            var doubleTap = Make<DoubleTapGesture>(TouchGestureKinds.DoubleTap);
            if (doubleTap != null) doubleTap.Started += e => _device.Pulse(TouchGestureButton.DoubleTap, e.PositionPx);

            var twoFingerTap = Make<TwoFingerTapGesture>(TouchGestureKinds.TwoFingerTap);
            if (twoFingerTap != null) twoFingerTap.Started += e => _device.Pulse(TouchGestureButton.TwoFingerTap, e.PositionPx);

            Held(Make<LongPressGesture>(TouchGestureKinds.LongPress), TouchGestureButton.LongPress, null);

            var swipe = Make<SwipeGesture>(TouchGestureKinds.Swipe);
            if (swipe != null)
            {
                swipe.Started += e =>
                {
                    var d = e.Direction;
                    var button = Mathf.Abs(d.x) >= Mathf.Abs(d.y)
                        ? (d.x < 0f ? TouchGestureButton.SwipeLeft : TouchGestureButton.SwipeRight)
                        : (d.y < 0f ? TouchGestureButton.SwipeDown : TouchGestureButton.SwipeUp);
                    _device.Pulse(button, e.PositionPx);
                    _device.SetSwipe(d);
                };
            }

            var edgeSwipe = Make<EdgeSwipeGesture>(TouchGestureKinds.EdgeSwipe);
            if (edgeSwipe != null)
            {
                edgeSwipe.Started += e =>
                {
                    if (TryEdgeButton(e.Edge, out var b)) _device.Press(b, e.PositionPx);
                };
                edgeSwipe.Performed += e => _device.SetPosition(e.PositionPx);
                edgeSwipe.Ended += e =>
                {
                    if (TryEdgeButton(e.Edge, out var b)) _device.Release(b);
                };
            }

            Held(Make<DragGesture>(TouchGestureKinds.Drag), TouchGestureButton.Dragging, e => _device.AddDrag(e.DeltaPx));
            Held(Make<PinchGesture>(TouchGestureKinds.Pinch), TouchGestureButton.Pinching, e => _device.AddPinch(e.ScaleDelta));
            Held(Make<RotateGesture>(TouchGestureKinds.Rotate), TouchGestureButton.Rotating, e => _device.AddRotate(e.AngleDeltaDeg));
            Held(Make<TwoFingerPanGesture>(TouchGestureKinds.TwoFingerPan), TouchGestureButton.TwoFingerPanning,
                e => _device.AddTwoFingerPan(e.DeltaPx));
        }

        /// <summary>The gestures it feeds: those asked for that the lineup has.</summary>
        public TouchGestureKinds Mapped { get; private set; }

        /// <summary>Its intentions (one per mapped gesture), e.g. to inspect or log them.</summary>
        public IReadOnlyList<Intention> Intentions => _intentions;

        /// <summary>Registers its intentions (true) or ends and releases them (false). Starts disabled.</summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_disposed && value) throw new ObjectDisposedException(nameof(GestureInputOutput));
                if (_enabled == value) return;
                _enabled = value;
                foreach (var intention in _intentions)
                    intention.Enabled = value;
            }
        }

        /// <summary>Disables it: held buttons are released at the device's next update.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            Enabled = false;
            _disposed = true;
        }

        private void Held(Intention intention, TouchGestureButton button, Action<IntentionEventData> performed)
        {
            if (intention == null) return;
            intention.Started += e => _device.Press(button, e.PositionPx);
            intention.Performed += e =>
            {
                _device.SetPosition(e.PositionPx);
                performed?.Invoke(e);
            };
            intention.Ended += _ => _device.Release(button);
        }

        private static bool TryEdgeButton(ScreenEdge edge, out TouchGestureButton button)
        {
            switch (edge)
            {
                case ScreenEdge.Left: button = TouchGestureButton.EdgeSwipeLeft; return true;
                case ScreenEdge.Right: button = TouchGestureButton.EdgeSwipeRight; return true;
                case ScreenEdge.Bottom: button = TouchGestureButton.EdgeSwipeBottom; return true;
                case ScreenEdge.Top: button = TouchGestureButton.EdgeSwipeTop; return true;
                default: button = default; return false;
            }
        }
    }
}
