using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Threads.TouchController.Intentions
{
    /// <summary>
    /// Feeds Input System actions into intentions, so the same intention (e.g. a frame drag) works with a
    /// mouse, a pen or a gamepad as well as touch. Wraps an <see cref="IntentionSource"/>: bind
    /// <see cref="Source"/> to the intentions it should drive.
    /// </summary>
    /// <example>
    /// <code>
    /// var mouse = InputActionSource.Pointer(intentions, pressAction, positionAction);   // e.g. &lt;Mouse&gt;/leftButton, &lt;Mouse&gt;/position
    /// var stick = InputActionSource.Stick(intentions, moveAction, 600f, () =&gt; frameCentreDp);
    /// frameDrag.Bind(mouse.Source).Bind(stick.Source);
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><b>Pointer</b>: the press action going down begins at the position action's value (pixels), moves
    /// follow the position while pressed, and release ends. Like a finger.</para>
    /// <para><b>Stick</b>: moving the stick out of its dead zone begins at <c>startPositionDp</c> (it decides
    /// which area the stick acts on); every update while held moves by value × speed × time; letting go ends.</para>
    /// <para>Actions are enabled if they aren't already, and only those are disabled again by
    /// <see cref="Dispose"/>, so actions shared with other code are left as they were.</para>
    /// </remarks>
    public sealed class InputActionSource : IDisposable
    {
        private readonly IntentionSystem _system;
        private readonly InputAction _press;
        private readonly InputAction _position;
        private readonly InputAction _stick;
        private readonly float _speedDps;
        private readonly Func<Vector2> _stickStartDp;
        private readonly bool _enabledPress, _enabledPosition, _enabledStick;
        private double _lastTick = double.NaN;
        private bool _disposed;

        private InputActionSource(IntentionSystem system, string name, InputAction press, InputAction position, InputAction stick,
            float speedDps, Func<Vector2> stickStartDp)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
            Source = system.CreateSource(name, IntentionSourceKind.InputAction);
            _press = press;
            _position = position;
            _stick = stick;
            _speedDps = speedDps;
            _stickStartDp = stickStartDp;

            if (_press != null)
            {
                _press.started += OnPressStarted;
                _press.canceled += OnPressCanceled;
                _enabledPress = EnableIfNeeded(_press);
            }
            if (_position != null)
            {
                _position.performed += OnPositionPerformed;
                _enabledPosition = EnableIfNeeded(_position);
            }
            if (_stick != null)
            {
                _stick.canceled += OnStickCanceled;
                _system.BeforeUpdate += OnBeforeUpdate;
                _enabledStick = EnableIfNeeded(_stick);
            }
        }

        /// <summary>A pointer: <paramref name="press"/> (a button) and <paramref name="position"/> (a <c>Vector2</c> in pixels, e.g. <c>&lt;Pointer&gt;/position</c>).</summary>
        public static InputActionSource Pointer(IntentionSystem system, InputAction press, InputAction position, string name = "Pointer") =>
            new InputActionSource(system, name, press ?? throw new ArgumentNullException(nameof(press)),
                position ?? throw new ArgumentNullException(nameof(position)), null, 0f, null);

        public static InputActionSource Pointer(IntentionSystem system, InputActionReference press, InputActionReference position, string name = "Pointer") =>
            Pointer(system, Action(press, nameof(press)), Action(position, nameof(position)), name);

        /// <summary>
        /// A stick: <paramref name="stick"/> (a <c>Vector2</c> value, e.g. <c>&lt;Gamepad&gt;/leftStick</c>) moves at up to
        /// <paramref name="speedDpPerSecond"/>. <paramref name="startPositionDp"/> says where it acts (e.g. the frame's centre).
        /// </summary>
        public static InputActionSource Stick(IntentionSystem system, InputAction stick, float speedDpPerSecond, Func<Vector2> startPositionDp,
            string name = "Stick")
        {
            if (!(speedDpPerSecond > 0f)) throw new ArgumentOutOfRangeException(nameof(speedDpPerSecond), speedDpPerSecond, "Must be greater than 0.");
            return new InputActionSource(system, name, null, null, stick ?? throw new ArgumentNullException(nameof(stick)),
                speedDpPerSecond, startPositionDp ?? throw new ArgumentNullException(nameof(startPositionDp)));
        }

        public static InputActionSource Stick(IntentionSystem system, InputActionReference stick, float speedDpPerSecond, Func<Vector2> startPositionDp,
            string name = "Stick") =>
            Stick(system, Action(stick, nameof(stick)), speedDpPerSecond, startPositionDp, name);

        /// <summary>The source to bind intentions to.</summary>
        public IntentionSource Source { get; }

        /// <summary>Unsubscribes, ends any press (Cancelled), and disables the actions this source enabled.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_press != null)
            {
                _press.started -= OnPressStarted;
                _press.canceled -= OnPressCanceled;
                if (_enabledPress) _press.Disable();
            }
            if (_position != null)
            {
                _position.performed -= OnPositionPerformed;
                if (_enabledPosition) _position.Disable();
            }
            if (_stick != null)
            {
                _stick.canceled -= OnStickCanceled;
                _system.BeforeUpdate -= OnBeforeUpdate;
                if (_enabledStick) _stick.Disable();
            }
            Source.Cancel();
        }

        private void OnPressStarted(InputAction.CallbackContext context) =>
            Source.Begin(_position.ReadValue<Vector2>() / _system.PixelsPerDp, context.time);

        private void OnPressCanceled(InputAction.CallbackContext context) => Source.End(context.time);

        private void OnPositionPerformed(InputAction.CallbackContext context)
        {
            if (Source.IsDown)
                Source.MoveTo(context.ReadValue<Vector2>() / _system.PixelsPerDp, context.time);
        }

        private void OnStickCanceled(InputAction.CallbackContext context) => Source.End();

        /// <summary>Every IntentionSystem update, before it resolves: begin, move or keep holding the stick.</summary>
        private void OnBeforeUpdate(double time)
        {
            var dt = double.IsNaN(_lastTick) ? 0.0 : time - _lastTick;
            _lastTick = time;
            var value = _stick.ReadValue<Vector2>();
            if (value == Vector2.zero) return; // inside the dead zone (release is handled by canceled)

            if (!Source.IsDown)
                Source.Begin(_stickStartDp(), time);
            else if (dt > 0.0)
                Source.MoveBy(value * (_speedDps * (float)dt), time);
        }

        private static bool EnableIfNeeded(InputAction action)
        {
            if (action.enabled) return false;
            action.Enable();
            return true;
        }

        private static InputAction Action(InputActionReference reference, string parameter)
        {
            if (reference == null || reference.action == null) throw new ArgumentNullException(parameter);
            return reference.action;
        }
    }
}
