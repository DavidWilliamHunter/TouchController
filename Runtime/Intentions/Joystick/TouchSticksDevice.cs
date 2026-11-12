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
    /// <summary>State of a <see cref="TouchSticks"/> device: two sticks.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TouchSticksState : IInputStateTypeInfo
    {
        public static FourCC Format => new FourCC('T', 'S', 'T', 'K');

        [InputControl(name = "leftStick", layout = "Vector2", usage = "Primary2DMotion", displayName = "Left Stick")]
        [FieldOffset(0)] public Vector2 leftStick;

        [InputControl(name = "rightStick", layout = "Vector2", usage = "Secondary2DMotion", displayName = "Right Stick")]
        [FieldOffset(8)] public Vector2 rightStick;

        public FourCC format => Format;
    }

    /// <summary>
    /// The Input System device that on-screen touch joysticks write to (layout name <c>TouchSticks</c>). Bind
    /// actions to <c>&lt;TouchSticks&gt;/leftStick</c> and <c>/rightStick</c> (or <c>*/{Primary2DMotion}</c> and
    /// <c>*/{Secondary2DMotion}</c>, which also match gamepads), beside your gamepad bindings.
    /// </summary>
    /// <remarks>Created and removed through <see cref="TouchSticksDevice"/>.</remarks>
    [InputControlLayout(stateType = typeof(TouchSticksState), displayName = "Touch Sticks")]
    public sealed class TouchSticks : InputDevice
    {
        public const string LayoutName = "TouchSticks";

        public Vector2Control leftStick { get; private set; }

        public Vector2Control rightStick { get; private set; }

        protected override void FinishSetup()
        {
            leftStick = GetChildControl<Vector2Control>("leftStick");
            rightStick = GetChildControl<Vector2Control>("rightStick");
            base.FinishSetup();
        }

        /// <summary>Registers the layout with the Input System (idempotent). <see cref="TouchSticksDevice"/> calls it.</summary>
        public static void RegisterLayout()
        {
            if (InputSystem.LoadLayout(LayoutName) == null)
                InputSystem.RegisterLayout<TouchSticks>(LayoutName);
        }

#if UNITY_EDITOR
        // So the binding picker of the actions editor lists the device. Layout registration is the Input
        // System's own registry; the package keeps no state of its own here.
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor() => RegisterLayout();
#endif
    }

    /// <summary>
    /// Owns one <see cref="TouchSticks"/> device: adds it to the Input System when created and removes it when
    /// disposed. Create one at start-up (e.g. <see cref="IntentionRunner"/>'s "Create Touch Sticks") and pass it
    /// to the <see cref="JoystickInputOutput"/>s that write to it. Not a singleton: nothing finds it globally.
    /// </summary>
    public sealed class TouchSticksDevice : IDisposable
    {
        public TouchSticksDevice()
        {
            TouchSticks.RegisterLayout();
            Device = InputSystem.AddDevice<TouchSticks>();
        }

        /// <summary>The device (null once disposed).</summary>
        public TouchSticks Device { get; private set; }

        public Vector2Control LeftStick => Device?.leftStick;

        public Vector2Control RightStick => Device?.rightStick;

        /// <summary>Queues a new value for a stick of this device (applied at the Input System's next update).</summary>
        public void Write(Vector2Control stick, Vector2 value)
        {
            if (stick == null) throw new ArgumentNullException(nameof(stick));
            if (Device == null || !Device.added) return;
            if (stick.device != Device) throw new ArgumentException("The stick belongs to another device.", nameof(stick));
            InputSystem.QueueDeltaStateEvent(stick, value);
        }

        /// <summary>Removes the device from the Input System.</summary>
        public void Dispose()
        {
            if (Device == null) return;
            if (Device.added) InputSystem.RemoveDevice(Device);
            Device = null;
        }
    }

    /// <summary>
    /// Feeds a <see cref="TouchJoystick"/> to a stick of a <see cref="TouchSticksDevice"/>, so code that reads
    /// Input System actions works unchanged with touch.
    /// </summary>
    /// <code>
    /// var sticks = new TouchSticksDevice();
    /// var output = new JoystickInputOutput(moveJoystick, sticks, sticks.LeftStick);
    /// // "Move" action bound to &lt;Gamepad&gt;/leftStick and &lt;TouchSticks&gt;/leftStick
    /// </code>
    public sealed class JoystickInputOutput : IDisposable
    {
        private readonly TouchJoystick _joystick;
        private readonly TouchSticksDevice _device;
        private readonly Vector2Control _stick;
        private bool _disposed;

        public JoystickInputOutput(TouchJoystick joystick, TouchSticksDevice device, Vector2Control stick)
        {
            _joystick = joystick ?? throw new ArgumentNullException(nameof(joystick));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _stick = stick ?? throw new ArgumentNullException(nameof(stick));
            if (device.Device == null || stick.device != device.Device)
                throw new ArgumentException("The stick must belong to the device.", nameof(stick));
            _joystick.Changed += OnChanged;
            if (_joystick.Value != Vector2.zero) OnChanged(_joystick.Value);
        }

        /// <summary>Stops writing, and sets the stick back to zero.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _joystick.Changed -= OnChanged;
            _device.Write(_stick, Vector2.zero);
        }

        private void OnChanged(Vector2 value) => _device.Write(_stick, value);
    }
}
