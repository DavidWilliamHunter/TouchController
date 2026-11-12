using System;
using UnityEngine;

namespace Threads.TouchController.Intentions
{
    /// <summary>
    /// Drives intentions from anything that isn't a touch gesture: a mouse, a gamepad, a keyboard, the
    /// network, or a test. Create one with <see cref="IntentionSystem.CreateSource"/> and bind it with
    /// <see cref="Intention.Bind(IntentionSource)"/>. It acts like one extra finger: it goes down
    /// (<see cref="Begin"/>), moves, and lifts (<see cref="End"/>), and its intentions go through the same
    /// areas, priorities, claims and events as touch.
    /// </summary>
    /// <remarks>
    /// <para>Mapping: <see cref="Begin"/> → Possible and a start request (like a press: it starts in the
    /// next <see cref="IntentionSystem.Update"/>, if nothing with a higher priority holds it);
    /// <see cref="MoveTo"/> / <see cref="MoveBy"/> / <see cref="ScaleBy"/> / <see cref="RotateBy"/> → Performed;
    /// <see cref="End"/> → Ended(Completed); <see cref="Cancel"/> → Ended(Cancelled); <see cref="Tap"/> →
    /// Started, Performed and Ended in one update.</para>
    /// <para>Calls are queued and resolved in the next <see cref="IntentionSystem.Update"/>, like gesture
    /// events. Calls while the system is not running are ignored. Positions are in dp, origin bottom-left
    /// (pixels ÷ <c>TouchEnvironment.PixelsPerDp</c>). Times are on the Input System clock; omit them to use
    /// the current frame's time. Its pointer ID is negative, so it never clashes with a touch.</para>
    /// </remarks>
    public sealed class IntentionSource
    {
        private readonly IntentionSystem _system;
        private GestureInstance _instance;
        private double _lastTime;

        internal IntentionSource(IntentionSystem system, string name, int pointerId, IntentionSourceKind kind)
        {
            _system = system;
            Name = name;
            PointerId = pointerId;
            Kind = kind;
        }

        public string Name { get; }

        /// <summary>This source's pointer (finger) ID: negative, unique within its system.</summary>
        public int PointerId { get; }

        public IntentionSourceKind Kind { get; }

        public IntentionSystem System => _system;

        /// <summary>True between <see cref="Begin"/> and <see cref="End"/> / <see cref="Cancel"/>.</summary>
        public bool IsDown => _instance != null;

        /// <summary>Where the pointer is (dp).</summary>
        public Vector2 PositionDp { get; private set; }

        /// <summary>Goes down at <paramref name="positionDp"/>. If already down, the previous press is cancelled first.</summary>
        public void Begin(Vector2 positionDp, double? time = null)
        {
            if (!_system.IsRunning) return;
            if (IsDown) Cancel(time);

            var t = Time(time);
            _instance = new GestureInstance
            {
                Id = _system.NextSourceInstanceId(),
                State = GestureState.Possible,
                StartTime = t,
                PrimaryFingerId = PointerId,
                StartPositionDp = positionDp,
                PositionDp = positionDp,
            };
            _instance.FingerList.Add(PointerId);
            PositionDp = positionDp;
            _lastTime = t;
            Send(GestureEventType.Possible, GestureReason.None, t, Vector2.zero);
            _instance.State = GestureState.Active;
            _instance.EnterTime = t;
            Send(GestureEventType.Enter, GestureReason.None, t, Vector2.zero);
        }

        /// <summary>Moves to <paramref name="positionDp"/> (ignored when not down).</summary>
        public void MoveTo(Vector2 positionDp, double? time = null) => MoveBy(positionDp - PositionDp, time);

        /// <summary>Moves by <paramref name="deltaDp"/> (ignored when not down, or when zero).</summary>
        public void MoveBy(Vector2 deltaDp, double? time = null)
        {
            if (!IsDown || !_system.IsRunning || deltaDp == Vector2.zero) return;
            var t = Time(time);
            var dt = t - _lastTime;
            _instance.VelocityDps = dt > 1e-6 ? deltaDp / (float)dt : _instance.VelocityDps;
            _lastTime = t;
            PositionDp += deltaDp;
            _instance.PositionDp = PositionDp;
            Send(GestureEventType.Stay, GestureReason.None, t, deltaDp);
        }

        /// <summary>Scales by <paramref name="factor"/> (e.g. a mouse wheel or triggers zooming). Ignored when not down.</summary>
        public void ScaleBy(float factor, double? time = null)
        {
            if (!IsDown || !_system.IsRunning || factor == 1f) return;
            if (!(factor > 0f)) throw new ArgumentOutOfRangeException(nameof(factor), factor, "Must be greater than 0.");
            _instance.Scale *= factor;
            Send(GestureEventType.Stay, GestureReason.None, Time(time), Vector2.zero, factor);
        }

        /// <summary>Rotates by <paramref name="degrees"/> (counter-clockwise positive). Ignored when not down.</summary>
        public void RotateBy(float degrees, double? time = null)
        {
            if (!IsDown || !_system.IsRunning || degrees == 0f) return;
            _instance.AngleDeg += degrees;
            Send(GestureEventType.Stay, GestureReason.None, Time(time), Vector2.zero, 1f, degrees);
        }

        /// <summary>Lifts: the intention ends with Completed.</summary>
        public void End(double? time = null) => Finish(GestureReason.Completed, time);

        /// <summary>Lifts, abandoning: the intention ends with Cancelled.</summary>
        public void Cancel(double? time = null) => Finish(GestureReason.Cancelled, time);

        /// <summary>A discrete press at <paramref name="positionDp"/> (e.g. a click): Possible, Started, Performed and Ended in one update.</summary>
        public void Tap(Vector2 positionDp, double? time = null)
        {
            Begin(positionDp, time);
            if (IsDown) _instance.TapCount = 1;
            End(time);
        }

        public override string ToString() => $"{Name} (pointer {PointerId})";

        private void Finish(GestureReason reason, double? time)
        {
            if (!IsDown) return;
            if (_system.IsRunning)
            {
                _instance.State = GestureState.Finished;
                Send(GestureEventType.Exit, reason, Time(time), Vector2.zero);
            }
            _instance = null;
        }

        private void Send(GestureEventType type, GestureReason reason, double time, Vector2 deltaDp, float scaleDelta = 1f, float angleDeltaDeg = 0f) =>
            _system.EnqueueSource(this, new GestureEventData(null, _instance, type, reason, time, _system.PixelsPerDp, deltaDp, scaleDelta, angleDeltaDeg));

        private double Time(double? time) => time ?? _system.CurrentTime;
    }
}
