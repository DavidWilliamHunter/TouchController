using System;
using UnityEngine;

namespace Threads.TouchController.Intentions
{
    /// <summary>Where a <see cref="TouchJoystick"/> is.</summary>
    public enum JoystickMode
    {
        /// <summary>In a fixed place: touching the base (plus a margin) grabs it.</summary>
        Fixed = 0,

        /// <summary>Appears where a finger touches down inside its activation area; motion is relative to that point.</summary>
        Floating = 1,
    }

    /// <summary>Settings of a <see cref="TouchJoystick"/>. Read when the joystick is created.</summary>
    public sealed class TouchJoystickOptions
    {
        /// <summary>For logs.</summary>
        public string Name = "Joystick";

        public JoystickMode Mode = JoystickMode.Fixed;

        /// <summary>Fixed mode: the centre of the base, in dp (asked at each touch-down, so it can follow the layout).</summary>
        public Func<Vector2> CentreDp;

        /// <summary>Full deflection is reached this far from the origin (dp).</summary>
        public float RadiusDp = 60f;

        /// <summary>Fixed mode: how far outside the base a touch still grabs it (dp).</summary>
        public float TouchMarginDp = 24f;

        /// <summary>Deflections below this fraction of the radius read as zero; the rest is rescaled to 0..1.</summary>
        public float DeadZone = 0.1f;

        /// <summary>Response curve: magnitude = rescaled ^ exponent (1 = linear, 2 = finer control near the centre).</summary>
        public float ResponseExponent = 1f;

        /// <summary>Floating mode: where a touch-down makes the joystick appear. Null: anywhere.</summary>
        public IIntentionArea ActivationArea;

        /// <summary>
        /// Floating mode: the origin is clamped so the whole base (radius) stays inside this rectangle (dp).
        /// Null: the screen.
        /// </summary>
        public Func<Rect> OriginBoundsDp;

        /// <summary>Floating mode: when the finger goes past the radius, the origin follows it (the base is dragged along).</summary>
        public bool Follow;

        /// <summary>Its intention's priority. It is exclusive, so a held joystick's finger drives nothing else of lower priority.</summary>
        public int Priority = 1000;
    }

    /// <summary>
    /// An on-screen joystick driven by touch, through the intention system: a circular base whose centre is the
    /// origin, and the finger's offset from it as the output (<see cref="Value"/>, −1..1 on each axis, length ≤ 1).
    /// </summary>
    /// <remarks>
    /// <para>It responds at touch-down (bound to <see cref="PressGesture"/>), and claims its finger exclusively at
    /// <see cref="TouchJoystickOptions.Priority"/>. Each joystick follows one finger at a time; other fingers in
    /// its area are left to other intentions. Two joysticks (two thumbs) work independently.</para>
    /// <para>Read it directly (<see cref="Value"/>, <see cref="Changed"/>), or feed it to the Input System with
    /// <see cref="JoystickInputOutput"/> and a <see cref="TouchSticksDevice"/>, so code reading actions works
    /// unchanged.</para>
    /// <code>
    /// var move = new TouchJoystick(intentions, new TouchJoystickOptions
    /// {
    ///     Mode = JoystickMode.Floating,
    ///     ActivationArea = ScreenRectArea.Fraction(new Rect(0, 0, 0.5f, 1)), // left half
    /// });
    /// move.Enabled = true;
    /// // each frame: player.Move(move.Value);
    /// </code>
    /// </remarks>
    public sealed class TouchJoystick : IDisposable
    {
        private readonly IntentionSystem _system;
        private readonly JoystickMode _mode;
        private readonly Func<Vector2> _centreDp;
        private readonly float _radiusDp;
        private readonly float _deadZone;
        private readonly float _exponent;
        private readonly Func<Rect> _originBoundsDp;
        private readonly bool _follow;
        private int _instanceId;

        public TouchJoystick(IntentionSystem system, TouchJoystickOptions options)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (!(options.RadiusDp > 0f)) throw new ArgumentOutOfRangeException(nameof(options), options.RadiusDp, "RadiusDp must be greater than 0.");
            if (!(options.DeadZone >= 0f && options.DeadZone < 1f)) throw new ArgumentOutOfRangeException(nameof(options), options.DeadZone, "DeadZone must be in [0, 1).");
            if (!(options.ResponseExponent > 0f)) throw new ArgumentOutOfRangeException(nameof(options), options.ResponseExponent, "ResponseExponent must be greater than 0.");
            if (options.Mode == JoystickMode.Fixed && options.CentreDp == null)
                throw new ArgumentException("A fixed joystick needs CentreDp.", nameof(options));

            _mode = options.Mode;
            _centreDp = options.CentreDp;
            _radiusDp = options.RadiusDp;
            _deadZone = options.DeadZone;
            _exponent = options.ResponseExponent;
            _originBoundsDp = options.OriginBoundsDp;
            _follow = options.Follow && options.Mode == JoystickMode.Floating;

            var touchRadius = options.RadiusDp + Mathf.Max(0f, options.TouchMarginDp);
            var area = _mode == JoystickMode.Fixed
                ? new FuncArea(p => (p.PositionDp - _centreDp()).sqrMagnitude <= touchRadius * touchRadius)
                : options.ActivationArea;

            Intention = new Intention(options.Name, system)
                .Bind<PressGesture>()
                .WithPriority(options.Priority)
                .Exclusive()
                .StartWhen(_ => !IsHeld); // one finger at a time: others stay free for other intentions
            if (area != null) Intention.InArea(area);
            Intention.Started += OnStarted;
            Intention.Performed += OnPerformed;
            Intention.Ended += OnEnded;

            OriginDp = ThumbDp = _mode == JoystickMode.Fixed ? _centreDp() : Vector2.zero;
        }

        /// <summary>The underlying intention (e.g. to change its priority while disabled).</summary>
        public Intention Intention { get; }

        /// <summary>Registers it with the intention system (true) or releases it (false). Starts disabled.</summary>
        public bool Enabled
        {
            get => Intention.Enabled;
            set => Intention.Enabled = value;
        }

        /// <summary>The output: direction × magnitude, after the dead zone and the response curve. Zero when released.</summary>
        public Vector2 Value { get; private set; }

        /// <summary>A finger holds it.</summary>
        public bool IsHeld { get; private set; }

        /// <summary>The holding finger (<see cref="TouchPoint.touchId"/>), or −1.</summary>
        public int FingerId { get; private set; } = -1;

        /// <summary>The centre of the base, in dp (screen space, origin bottom-left). Floating: where the finger touched down (clamped).</summary>
        public Vector2 OriginDp { get; private set; }

        /// <summary>Where to draw the knob, in dp: the finger, kept within the radius.</summary>
        public Vector2 ThumbDp { get; private set; }

        public float RadiusDp => _radiusDp;

        public JoystickMode Mode => _mode;

        /// <summary>A finger took the joystick (<see cref="OriginDp"/> is set).</summary>
        public event Action<TouchJoystick> Pressed;

        /// <summary><see cref="Value"/> changed (including back to zero on release).</summary>
        public event Action<Vector2> Changed;

        /// <summary>The finger lifted (or was cancelled, or a higher-priority intention took it). Value is zero.</summary>
        public event Action<TouchJoystick> Released;

        /// <summary>Disables it (releasing it) and unsubscribes.</summary>
        public void Dispose()
        {
            Intention.Enabled = false;
            Intention.Started -= OnStarted;
            Intention.Performed -= OnPerformed;
            Intention.Ended -= OnEnded;
        }

        /// <summary>The output for an offset from the origin (dp): the dead zone, rescaling and the response curve.</summary>
        public Vector2 Evaluate(Vector2 offsetDp)
        {
            var distance = offsetDp.magnitude;
            if (distance <= 0f) return Vector2.zero;
            var m = Mathf.Min(distance / _radiusDp, 1f);
            if (m <= _deadZone) return Vector2.zero;
            m = (m - _deadZone) / (1f - _deadZone);
            if (_exponent != 1f) m = Mathf.Pow(m, _exponent);
            return offsetDp / distance * m;
        }

        private void OnStarted(IntentionEventData e)
        {
            _instanceId = e.InstanceId;
            IsHeld = true;
            FingerId = e.FingerId;
            OriginDp = _mode == JoystickMode.Fixed ? _centreDp() : ClampOrigin(e.StartPositionDp);
            ThumbDp = OriginDp;
            Pressed?.Invoke(this);
            Track(e.PositionDp);
        }

        private void OnPerformed(IntentionEventData e)
        {
            if (e.InstanceId == _instanceId) Track(e.PositionDp);
        }

        private void OnEnded(IntentionEventData e)
        {
            if (e.InstanceId != _instanceId || !IsHeld) return;
            IsHeld = false;
            FingerId = -1;
            ThumbDp = OriginDp;
            SetValue(Vector2.zero);
            Released?.Invoke(this);
        }

        private void Track(Vector2 fingerDp)
        {
            var offset = fingerDp - OriginDp;
            var distance = offset.magnitude;
            if (distance > _radiusDp)
            {
                var excess = offset / distance * (distance - _radiusDp);
                if (_follow) OriginDp += excess;
                offset -= excess;
            }
            ThumbDp = OriginDp + offset;
            SetValue(Evaluate(offset));
        }

        private void SetValue(Vector2 value)
        {
            if (value == Value) return;
            Value = value;
            Changed?.Invoke(value);
        }

        private Vector2 ClampOrigin(Vector2 p)
        {
            Rect bounds;
            if (_originBoundsDp != null)
                bounds = _originBoundsDp();
            else
            {
                var screen = _system.Touch.Frame?.ScreenSizeDp ?? Vector2.zero;
                if (screen == Vector2.zero) return p; // unknown
                bounds = new Rect(Vector2.zero, screen);
            }
            return new Vector2(ClampAxis(p.x, bounds.xMin, bounds.xMax), ClampAxis(p.y, bounds.yMin, bounds.yMax));
        }

        private float ClampAxis(float v, float min, float max)
        {
            min += _radiusDp;
            max -= _radiusDp;
            return min > max ? (min + max) * 0.5f : Mathf.Clamp(v, min, max);
        }
    }
}
