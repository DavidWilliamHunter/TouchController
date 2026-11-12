using System;
using System.Collections.Generic;
using System.Linq;

namespace Threads.TouchController.Intentions.Testing
{
    /// <summary>
    /// Records an intention's events as text (<c>"Possible#1"</c>, <c>"Ended(Completed)#1"</c>, …, where
    /// the number is the instance ID) and checks the event rules (<see cref="Violations"/>).
    /// </summary>
    public sealed class IntentionEventLog : IDisposable
    {
        private readonly Intention _intention;
        private readonly Dictionary<int, IntentionEventType> _last = new Dictionary<int, IntentionEventType>();
        private readonly HashSet<int> _finished = new HashSet<int>();

        public IntentionEventLog(Intention intention)
        {
            _intention = intention ?? throw new ArgumentNullException(nameof(intention));
            _intention.Possible += OnEvent;
            _intention.Rejected += OnEvent;
            _intention.Started += OnEvent;
            _intention.Performed += OnEvent;
            _intention.Ended += OnEvent;
        }

        /// <summary>Every event, as text.</summary>
        public List<string> Entries { get; } = new List<string>();

        /// <summary>Every event, with its data.</summary>
        public List<IntentionEventData> Events { get; } = new List<IntentionEventData>();

        /// <summary>Broken event rules, as text. Empty when every instance followed the lifecycle.</summary>
        public List<string> Violations { get; } = new List<string>();

        /// <summary>The entries without the given event types (e.g. without Performed).</summary>
        public List<string> Without(params IntentionEventType[] types) =>
            Events.Where(e => !types.Contains(e.Type)).Select(Format).ToList();

        /// <summary>The entries without Performed.</summary>
        public List<string> WithoutPerformed() => Without(IntentionEventType.Performed);

        public int Count(IntentionEventType type) => Events.Count(e => e.Type == type);

        public void Clear()
        {
            Entries.Clear();
            Events.Clear();
        }

        public void Dispose()
        {
            _intention.Possible -= OnEvent;
            _intention.Rejected -= OnEvent;
            _intention.Started -= OnEvent;
            _intention.Performed -= OnEvent;
            _intention.Ended -= OnEvent;
        }

        public static string Format(IntentionEventData e) =>
            e.Reason == IntentionReason.None ? $"{e.Type}#{e.InstanceId}" : $"{e.Type}({e.Reason})#{e.InstanceId}";

        private void OnEvent(IntentionEventData e)
        {
            Entries.Add(Format(e));
            Events.Add(e);
            Check(e);
        }

        private void Check(IntentionEventData e)
        {
            var id = e.InstanceId;
            var hasLast = _last.TryGetValue(id, out var last);
            void Violation(string rule) => Violations.Add($"{Format(e)}: {rule}");

            if (_finished.Contains(id))
                Violation("event after Rejected/Ended");
            else if (!hasLast && e.Type != IntentionEventType.Possible)
                Violation("first event must be Possible");
            else if (hasLast)
            {
                switch (e.Type)
                {
                    case IntentionEventType.Possible:
                        Violation("Possible sent twice");
                        break;
                    case IntentionEventType.Rejected:
                        if (last != IntentionEventType.Possible) Violation("Rejected only before Started");
                        break;
                    case IntentionEventType.Started:
                        if (last != IntentionEventType.Possible) Violation("Started only once, after Possible");
                        break;
                    case IntentionEventType.Performed:
                        if (last != IntentionEventType.Started && last != IntentionEventType.Performed) Violation("Performed only while started");
                        break;
                    case IntentionEventType.Ended:
                        if (last != IntentionEventType.Performed && last != IntentionEventType.Started) Violation("Ended only after Started");
                        break;
                }
                // A Started handler may end the instance (disable it, shut the system down): then Ended follows at once.
                if (last == IntentionEventType.Started && e.Type != IntentionEventType.Performed && e.Type != IntentionEventType.Ended)
                    Violation("Started must be followed by Performed (or by Ended, if a handler ended it)");
            }

            if ((e.Type == IntentionEventType.Started || e.Type == IntentionEventType.Performed) && e.Reason != IntentionReason.None)
                Violation("only Rejected and Ended carry a reason");
            if ((e.Type == IntentionEventType.Rejected || e.Type == IntentionEventType.Ended) && e.Reason == IntentionReason.None)
                Violation("Rejected and Ended need a reason");

            _last[id] = e.Type;
            if (e.Type == IntentionEventType.Rejected || e.Type == IntentionEventType.Ended)
                _finished.Add(id);
        }
    }
}
