using System;
using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController.Testing
{
    /// <summary>
    /// Hand-scripted touch input for tests: produces <see cref="TouchFrame"/>s exactly as
    /// <see cref="TouchSource"/> would, with exact, test-controlled timing.
    /// </summary>
    /// <example>
    /// <code>
    /// var input = new ScriptedTouchInput();
    /// input.FrameProduced += history.Update;
    /// input.Down(1, new Vector2(100, 100)); input.Step();           // Began
    /// input.Drag(1, new Vector2(200, 100), durationMs: 100);       // Moved frames
    /// input.Up(1); input.Step();                                    // Ended
    /// </code>
    /// </example>
    /// <remarks>
    /// Changes (Down / Move / Up / Cancel) are queued and appear in the next <see cref="Step"/>.
    /// Like EnhancedTouch, a contact cannot begin and end in the same frame, and every contact appears
    /// in every frame of its life (Stationary when it did not move). The same frame instance is reused.
    /// <para>It is also an <see cref="ITouchInput"/>: as a TouchSystem's input, <see cref="Capture"/> copies
    /// the last stepped frame (see <see cref="TouchSystemHarness"/>).</para>
    /// </remarks>
    public sealed class ScriptedTouchInput : ITouchInput
    {
        void ITouchInput.Enable()
        {
        }

        void ITouchInput.Disable()
        {
        }

        /// <summary>
        /// Copies the last stepped frame into <paramref name="frame"/>. Scripted input supplies its own
        /// clock, so the frame keeps this input's <see cref="Time"/>; positions are already in dp.
        /// </summary>
        public void Capture(TouchFrame frame, float pixelsPerDp, double time, Vector2 screenSizePx)
        {
            frame.Reset(Time, pixelsPerDp, Frame.ScreenSizeDp);
            foreach (var touch in Frame.Touches)
                frame.Add(touch);
        }

        public const double DefaultStepMs = 1000.0 / 60.0;

        private readonly List<Contact> _contacts = new List<Contact>();
        private readonly float _pixelsPerDp;
        private readonly Vector2 _screenSizeDp;

        public ScriptedTouchInput(float pixelsPerDp = 1f, Vector2? screenSizeDp = null, double startTime = 0.0)
        {
            _pixelsPerDp = pixelsPerDp;
            _screenSizeDp = screenSizeDp ?? new Vector2(400f, 800f);
            Time = startTime;
        }

        /// <summary>Raised with each frame produced by <see cref="Step"/>.</summary>
        public event Action<TouchFrame> FrameProduced;

        /// <summary>Time of the last produced frame (seconds).</summary>
        public double Time { get; private set; }

        /// <summary>The most recent frame (reused between steps).</summary>
        public TouchFrame Frame { get; } = new TouchFrame();

        public void Down(int touchId, Vector2 positionDp)
        {
            if (Find(touchId) != null)
                throw new InvalidOperationException($"Touch {touchId} is already down.");
            _contacts.Add(new Contact { Id = touchId, Position = positionDp, Pending = TouchPointPhase.Began });
        }

        public void Move(int touchId, Vector2 positionDp)
        {
            var contact = Require(touchId);
            if (contact.Pending == TouchPointPhase.Ended || contact.Pending == TouchPointPhase.Canceled)
                throw new InvalidOperationException($"Touch {touchId} is already lifting.");
            contact.Position = positionDp;
            if (contact.Pending != TouchPointPhase.Began)
                contact.Pending = TouchPointPhase.Moved;
        }

        public void Up(int touchId) => End(touchId, TouchPointPhase.Ended);

        public void Cancel(int touchId) => End(touchId, TouchPointPhase.Canceled);

        /// <summary>Advances time and produces the next frame.</summary>
        public TouchFrame Step(double ms = DefaultStepMs)
        {
            if (ms < 0) throw new ArgumentOutOfRangeException(nameof(ms));
            Time += ms / 1000.0;

            _contacts.RemoveAll(c => c.Finished);
            Frame.Reset(Time, _pixelsPerDp, _screenSizeDp);
            foreach (var contact in _contacts)
            {
                var phase = contact.Pending ?? TouchPointPhase.Stationary;
                if (phase == TouchPointPhase.Began)
                {
                    contact.Start = contact.Position;
                    contact.StartTime = Time;
                }
                Frame.Add(new TouchPoint(contact.Id, phase, contact.Position, contact.Start, contact.StartTime));
                contact.Finished = phase == TouchPointPhase.Ended || phase == TouchPointPhase.Canceled;
                contact.Pending = null;
            }

            FrameProduced?.Invoke(Frame);
            return Frame;
        }

        /// <summary>Produces frames with no changes until <paramref name="ms"/> has passed.</summary>
        public TouchFrame Hold(double ms, double stepMs = DefaultStepMs)
        {
            var remaining = ms;
            do
            {
                var step = Math.Min(stepMs, remaining);
                Step(step);
                remaining -= step;
            } while (remaining > 1e-9);
            return Frame;
        }

        /// <summary>Moves a contact in a straight line at constant speed, one frame per step.</summary>
        public TouchFrame Drag(int touchId, Vector2 toDp, double durationMs, double stepMs = DefaultStepMs)
        {
            var from = Require(touchId).Position;
            var elapsed = 0.0;
            do
            {
                var step = Math.Min(stepMs, durationMs - elapsed);
                elapsed += step;
                var t = durationMs > 0 ? (float)(elapsed / durationMs) : 1f;
                Move(touchId, Vector2.Lerp(from, toDp, t));
                Step(step);
            } while (durationMs - elapsed > 1e-9);
            return Frame;
        }

        private void End(int touchId, TouchPointPhase phase)
        {
            var contact = Require(touchId);
            if (contact.Pending == TouchPointPhase.Began)
                throw new InvalidOperationException($"Touch {touchId} cannot begin and end in the same frame; call Step() first.");
            contact.Pending = phase;
        }

        private Contact Require(int touchId) =>
            Find(touchId) ?? throw new InvalidOperationException($"Touch {touchId} is not down.");

        private Contact Find(int touchId)
        {
            foreach (var contact in _contacts)
                if (contact.Id == touchId && !contact.Finished) return contact;
            return null;
        }

        private sealed class Contact
        {
            public int Id;
            public Vector2 Position;
            public Vector2 Start;
            public double StartTime;
            public TouchPointPhase? Pending;
            public bool Finished;
        }
    }
}
