using System;
using System.Collections.Generic;

namespace Threads.TouchController.Testing
{
    /// <summary>
    /// Records a gesture's events for assertions, and checks that every instance follows the event
    /// rules (Possible first; then Fail, or Enter → Stay* → Exit; nothing after the end; silence while
    /// disabled).
    /// </summary>
    /// <example>
    /// <code>
    /// CollectionAssert.AreEqual(new[] { "Possible#1", "Enter#1", "Exit(Completed)#1" }, log.WithoutStays());
    /// Assert.IsEmpty(log.Violations);
    /// </code>
    /// </example>
    public sealed class GestureEventLog : IDisposable
    {
        private readonly IGesture _gesture;
        private readonly List<GestureEventData> _events = new List<GestureEventData>();
        private readonly List<string> _entries = new List<string>();
        private readonly List<string> _violations = new List<string>();
        private readonly Dictionary<int, GestureEventType> _lastByInstance = new Dictionary<int, GestureEventType>();
        private bool _disabled;

        public GestureEventLog(IGesture gesture)
        {
            _gesture = gesture ?? throw new ArgumentNullException(nameof(gesture));
            _disabled = !gesture.Enabled;
            gesture.Possible += Record;
            gesture.Fail += Record;
            gesture.Enter += Record;
            gesture.Stay += Record;
            gesture.Exit += Record;
            gesture.OnEnabled += RecordEnabled;
            gesture.OnDisabled += RecordDisabled;
        }

        /// <summary>The events, in order (OnEnabled / OnDisabled are only in <see cref="Entries"/>).</summary>
        public IReadOnlyList<GestureEventData> Events => _events;

        /// <summary>
        /// Every event as text, in order: "Possible#1", "Enter#1", "Stay#1", "Exit(Completed)#1",
        /// "Fail(Moved)#2", "OnDisabled", "OnEnabled". Numbers are instance IDs.
        /// </summary>
        public IReadOnlyList<string> Entries => _entries;

        /// <summary>Broken event rules, as text. Should be empty.</summary>
        public IReadOnlyList<string> Violations => _violations;

        /// <summary><see cref="Entries"/> without the (frequent) Stay events.</summary>
        public List<string> WithoutStays() => _entries.FindAll(e => !e.StartsWith("Stay", StringComparison.Ordinal));

        public int Count(GestureEventType type) => _events.FindAll(e => e.Type == type).Count;

        /// <summary>Forgets recorded events (the rule checking continues).</summary>
        public void Clear()
        {
            _events.Clear();
            _entries.Clear();
        }

        public void Dispose()
        {
            _gesture.Possible -= Record;
            _gesture.Fail -= Record;
            _gesture.Enter -= Record;
            _gesture.Stay -= Record;
            _gesture.Exit -= Record;
            _gesture.OnEnabled -= RecordEnabled;
            _gesture.OnDisabled -= RecordDisabled;
        }

        private void Record(GestureEventData e)
        {
            _events.Add(e);
            _entries.Add(e.Reason == GestureReason.None ? $"{e.Type}#{e.InstanceId}" : $"{e.Type}({e.Reason})#{e.InstanceId}");
            Check(e);
        }

        private void RecordEnabled(IGesture gesture)
        {
            _entries.Add("OnEnabled");
            _disabled = false;
        }

        private void RecordDisabled(IGesture gesture)
        {
            _entries.Add("OnDisabled");
            _disabled = true;
            _lastByInstance.Clear(); // all instances discarded without Fail / Exit
        }

        private void Check(GestureEventData e)
        {
            if (_disabled)
                Violation(e, "event while disabled");

            var known = _lastByInstance.TryGetValue(e.InstanceId, out var last);
            var ok = e.Type switch
            {
                GestureEventType.Possible => !known,
                GestureEventType.Fail => known && last == GestureEventType.Possible,
                GestureEventType.Enter => known && last == GestureEventType.Possible,
                GestureEventType.Stay => known && (last == GestureEventType.Enter || last == GestureEventType.Stay),
                GestureEventType.Exit => known && (last == GestureEventType.Enter || last == GestureEventType.Stay),
                _ => false,
            };
            if (!ok)
                Violation(e, known ? $"{e.Type} after {last}" : $"{e.Type} before Possible");

            if (e.Type == GestureEventType.Fail && (e.Reason == GestureReason.None || e.Reason == GestureReason.Completed))
                Violation(e, $"Fail with reason {e.Reason}");
            if (e.Type == GestureEventType.Exit && e.Reason != GestureReason.Completed && e.Reason != GestureReason.Cancelled)
                Violation(e, $"Exit with reason {e.Reason}");

            if (e.Type == GestureEventType.Fail || e.Type == GestureEventType.Exit)
                _lastByInstance[e.InstanceId] = (GestureEventType)(-1); // finished: anything further is a violation
            else
                _lastByInstance[e.InstanceId] = e.Type;
        }

        private void Violation(GestureEventData e, string message) =>
            _violations.Add($"{_gesture.Name} #{e.InstanceId}: {message}");
    }
}
