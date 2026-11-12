using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Threads.TouchController
{
    /// <summary>
    /// Adapter from the Input System's <c>EnhancedTouch</c> API to <see cref="TouchFrame"/>s in dp.
    /// The only class in the package that reads touch input directly.
    /// </summary>
    /// <remarks>
    /// <para>Contacts get a session-unique <see cref="TouchPoint.touchId"/>. Input System touch IDs are
    /// only unique per device, and a Surface in the Editor has both a real touchscreen and the simulated
    /// one used by <c>TouchSimulation</c>, whose IDs can collide.</para>
    /// <para>Phases are worked out from what this source has seen, not taken from a single Input System
    /// update: EnhancedTouch only reports Began and Moved during the update in which they happened, so when
    /// the Input System updates more than once per frame (fixed-update mode, manual updates) they would be
    /// lost. A contact seen for the first time is Began; a Stationary contact whose position changed is
    /// Moved; a contact that disappears without an Ended being seen is Ended; one that began and ended
    /// between two captures is Began, then Ended in the next frame (a contact never begins and ends in one frame).</para>
    /// <para><c>EnhancedTouchSupport.Enable</c> is reference-counted, so enabling here does not interfere
    /// with other code using it. <c>TouchSimulation</c> is not: it is only disabled again if this source
    /// enabled it, and is enabled again on the next <see cref="Enable"/> if it is off.</para>
    /// </remarks>
    public sealed class TouchSource : ITouchInput, IDisposable
    {
        private readonly bool _simulateTouchInEditor;
        private readonly Dictionary<(int deviceId, int touchId), Contact> _contacts = new Dictionary<(int, int), Contact>();
        private readonly List<(int deviceId, int touchId)> _ended = new List<(int, int)>();
        private readonly HashSet<(int deviceId, int touchId)> _seen = new HashSet<(int, int)>();
        private readonly List<(int deviceId, int touchId)> _vanished = new List<(int, int)>();
        // Contacts ended in the previous capture (with their start time): EnhancedTouch may still list them
        // as Ended if no Input System update ran in between, and they must not come back as new contacts.
        private Dictionary<(int deviceId, int touchId), double> _recentlyEnded = new Dictionary<(int, int), double>();
        private Dictionary<(int deviceId, int touchId), double> _endedNow = new Dictionary<(int, int), double>();
        private int _nextId = 1;
        private bool _enabled;
        private bool _ownsSimulation;

        /// <param name="simulateTouchInEditor">In the Editor, turn mouse / pen input into touches (Input System <c>TouchSimulation</c>).</param>
        public TouchSource(bool simulateTouchInEditor = true)
        {
            _simulateTouchInEditor = simulateTouchInEditor;
        }

        public bool IsEnabled => _enabled;

        public void Enable()
        {
            if (_enabled) return;
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            // TouchSimulation.Disable() only disables the instance (it is not destroyed), so check
            // "enabled", not just "exists": otherwise a restart would leave simulation off.
            var simulation = TouchSimulation.instance;
            if (_simulateTouchInEditor && (simulation == null || !simulation.enabled))
            {
                TouchSimulation.Enable();
                _ownsSimulation = true;
            }
#endif
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled) return;
            if (_ownsSimulation)
            {
                TouchSimulation.Disable();
                _ownsSimulation = false;
            }
            EnhancedTouchSupport.Disable();
            _contacts.Clear();
            _ended.Clear();
            _recentlyEnded.Clear();
            _endedNow.Clear();
            _enabled = false;
        }

        public void Dispose() => Disable();

        /// <summary>Fills <paramref name="frame"/> with the current touches.</summary>
        public void Capture(TouchFrame frame, float pixelsPerDp, double time, Vector2 screenSizePx)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (!_enabled) throw new InvalidOperationException("TouchSource is not enabled. Call Enable() first.");

            // Contacts that ended in the previous capture release their IDs now.
            (_recentlyEnded, _endedNow) = (_endedNow, _recentlyEnded);
            _endedNow.Clear();
            foreach (var key in _ended)
            {
                _recentlyEnded[key] = _contacts[key].StartTime;
                _contacts.Remove(key);
            }
            _ended.Clear();
            _seen.Clear();

            frame.Reset(time, pixelsPerDp, screenSizePx / pixelsPerDp);

            // Contacts that began and ended between two captures: their Began was sent last frame, their end now.
            foreach (var pair in _contacts)
            {
                var contact = pair.Value;
                if (!contact.PendingEnd.HasValue) continue;
                _seen.Add(pair.Key);
                _ended.Add(pair.Key);
                frame.Add(contact.Point(contact.PendingEnd.Value, contact.LastPx, pixelsPerDp));
            }

            foreach (var touch in EnhancedTouch.activeTouches)
            {
                if (!TryMapPhase(touch.phase, out var phase))
                    continue;
                var key = (touch.screen.deviceId, touch.touchId);
                if (!_seen.Add(key))
                    continue; // already sent this frame (its pending end); a reused ID is picked up next capture

                var ending = phase == TouchPointPhase.Ended || phase == TouchPointPhase.Canceled;
                if (ending && !_contacts.ContainsKey(key) && _recentlyEnded.TryGetValue(key, out var endedStart) && endedStart == touch.startTime)
                {
                    _endedNow[key] = endedStart; // the same ended contact, still listed: already reported
                    continue;
                }
                var known = _contacts.TryGetValue(key, out var contact);
                var isNew = !known || phase == TouchPointPhase.Began || contact.StartTime != touch.startTime;
                if (isNew)
                {
                    if (known) // the device reused the ID before we saw the old contact end: end it now
                        frame.Add(contact.Point(TouchPointPhase.Ended, contact.LastPx, pixelsPerDp));

                    contact = new Contact(_nextId++, touch.startScreenPosition, touch.startTime, touch.touchId, touch.screen.deviceId);
                    _contacts[key] = contact;
                    contact.LastPx = touch.screenPosition;
                    if (ending)
                        contact.PendingEnd = phase; // began and ended since the last capture: Began now, the end next frame
                    frame.Add(contact.Point(TouchPointPhase.Began, touch.screenPosition, pixelsPerDp));
                    continue;
                }

                if (phase == TouchPointPhase.Stationary && touch.screenPosition != contact.LastPx)
                    phase = TouchPointPhase.Moved; // it moved in an Input System update we didn't capture
                contact.LastPx = touch.screenPosition;
                if (ending)
                    _ended.Add(key);
                frame.Add(contact.Point(phase, touch.screenPosition, pixelsPerDp));
            }

            // Contacts that disappeared without an Ended being seen (it happened in an update we didn't capture).
            _vanished.Clear();
            foreach (var pair in _contacts)
                if (!_seen.Contains(pair.Key))
                    _vanished.Add(pair.Key);
            foreach (var key in _vanished)
            {
                var contact = _contacts[key];
                _ended.Add(key);
                frame.Add(contact.Point(TouchPointPhase.Ended, contact.LastPx, pixelsPerDp));
            }
        }

        /// <summary>One contact as this source has seen it.</summary>
        private sealed class Contact
        {
            public Contact(int id, Vector2 startPx, double startTime, int inputTouchId, int inputDeviceId)
            {
                Id = id;
                StartPx = startPx;
                StartTime = startTime;
                InputTouchId = inputTouchId;
                InputDeviceId = inputDeviceId;
            }

            public int Id { get; }
            public Vector2 StartPx { get; }
            public double StartTime { get; }
            public int InputTouchId { get; }
            public int InputDeviceId { get; }
            public Vector2 LastPx;
            public TouchPointPhase? PendingEnd;

            public TouchPoint Point(TouchPointPhase phase, Vector2 positionPx, float pixelsPerDp) =>
                new TouchPoint(Id, phase, positionPx / pixelsPerDp, StartPx / pixelsPerDp, StartTime, InputTouchId, InputDeviceId);
        }

        internal static bool TryMapPhase(InputTouchPhase phase, out TouchPointPhase mapped)
        {
            switch (phase)
            {
                case InputTouchPhase.Began: mapped = TouchPointPhase.Began; return true;
                case InputTouchPhase.Moved: mapped = TouchPointPhase.Moved; return true;
                case InputTouchPhase.Stationary: mapped = TouchPointPhase.Stationary; return true;
                case InputTouchPhase.Ended: mapped = TouchPointPhase.Ended; return true;
                case InputTouchPhase.Canceled: mapped = TouchPointPhase.Canceled; return true;
                default: mapped = default; return false;
            }
        }
    }
}
