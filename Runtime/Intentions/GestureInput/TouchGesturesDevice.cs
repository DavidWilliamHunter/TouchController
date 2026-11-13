using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace Threads.TouchController.Intentions
{
    /// <summary>The buttons of a <see cref="TouchGestures"/> device (the bit of each in <see cref="TouchGesturesState.buttons"/>).</summary>
    public enum TouchGestureButton
    {
        /// <summary>Pulse: a tap.</summary>
        Tap = 0,

        /// <summary>Pulse: a double tap.</summary>
        DoubleTap = 1,

        /// <summary>Pulse: a two-finger tap.</summary>
        TwoFingerTap = 2,

        /// <summary>Held while a long press lasts.</summary>
        LongPress = 3,

        /// <summary>Pulse: a swipe (flick) to the left.</summary>
        SwipeLeft = 4,

        SwipeRight = 5,
        SwipeUp = 6,
        SwipeDown = 7,

        /// <summary>Held while a swipe in from the left edge lasts.</summary>
        EdgeSwipeLeft = 8,

        EdgeSwipeRight = 9,
        EdgeSwipeBottom = 10,
        EdgeSwipeTop = 11,

        /// <summary>Held while a one-finger drag lasts.</summary>
        Dragging = 12,

        /// <summary>Held while a pinch lasts.</summary>
        Pinching = 13,

        /// <summary>Held while a two-finger rotation lasts.</summary>
        Rotating = 14,

        /// <summary>Held while a two-finger pan lasts.</summary>
        TwoFingerPanning = 15,
    }

    /// <summary>State of a <see cref="TouchGestures"/> device.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 44)]
    public struct TouchGesturesState : IInputStateTypeInfo, IEquatable<TouchGesturesState>
    {
        public static FourCC Format => new FourCC('T', 'G', 'S', 'T');

        [InputControl(name = "tap", layout = "Button", bit = (int)TouchGestureButton.Tap, displayName = "Tap")]
        [InputControl(name = "doubleTap", layout = "Button", bit = (int)TouchGestureButton.DoubleTap, displayName = "Double Tap")]
        [InputControl(name = "twoFingerTap", layout = "Button", bit = (int)TouchGestureButton.TwoFingerTap, displayName = "Two-Finger Tap")]
        [InputControl(name = "longPress", layout = "Button", bit = (int)TouchGestureButton.LongPress, displayName = "Long Press")]
        [InputControl(name = "swipeLeft", layout = "Button", bit = (int)TouchGestureButton.SwipeLeft, displayName = "Swipe Left")]
        [InputControl(name = "swipeRight", layout = "Button", bit = (int)TouchGestureButton.SwipeRight, displayName = "Swipe Right")]
        [InputControl(name = "swipeUp", layout = "Button", bit = (int)TouchGestureButton.SwipeUp, displayName = "Swipe Up")]
        [InputControl(name = "swipeDown", layout = "Button", bit = (int)TouchGestureButton.SwipeDown, displayName = "Swipe Down")]
        [InputControl(name = "edgeSwipeLeft", layout = "Button", bit = (int)TouchGestureButton.EdgeSwipeLeft, displayName = "Edge Swipe From Left")]
        [InputControl(name = "edgeSwipeRight", layout = "Button", bit = (int)TouchGestureButton.EdgeSwipeRight, displayName = "Edge Swipe From Right")]
        [InputControl(name = "edgeSwipeBottom", layout = "Button", bit = (int)TouchGestureButton.EdgeSwipeBottom, displayName = "Edge Swipe From Bottom")]
        [InputControl(name = "edgeSwipeTop", layout = "Button", bit = (int)TouchGestureButton.EdgeSwipeTop, displayName = "Edge Swipe From Top")]
        [InputControl(name = "dragging", layout = "Button", bit = (int)TouchGestureButton.Dragging, displayName = "Dragging")]
        [InputControl(name = "pinching", layout = "Button", bit = (int)TouchGestureButton.Pinching, displayName = "Pinching")]
        [InputControl(name = "rotating", layout = "Button", bit = (int)TouchGestureButton.Rotating, displayName = "Rotating")]
        [InputControl(name = "twoFingerPanning", layout = "Button", bit = (int)TouchGestureButton.TwoFingerPanning, displayName = "Two-Finger Panning")]
        [FieldOffset(0)] public uint buttons;

        /// <summary>Where the latest gesture happened (pixels, origin bottom-left, as <c>&lt;Touchscreen&gt;/position</c>).</summary>
        [InputControl(name = "position", layout = "Vector2", displayName = "Position")]
        [FieldOffset(4)] public Vector2 position;

        /// <summary>For one update after a swipe: its direction (one axis, ±1).</summary>
        [InputControl(name = "swipe", layout = "Vector2", displayName = "Swipe Direction")]
        [FieldOffset(12)] public Vector2 swipe;

        /// <summary>One-finger drag movement since the previous update (pixels), as <c>&lt;Pointer&gt;/delta</c>.</summary>
        [InputControl(name = "drag", layout = "Vector2", displayName = "Drag Delta")]
        [FieldOffset(20)] public Vector2 drag;

        /// <summary>Two-finger pan movement since the previous update (pixels).</summary>
        [InputControl(name = "twoFingerPan", layout = "Vector2", displayName = "Two-Finger Pan Delta")]
        [FieldOffset(28)] public Vector2 twoFingerPan;

        /// <summary>Pinch since the previous update: the scale factor minus one (+0.1 = 10 % bigger; 0 = no change).</summary>
        [InputControl(name = "pinch", layout = "Axis", displayName = "Pinch Delta")]
        [FieldOffset(36)] public float pinch;

        /// <summary>Two-finger rotation since the previous update (degrees, as the gesture's <c>AngleDeltaDeg</c>).</summary>
        [InputControl(name = "rotate", layout = "Axis", displayName = "Rotate Delta")]
        [FieldOffset(40)] public float rotate;

        public FourCC format => Format;

        public bool IsPressed(TouchGestureButton button) => (buttons & (1u << (int)button)) != 0;

        public bool Equals(TouchGesturesState other) =>
            buttons == other.buttons && position == other.position && swipe == other.swipe && drag == other.drag &&
            twoFingerPan == other.twoFingerPan && pinch == other.pinch && rotate == other.rotate;

        public override bool Equals(object obj) => obj is TouchGesturesState other && Equals(other);

        public override int GetHashCode() => (int)buttons ^ position.GetHashCode() ^ drag.GetHashCode();
    }

    /// <summary>
    /// The Input System device that touch gestures write to (layout name <c>TouchGestures</c>), so actions can be
    /// bound to gestures: <c>&lt;TouchGestures&gt;/swipeLeft</c>, <c>/tap</c>, <c>/pinch</c>, <c>/drag</c>…
    /// </summary>
    /// <remarks>
    /// <para>Taps and swipes are <b>pulses</b>: the button is down for exactly one Input System update, so
    /// <c>action.WasPerformedThisFrame()</c>, <c>performed</c> callbacks and <c>IsPressed()</c> all see them.
    /// Long press, edge swipes and the continuous gestures are <b>held</b> while they last.</para>
    /// <para>Deltas (<c>drag</c>, <c>twoFingerPan</c>, <c>pinch</c>, <c>rotate</c>) hold the movement since the previous
    /// update and fall back to zero, like a mouse delta. Positions and drag deltas are in pixels.</para>
    /// <para>Created and removed through <see cref="TouchGesturesDevice"/>, fed by <see cref="GestureInputOutput"/>.</para>
    /// </remarks>
    [InputControlLayout(stateType = typeof(TouchGesturesState), displayName = "Touch Gestures")]
    public sealed class TouchGestures : InputDevice
    {
        public const string LayoutName = "TouchGestures";

        public ButtonControl tap { get; private set; }
        public ButtonControl doubleTap { get; private set; }
        public ButtonControl twoFingerTap { get; private set; }
        public ButtonControl longPress { get; private set; }
        public ButtonControl swipeLeft { get; private set; }
        public ButtonControl swipeRight { get; private set; }
        public ButtonControl swipeUp { get; private set; }
        public ButtonControl swipeDown { get; private set; }
        public ButtonControl edgeSwipeLeft { get; private set; }
        public ButtonControl edgeSwipeRight { get; private set; }
        public ButtonControl edgeSwipeBottom { get; private set; }
        public ButtonControl edgeSwipeTop { get; private set; }
        public ButtonControl dragging { get; private set; }
        public ButtonControl pinching { get; private set; }
        public ButtonControl rotating { get; private set; }
        public ButtonControl twoFingerPanning { get; private set; }
        public Vector2Control position { get; private set; }
        public Vector2Control swipe { get; private set; }
        public Vector2Control drag { get; private set; }
        public Vector2Control twoFingerPan { get; private set; }
        public AxisControl pinch { get; private set; }
        public AxisControl rotate { get; private set; }

        /// <summary>The control of a button.</summary>
        public ButtonControl this[TouchGestureButton button]
        {
            get
            {
                switch (button)
                {
                    case TouchGestureButton.Tap: return tap;
                    case TouchGestureButton.DoubleTap: return doubleTap;
                    case TouchGestureButton.TwoFingerTap: return twoFingerTap;
                    case TouchGestureButton.LongPress: return longPress;
                    case TouchGestureButton.SwipeLeft: return swipeLeft;
                    case TouchGestureButton.SwipeRight: return swipeRight;
                    case TouchGestureButton.SwipeUp: return swipeUp;
                    case TouchGestureButton.SwipeDown: return swipeDown;
                    case TouchGestureButton.EdgeSwipeLeft: return edgeSwipeLeft;
                    case TouchGestureButton.EdgeSwipeRight: return edgeSwipeRight;
                    case TouchGestureButton.EdgeSwipeBottom: return edgeSwipeBottom;
                    case TouchGestureButton.EdgeSwipeTop: return edgeSwipeTop;
                    case TouchGestureButton.Dragging: return dragging;
                    case TouchGestureButton.Pinching: return pinching;
                    case TouchGestureButton.Rotating: return rotating;
                    case TouchGestureButton.TwoFingerPanning: return twoFingerPanning;
                    default: throw new ArgumentOutOfRangeException(nameof(button), button, null);
                }
            }
        }

        protected override void FinishSetup()
        {
            tap = GetChildControl<ButtonControl>("tap");
            doubleTap = GetChildControl<ButtonControl>("doubleTap");
            twoFingerTap = GetChildControl<ButtonControl>("twoFingerTap");
            longPress = GetChildControl<ButtonControl>("longPress");
            swipeLeft = GetChildControl<ButtonControl>("swipeLeft");
            swipeRight = GetChildControl<ButtonControl>("swipeRight");
            swipeUp = GetChildControl<ButtonControl>("swipeUp");
            swipeDown = GetChildControl<ButtonControl>("swipeDown");
            edgeSwipeLeft = GetChildControl<ButtonControl>("edgeSwipeLeft");
            edgeSwipeRight = GetChildControl<ButtonControl>("edgeSwipeRight");
            edgeSwipeBottom = GetChildControl<ButtonControl>("edgeSwipeBottom");
            edgeSwipeTop = GetChildControl<ButtonControl>("edgeSwipeTop");
            dragging = GetChildControl<ButtonControl>("dragging");
            pinching = GetChildControl<ButtonControl>("pinching");
            rotating = GetChildControl<ButtonControl>("rotating");
            twoFingerPanning = GetChildControl<ButtonControl>("twoFingerPanning");
            position = GetChildControl<Vector2Control>("position");
            swipe = GetChildControl<Vector2Control>("swipe");
            drag = GetChildControl<Vector2Control>("drag");
            twoFingerPan = GetChildControl<Vector2Control>("twoFingerPan");
            pinch = GetChildControl<AxisControl>("pinch");
            rotate = GetChildControl<AxisControl>("rotate");
            base.FinishSetup();
        }

        /// <summary>Registers the layout with the Input System (idempotent). <see cref="TouchGesturesDevice"/> calls it.</summary>
        public static void RegisterLayout()
        {
            if (InputSystem.LoadLayout(LayoutName) == null)
                InputSystem.RegisterLayout<TouchGestures>(LayoutName);
        }

#if UNITY_EDITOR
        // So the binding picker of the actions editor lists the device.
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor() => RegisterLayout();
#endif
    }

    /// <summary>
    /// Owns one <see cref="TouchGestures"/> device: adds it to the Input System when created and removes it when
    /// disposed. Collects what happens during a frame (<see cref="Pulse"/>, <see cref="Press"/>, the <c>Add…</c>
    /// deltas) and sends it to the device at the start of the next Input System update, so every pulse is seen
    /// for exactly one update. Usually fed by a <see cref="GestureInputOutput"/>; your own intentions can write to
    /// it too. Not a singleton: nothing finds it globally.
    /// </summary>
    public sealed class TouchGesturesDevice : IDisposable
    {
        private const uint AllButtons = 0xFFFF;

        private TouchGesturesState _pending;
        private TouchGesturesState _sent;
        private uint _latched;               // pressed since the last send (pulses, and presses that may already have ended)
        private readonly int[] _held = new int[16];
        private float _pinchFactor = 1f;

        /// <param name="usage">
        /// Optional usage tag, to tell several devices apart in bindings: with <c>"LeftHalf"</c>,
        /// <c>&lt;TouchGestures&gt;{LeftHalf}/tap</c> binds only this device.
        /// </param>
        public TouchGesturesDevice(string usage = null)
        {
            TouchGestures.RegisterLayout();
            Device = InputSystem.AddDevice<TouchGestures>();
            if (!string.IsNullOrEmpty(usage))
                InputSystem.SetDeviceUsage(Device, usage);
            InputSystem.onBeforeUpdate += OnBeforeUpdate;
        }

        /// <summary>The device (null once disposed).</summary>
        public TouchGestures Device { get; private set; }

        /// <summary>Presses a button for one Input System update (a tap, a swipe), at a position in pixels.</summary>
        public void Pulse(TouchGestureButton button, Vector2 positionPx)
        {
            _latched |= Bit(button);
            _pending.position = positionPx;
        }

        /// <summary>Holds a button down until a matching <see cref="Release"/> (presses are counted). It is seen down for at least one update.</summary>
        public void Press(TouchGestureButton button, Vector2 positionPx)
        {
            _held[(int)button]++;
            _latched |= Bit(button);
            _pending.position = positionPx;
        }

        /// <summary>Ends one <see cref="Press"/> of a button.</summary>
        public void Release(TouchGestureButton button)
        {
            if (_held[(int)button] > 0) _held[(int)button]--;
        }

        /// <summary>The position control's value (pixels).</summary>
        public void SetPosition(Vector2 positionPx) => _pending.position = positionPx;

        /// <summary>The swipe direction, for one update.</summary>
        public void SetSwipe(Vector2 direction) => _pending.swipe = direction;

        /// <summary>Adds to this update's one-finger drag delta (pixels).</summary>
        public void AddDrag(Vector2 deltaPx) => _pending.drag += deltaPx;

        /// <summary>Adds to this update's two-finger pan delta (pixels).</summary>
        public void AddTwoFingerPan(Vector2 deltaPx) => _pending.twoFingerPan += deltaPx;

        /// <summary>Multiplies this update's pinch by a scale factor (a gesture's <c>ScaleDelta</c>).</summary>
        public void AddPinch(float scaleDelta)
        {
            if (scaleDelta > 0f) _pinchFactor *= scaleDelta;
        }

        /// <summary>Adds to this update's rotation (degrees).</summary>
        public void AddRotate(float deltaDeg) => _pending.rotate += deltaDeg;

        /// <summary>Removes the device from the Input System.</summary>
        public void Dispose()
        {
            if (Device == null) return;
            InputSystem.onBeforeUpdate -= OnBeforeUpdate;
            if (Device.added) InputSystem.RemoveDevice(Device);
            Device = null;
        }

        private static uint Bit(TouchGestureButton button) => 1u << (int)button;

        private void OnBeforeUpdate()
        {
            // Play Mode: the Editor's own updates (UI of the Editor) must not consume the game's pulses.
            var type = InputState.currentUpdateType;
            if (type == InputUpdateType.BeforeRender) return;
            if (type == InputUpdateType.Editor && Application.isPlaying) return;
            Flush();
        }

        /// <summary>Sends the collected state to the device (normally at the start of each Input System update).</summary>
        internal void Flush()
        {
            if (Device == null || !Device.added) return;

            uint held = 0;
            for (var i = 0; i < _held.Length; i++)
                if (_held[i] > 0) held |= 1u << i;

            _pending.pinch = _pinchFactor - 1f;
            _pending.buttons = (_latched | held) & AllButtons;

            // A new pulse on a button that is still down from the previous update: release it first, so the
            // action sees a second press.
            var again = _latched & _sent.buttons & ~held;
            if (again != 0)
            {
                var released = _sent;
                released.buttons &= ~again;
                InputSystem.QueueStateEvent(Device, released);
                _sent = released;
            }

            if (!_pending.Equals(_sent))
            {
                InputSystem.QueueStateEvent(Device, _pending);
                _sent = _pending;
            }

            // What lasts one update only.
            _latched = 0;
            _pending.swipe = Vector2.zero;
            _pending.drag = Vector2.zero;
            _pending.twoFingerPan = Vector2.zero;
            _pending.rotate = 0f;
            _pinchFactor = 1f;
        }
    }
}
