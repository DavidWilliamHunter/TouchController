using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController.Demo
{
    /// <summary>Keeps the most recent gesture events as text for display. One start-up and one shut-down point.</summary>
    public sealed class GestureEventFeed
    {
        public struct Line
        {
            public string Text;
            public GestureEventType Type;
            public bool StateChange;
        }

        private readonly TouchSystem _touch;
        private readonly int _capacity;
        private readonly List<Line> _lines = new List<Line>();
        private readonly List<IGesture> _subscribed = new List<IGesture>();

        public GestureEventFeed(TouchSystem touch, int capacity)
        {
            _touch = touch;
            _capacity = capacity;
        }

        /// <summary>Show Possible events too (there are many: every touch makes most gestures possible).</summary>
        public bool IncludePossible { get; set; }

        /// <summary>Oldest first.</summary>
        public IReadOnlyList<Line> Lines => _lines;

        public void Startup()
        {
            foreach (var gesture in _touch.Gestures)
            {
                gesture.Possible += OnEvent;
                gesture.Fail += OnEvent;
                gesture.Enter += OnEvent;
                gesture.Exit += OnEvent;
                gesture.OnEnabled += OnEnabled;
                gesture.OnDisabled += OnDisabled;
                _subscribed.Add(gesture);
            }
        }

        public void Shutdown()
        {
            foreach (var gesture in _subscribed)
            {
                gesture.Possible -= OnEvent;
                gesture.Fail -= OnEvent;
                gesture.Enter -= OnEvent;
                gesture.Exit -= OnEvent;
                gesture.OnEnabled -= OnEnabled;
                gesture.OnDisabled -= OnDisabled;
            }
            _subscribed.Clear();
            _lines.Clear();
        }

        public void Clear() => _lines.Clear();

        private void OnEvent(GestureEventData e)
        {
            if (e.Type == GestureEventType.Possible && !IncludePossible)
                return;

            var text = $"{e.Time:F2}  {e.Gesture.Name} #{e.InstanceId} {e.Type}";
            if (e.Reason != GestureReason.None)
                text += $" ({e.Reason})";
            text += Detail(e);
            Add(new Line { Text = text, Type = e.Type });
        }

        private static string Detail(GestureEventData e)
        {
            if (e.Type != GestureEventType.Enter && e.Type != GestureEventType.Exit)
                return "";
            switch (e.Gesture)
            {
                case SwipeGesture _ when e.Type == GestureEventType.Enter:
                    return $"  dir {e.Direction}  {e.VelocityDps.magnitude:F0} dp/s";
                case PinchGesture _:
                    return $"  scale {e.Scale:F2}";
                case RotateGesture _:
                    return $"  {e.AngleDeg:F0}°";
                case EdgeSwipeGesture _:
                    return $"  {e.Edge}";
                case DoubleTapGesture _:
                case TapGesture _:
                    return $"  taps {e.TapCount}";
                default:
                    return e.FingerCount > 1 ? $"  {e.FingerCount} fingers" : "";
            }
        }

        private void OnEnabled(IGesture gesture) => Add(new Line { Text = $"{gesture.Name} enabled", StateChange = true });

        private void OnDisabled(IGesture gesture) => Add(new Line { Text = $"{gesture.Name} disabled", StateChange = true });

        private void Add(Line line)
        {
            _lines.Add(line);
            if (_lines.Count > _capacity)
                _lines.RemoveAt(0);
        }
    }
}
