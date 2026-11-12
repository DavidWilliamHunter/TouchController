using System;
using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController.Intentions
{
    /// <summary>
    /// Turns gestures into intentions: the single place intentions are started, resolved and shut down.
    /// Given the <see cref="TouchSystem"/> it reads from; no global state.
    /// </summary>
    /// <remarks>
    /// <para>Lifecycle: construct (nothing starts), <see cref="Startup"/> after <c>TouchSystem.Startup</c>,
    /// <see cref="Update"/> every frame <b>straight after</b> <c>TouchSystem.Update</c>, and
    /// <see cref="Shutdown"/> before <c>TouchSystem.Shutdown</c>.</para>
    /// <para>Gesture events are queued while the TouchSystem updates and resolved together in
    /// <see cref="Update"/>, so the result never depends on the order of gestures in the lineup.
    /// Intention events are sent from <see cref="Update"/> (and from <see cref="Intention.Enabled"/> /
    /// <see cref="Shutdown"/>, for instances they end).</para>
    /// <para>Arbitration per finger (plan.md §9.5): start requests in the same update are granted in
    /// descending priority (ties: the intention enabled first). A request is rejected if a finger is held
    /// by an active exclusive instance (of another intention) of equal or higher priority. An exclusive
    /// instance that starts rejects or preempts lower-priority instances (of other intentions) on its
    /// fingers. Instances of the same intention never compete: one intention bound to Pinch, Rotate and
    /// TwoFingerPan runs all three together.</para>
    /// </remarks>
    public sealed class IntentionSystem : IDisposable
    {
        private readonly List<Intention> _enabled = new List<Intention>();
        private readonly Dictionary<IGesture, GestureFeed> _feeds = new Dictionary<IGesture, GestureFeed>();
        private readonly Dictionary<IntentionSource, GestureFeed> _sourceFeeds = new Dictionary<IntentionSource, GestureFeed>();
        private int _nextPointerId = -1;        // sources' pointer IDs: negative, never a touch's
        private int _nextSourceInstanceId = -1; // their instance IDs: negative, never a gesture instance's
        private readonly List<QueuedEvent> _queue = new List<QueuedEvent>();
        private readonly Stack<List<int>> _fingerListPool = new Stack<List<int>>();
        private readonly Dictionary<int, FingerRecord> _fingers = new Dictionary<int, FingerRecord>();
        private readonly List<int> _endedFingers = new List<int>();
        private readonly List<IntentionInstance> _live = new List<IntentionInstance>();
        private readonly List<IntentionInstance> _requests = new List<IntentionInstance>();
        private readonly List<IntentionInstance> _candidates = new List<IntentionInstance>();
        private readonly List<IntentionInstance> _scratch = new List<IntentionInstance>();
        private readonly List<Intention> _snapshot = new List<Intention>();
        private int _nextInstanceId = 1;
        private int _nextOrder;
        private bool _updating;
        private bool _clearAfterUpdate; // Shutdown from an event handler: Update is still iterating the lists
        private bool _dispatching;
        private readonly Queue<(IntentionEventType type, IntentionEventData data)> _outbox = new Queue<(IntentionEventType, IntentionEventData)>();

        /// <summary>Configures the system. Nothing is started until <see cref="Startup"/>.</summary>
        public IntentionSystem(TouchSystem touch)
        {
            Touch = touch ?? throw new ArgumentNullException(nameof(touch));
        }

        public TouchSystem Touch { get; }

        /// <summary>True between <see cref="Startup"/> and <see cref="Shutdown"/>.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Enabled intentions, in the order they were enabled (registered while running).</summary>
        public IReadOnlyList<Intention> Intentions => _enabled;

        /// <summary>Registers every enabled intention. Call after <c>TouchSystem.Startup</c>. Throws if already running.</summary>
        public void Startup()
        {
            if (IsRunning)
                throw new InvalidOperationException("IntentionSystem is already running. Call Shutdown() first.");
            if (!Touch.IsRunning)
                throw new InvalidOperationException("Start the TouchSystem before the IntentionSystem.");
            if (_updating)
                throw new InvalidOperationException("IntentionSystem.Startup cannot be called from an intention event.");

            IsRunning = true;
            try
            {
                foreach (var intention in _enabled)
                    Activate(intention);
            }
            catch
            {
                Release();
                throw;
            }
        }

        /// <summary>Resolves this frame's gestures into intentions. Call once per frame, straight after <c>TouchSystem.Update</c>.</summary>
        public void Update()
        {
            RequireRunning();
            if (_updating)
                throw new InvalidOperationException("IntentionSystem.Update cannot be called from an intention event.");

            _updating = true;
            try
            {
                var frame = Touch.Frame;
                ReadFrame(frame);
                BeforeUpdate?.Invoke(frame.Time);
                ProcessQueue(frame);
                ResolveRequests();
                ResolveWaiting();
                Grant();
            }
            finally
            {
                _updating = false;
                EndOfUpdate();
                if (_clearAfterUpdate)
                    ClearState();
            }
        }

        /// <summary>
        /// Ends every instance (<see cref="IntentionReason.Disabled"/>) and unsubscribes from the gestures.
        /// Intentions stay enabled and register again at the next <see cref="Startup"/>. Safe to call when not running.
        /// </summary>
        public void Shutdown() => Release();

        /// <summary>Same as <see cref="Shutdown"/>.</summary>
        public void Dispose() => Release();

        // ---- Sources ---------------------------------------------------------------------------------

        /// <summary>
        /// A source of intentions other than touch gestures (mouse, gamepad, keyboard, network, tests): see
        /// <see cref="IntentionSource"/>. Bind it with <see cref="Intention.Bind(IntentionSource)"/>. It belongs
        /// to this system for its whole life (across restarts).
        /// </summary>
        public IntentionSource CreateSource(string name, IntentionSourceKind kind = IntentionSourceKind.Manual)
        {
            if (kind == IntentionSourceKind.Gesture)
                throw new ArgumentException("Gesture is not a source kind: gestures are bound directly.", nameof(kind));
            var source = new IntentionSource(this, name, _nextPointerId--, kind);
            _sourceFeeds.Add(source, new GestureFeed(source, kind));
            return source;
        }

        internal int NextSourceInstanceId() => _nextSourceInstanceId--;

        /// <summary>Sent at the start of every <see cref="Update"/>, with the frame time: sources that poll (sticks) act here.</summary>
        internal event Action<double> BeforeUpdate;

        internal float PixelsPerDp => Touch.IsRunning ? Touch.Environment.PixelsPerDp : 1f;

        internal double CurrentTime => Touch.IsRunning ? Touch.Frame.Time : 0.0;

        internal void EnqueueSource(IntentionSource source, in GestureEventData data)
        {
            if (_sourceFeeds.TryGetValue(source, out var feed))
                Enqueue(feed, data.Type, data);
        }

        // ---- Registration (from Intention.Enabled) ---------------------------------------------

        internal void Register(Intention intention)
        {
            if (IsRunning)
                Activate(intention); // throws before anything changes if a gesture is missing
            intention.Order = _nextOrder++;
            _enabled.Add(intention);
        }

        internal void Deregister(Intention intention)
        {
            _enabled.Remove(intention);
            if (intention.Registered)
                Deactivate(intention, IntentionReason.Disabled);
        }

        private void Activate(Intention intention)
        {
            intention.Resolve(Touch);
            foreach (var source in intention.BoundSources)
                if (source.System != this)
                    throw new InvalidOperationException($"Intention '{intention.Name}': source '{source.Name}' belongs to another IntentionSystem.");
            foreach (var g in intention.BoundGestures) Feed(g);
            foreach (var g in intention.RequiredToFail) Feed(g);
            foreach (var g in intention.RequiredActive) Feed(g);
            intention.Registered = true;
        }

        private void Deactivate(Intention intention, IntentionReason reason)
        {
            intention.Registered = false;
            _scratch.Clear();
            _scratch.AddRange(intention.InstanceList);
            FinishCollected(reason);
            intention.ByGestureInstance.Clear();
            if (!_updating)
                Prune(intention);
        }

        private GestureFeed Feed(IGesture gesture)
        {
            if (!_feeds.TryGetValue(gesture, out var feed))
            {
                feed = new GestureFeed(this, gesture);
                _feeds.Add(gesture, feed);
            }
            return feed;
        }

        // ---- Fingers and areas ------------------------------------------------------------------

        private void ReadFrame(TouchFrame frame)
        {
            foreach (var touch in frame.Touches)
            {
                if (touch.phase == TouchPointPhase.Began && !_fingers.ContainsKey(touch.touchId))
                {
                    var record = new FingerRecord(new IntentionPoint(touch.touchId, touch.inputTouchId, touch.startPositionDp,
                        frame.PixelsPerDp, frame.ScreenSizeDp, frame.Time));
                    _fingers.Add(touch.touchId, record);
                    // Check areas now, while what is under the finger is what the user touched.
                    foreach (var intention in _enabled)
                        if (intention.Registered && intention.Area != null)
                            record.Accepts(intention.Area, out _);
                }
                if (touch.HasEnded)
                    _endedFingers.Add(touch.touchId);
            }
        }

        private FingerRecord Finger(int fingerId, Vector2 fallbackPositionDp, TouchFrame frame)
        {
            if (_fingers.TryGetValue(fingerId, out var record))
                return record;
            // A touch whose Began frame this system did not see (Update not called that frame): use the
            // gesture's position. Not cached, so nothing outlives the touch.
            return new FingerRecord(new IntentionPoint(fingerId, 0, fallbackPositionDp, frame.PixelsPerDp, frame.ScreenSizeDp, frame.Time));
        }

        private bool AcceptsAtStart(Intention intention, in QueuedEvent e, TouchFrame frame, out object target)
        {
            target = null;
            var area = intention.Area;
            if (area == null) return true;

            switch (intention.FingerRule)
            {
                case FingerRule.Primary:
                    return Finger(e.Data.FingerId, e.Data.PositionDp, frame).Accepts(area, out target);

                case FingerRule.All:
                    foreach (var id in e.Fingers)
                    {
                        if (!Finger(id, e.Data.PositionDp, frame).Accepts(area, out var t))
                        {
                            target = null;
                            return false;
                        }
                        if (id == e.Data.FingerId || target == null) target = t;
                    }
                    return e.Fingers.Count > 0;

                default:
                    // Any: prefer the primary finger's target.
                    if (Finger(e.Data.FingerId, e.Data.PositionDp, frame).Accepts(area, out target))
                        return true;
                    foreach (var id in e.Fingers)
                        if (id != e.Data.FingerId && Finger(id, e.Data.PositionDp, frame).Accepts(area, out target))
                            return true;
                    return false;
            }
        }

        private static bool InAreaNow(IntentionInstance instance, TouchFrame frame)
        {
            var area = instance.Intention.Area;
            if (area == null) return true;
            var point = new IntentionPoint(instance.PrimaryFingerId, 0, instance.PositionDp, frame.PixelsPerDp, frame.ScreenSizeDp, frame.Time);
            return area.Accepts(point, out _);
        }

        // ---- Gesture events -----------------------------------------------------------------------

        private void Enqueue(GestureFeed feed, GestureEventType type, in GestureEventData data)
        {
            if (!IsRunning) return;
            var fingers = _fingerListPool.Count > 0 ? _fingerListPool.Pop() : new List<int>(4);
            fingers.Clear();
            fingers.AddRange(data.FingerIds); // the gesture's list is live: copy it now
            _queue.Add(new QueuedEvent(feed, type, data, fingers, false));
        }

        private void EnqueueDisabled(GestureFeed feed)
        {
            if (!IsRunning) return;
            _queue.Add(new QueuedEvent(feed, default, default, null, true));
        }

        private void ProcessQueue(TouchFrame frame)
        {
            // Intentions enabled by a handler during this update see events from the next update.
            _snapshot.Clear();
            _snapshot.AddRange(_enabled);
            for (var i = 0; i < _queue.Count; i++)
            {
                var e = _queue[i];
                if (e.Feed.Gesture != null) e.Feed.Track(e); // requirements only concern gestures
                foreach (var intention in _snapshot)
                {
                    if (!intention.Registered || !intention.BoundKeys.Contains(e.Feed.Key)) continue;
                    if (e.Disabled) GestureDisabled(intention, e.Feed.Gesture);
                    else Handle(intention, e, frame);
                }
            }
            foreach (var e in _queue)
                if (e.Fingers != null) _fingerListPool.Push(e.Fingers);
            _queue.Clear();
        }

        private void Handle(Intention intention, in QueuedEvent e, TouchFrame frame)
        {
            var data = e.Data;
            if (e.Type == GestureEventType.Possible)
            {
                if (intention.ByGestureInstance.ContainsKey(data.InstanceId)) return;
                if (!AcceptsAtStart(intention, e, frame, out var target)) return;

                var instance = new IntentionInstance(_nextInstanceId++, intention)
                {
                    State = IntentionState.Possible,
                    Source = e.Feed.Kind,
                    Gesture = e.Feed.Gesture,
                    GestureInstanceId = data.InstanceId,
                    StartTime = data.Time,
                    PrimaryFingerId = data.FingerId,
                    Target = target,
                    StartPositionDp = data.Instance != null ? data.Instance.StartPositionDp : data.PositionDp,
                };
                Apply(instance, e, frame);
                instance.ResetPending();
                if (BlockedByExclusive(instance)) return; // a finger is already taken: never becomes a candidate

                intention.ByGestureInstance.Add(data.InstanceId, instance);
                intention.InstanceList.Add(instance);
                _live.Add(instance);
                Send(instance, IntentionEventType.Possible, IntentionReason.None);
                return;
            }

            if (!intention.ByGestureInstance.TryGetValue(data.InstanceId, out var inst) || inst.State == IntentionState.Finished)
                return;
            Apply(inst, e, frame);

            switch (e.Type)
            {
                case GestureEventType.Fail:
                    Finish(inst, data.Reason == GestureReason.Cancelled ? IntentionReason.Cancelled : IntentionReason.GestureFailed);
                    break;

                case GestureEventType.Enter:
                    if (intention.AreaCheck == AreaCheck.Continuous && !InAreaNow(inst, frame))
                    {
                        Finish(inst, IntentionReason.LeftArea);
                        break;
                    }
                    inst.EnterRequested = true;
                    _requests.Add(inst);
                    break;

                case GestureEventType.Stay:
                    if (intention.AreaCheck == AreaCheck.Continuous && !InAreaNow(inst, frame))
                    {
                        Finish(inst, IntentionReason.LeftArea);
                        break;
                    }
                    if (inst.State == IntentionState.Active)
                        SendPerformed(inst);
                    break;

                case GestureEventType.Exit:
                    var reason = data.Reason == GestureReason.Cancelled ? IntentionReason.Cancelled : IntentionReason.Completed;
                    if (inst.State == IntentionState.Active)
                    {
                        if (HasPendingMovement(inst))
                            SendPerformed(inst); // the movement of the last frame
                        if (inst.State == IntentionState.Active)
                            Finish(inst, reason);
                    }
                    else if (inst.State == IntentionState.Waiting && reason == IntentionReason.Cancelled)
                        Finish(inst, IntentionReason.Cancelled);
                    else if (inst.State == IntentionState.Possible && !inst.EnterRequested)
                        Finish(inst, IntentionReason.GestureFailed); // cannot happen with the built-in gestures
                    else
                        inst.PendingEnd = reason; // entered this update, or waiting: ends right after it starts
                    break;
            }
        }

        private static bool HasPendingMovement(IntentionInstance inst) =>
            inst.PendingDeltaDp != Vector2.zero || inst.PendingScaleDelta != 1f || inst.PendingAngleDeltaDeg != 0f;

        private static void Apply(IntentionInstance inst, in QueuedEvent e, TouchFrame frame)
        {
            var data = e.Data;
            inst.Time = data.Time;
            inst.PixelsPerDp = frame.PixelsPerDp;
            inst.PositionDp = data.PositionDp;
            inst.VelocityDps = data.VelocityDps;
            inst.Scale = data.Scale;
            inst.AngleDeg = data.AngleDeg;
            inst.TapCount = data.TapCount;
            inst.Direction = data.Direction;
            inst.Edge = data.Edge;
            inst.PendingDeltaDp += data.DeltaDp;
            inst.PendingScaleDelta *= data.ScaleDelta;
            inst.PendingAngleDeltaDeg += data.AngleDeltaDeg;
            inst.FingerList.Clear();
            inst.FingerList.AddRange(e.Fingers);
            foreach (var id in e.Fingers) inst.EverFingers.Add(id);
        }

        private void GestureDisabled(Intention intention, IGesture gesture)
        {
            _scratch.Clear();
            foreach (var inst in intention.InstanceList)
                if (inst.Gesture == gesture && inst.State != IntentionState.Finished)
                    _scratch.Add(inst);
            FinishCollected(IntentionReason.Disabled);
        }

        // ---- Requirements and arbitration --------------------------------------------------------------

        private void ResolveRequests()
        {
            foreach (var inst in _requests)
            {
                if (inst.State != IntentionState.Possible) continue;
                var intention = inst.Intention;

                if (intention.StartCondition != null && !StartConditionMet(inst))
                {
                    Finish(inst, IntentionReason.StartConditionFailed);
                    continue;
                }

                if (intention.RequiredActive.Count > 0 && !RequiredActiveMet(inst))
                {
                    Finish(inst, IntentionReason.RequirementNotMet);
                    continue;
                }

                if (intention.RequiredToFail.Count > 0)
                {
                    inst.WaitingOn = new List<GestureRecord>();
                    foreach (var g in intention.RequiredToFail)
                        _feeds[g].CollectOverlapping(inst.EverFingers, inst.WaitingOn);
                    inst.State = IntentionState.Waiting; // resolved (perhaps at once) by ResolveWaiting
                    continue;
                }
                _candidates.Add(inst);
            }
            _requests.Clear();
        }

        private static bool StartConditionMet(IntentionInstance inst)
        {
            try
            {
                return inst.Intention.StartCondition(new IntentionEventData(inst, IntentionEventType.Started, IntentionReason.None, false));
            }
            catch (Exception e)
            {
                // A failing condition must not break arbitration: treat it as "don't start".
                UnityEngine.Debug.LogException(e);
                return false;
            }
        }

        private bool RequiredActiveMet(IntentionInstance inst)
        {
            foreach (var g in inst.Intention.RequiredActive)
                if (!_feeds[g].AnyActiveOn(inst.EverFingers))
                    return false;
            return true;
        }

        private void ResolveWaiting()
        {
            foreach (var inst in _live)
            {
                if (inst.State != IntentionState.Waiting) continue;
                var allFailed = true;
                var entered = false;
                foreach (var record in inst.WaitingOn)
                {
                    if (record.Entered) entered = true;
                    else if (!record.Finished) allFailed = false;
                }
                if (entered)
                {
                    _scratch.Add(inst);
                }
                else if (allFailed)
                {
                    inst.WaitingOn = null;
                    inst.State = IntentionState.Possible;
                    _candidates.Add(inst);
                }
            }
            FinishCollected(IntentionReason.RequiredGestureEntered);
        }

        private void Grant()
        {
            _candidates.Sort((a, b) =>
            {
                var byPriority = b.Intention.Priority.CompareTo(a.Intention.Priority);
                if (byPriority != 0) return byPriority;
                var byOrder = a.Intention.Order.CompareTo(b.Intention.Order);
                return byOrder != 0 ? byOrder : a.Id.CompareTo(b.Id);
            });

            foreach (var inst in _candidates)
            {
                if (inst.State != IntentionState.Possible || !inst.Intention.Registered) continue; // lost earlier this update

                if (BlockedByExclusive(inst))
                {
                    Finish(inst, IntentionReason.LostToPriority);
                    continue;
                }

                if (inst.Intention.Claim == ClaimMode.Exclusive)
                    Preempt(inst);

                inst.State = IntentionState.Active;
                Send(inst, IntentionEventType.Started, IntentionReason.None);
                if (inst.State != IntentionState.Active) continue; // a handler disabled it
                SendPerformed(inst);
                if (inst.State == IntentionState.Active && inst.PendingEnd.HasValue)
                    Finish(inst, inst.PendingEnd.Value);
            }
            _candidates.Clear();
        }

        /// <summary>True if one of its fingers is held by an active exclusive instance of another intention, of equal or higher priority.</summary>
        private bool BlockedByExclusive(IntentionInstance inst)
        {
            foreach (var other in _live)
            {
                if (other.Intention == inst.Intention || other.State != IntentionState.Active) continue; // never competes with itself
                if (other.Intention.Claim != ClaimMode.Exclusive || other.Intention.Priority < inst.Intention.Priority) continue;
                if (inst.SharesFingerWith(other)) return true;
            }
            return false;
        }

        private void Preempt(IntentionInstance winner)
        {
            _scratch.Clear();
            foreach (var other in _live)
            {
                if (other.Intention == winner.Intention || other.State == IntentionState.Finished) continue;
                if (other.Intention.Priority >= winner.Intention.Priority) continue;
                if (winner.SharesFingerWith(other)) _scratch.Add(other);
            }
            FinishCollected(IntentionReason.LostToPriority, IntentionReason.Preempted);
        }

        // ---- Sending ---------------------------------------------------------------------------------

        /// <summary>
        /// Sends an event. An event raised while another is being delivered (a handler that disables an
        /// intention, or shuts the system down) is queued and delivered as soon as the current one has
        /// reached every subscriber, so all subscribers see each instance's events in order. The data is
        /// captured when the event is raised.
        /// </summary>
        private void Send(IntentionInstance inst, IntentionEventType type, IntentionReason reason)
        {
            var data = new IntentionEventData(inst, type, reason, type == IntentionEventType.Performed);
            if (_dispatching)
            {
                _outbox.Enqueue((type, data));
                return;
            }

            _dispatching = true;
            try
            {
                inst.Intention.Raise(type, data);
                while (_outbox.Count > 0)
                {
                    var (queuedType, queuedData) = _outbox.Dequeue();
                    queuedData.Intention.Raise(queuedType, queuedData);
                }
            }
            finally
            {
                _dispatching = false;
                _outbox.Clear();
            }
        }

        private void SendPerformed(IntentionInstance inst)
        {
            Send(inst, IntentionEventType.Performed, IntentionReason.None);
            inst.ResetPending();
        }

        /// <summary>
        /// Finishes the instances collected in _scratch: <paramref name="reason"/> for those not started,
        /// <paramref name="activeReason"/> (default: the same) for active ones. Copies first, because the
        /// events can re-enter (a handler disabling an intention uses _scratch too).
        /// </summary>
        private void FinishCollected(IntentionReason reason, IntentionReason? activeReason = null)
        {
            if (_scratch.Count == 0) return;
            var items = _scratch.ToArray();
            _scratch.Clear();
            foreach (var inst in items)
                Finish(inst, inst.State == IntentionState.Active ? activeReason ?? reason : reason);
        }

        /// <summary>Rejects (if never started) or ends (if active) an instance. Idempotent.</summary>
        private void Finish(IntentionInstance inst, IntentionReason reason)
        {
            if (inst.State == IntentionState.Finished) return;
            var wasActive = inst.State == IntentionState.Active;
            inst.State = IntentionState.Finished;
            inst.WaitingOn = null;
            inst.Intention.ByGestureInstance.Remove(inst.GestureInstanceId);
            Send(inst, wasActive ? IntentionEventType.Ended : IntentionEventType.Rejected, reason);
        }

        // ---- Housekeeping -----------------------------------------------------------------------------

        private void EndOfUpdate()
        {
            _live.RemoveAll(i => i.State == IntentionState.Finished);
            foreach (var intention in _enabled)
                Prune(intention);
            foreach (var id in _endedFingers)
                _fingers.Remove(id);
            _endedFingers.Clear();
            foreach (var feed in _feeds.Values)
                feed.EndOfUpdate();
            _requests.Clear();
            _candidates.Clear();
        }

        private void Prune(Intention intention)
        {
            intention.InstanceList.RemoveAll(i => i.State == IntentionState.Finished);
            if (!_updating)
                _live.RemoveAll(i => i.State == IntentionState.Finished);
        }

        private void Release()
        {
            if (!IsRunning) return;
            foreach (var intention in _enabled.ToArray())
                if (intention.Registered)
                    Deactivate(intention, IntentionReason.Disabled);
            IsRunning = false;

            foreach (var feed in _feeds.Values)
                feed.Unsubscribe();
            _feeds.Clear();
            if (_updating)
                _clearAfterUpdate = true; // every instance has finished; Update skips them and clears up when it ends
            else
                ClearState();
        }

        private void ClearState()
        {
            _clearAfterUpdate = false;
            foreach (var e in _queue)
                if (e.Fingers != null) _fingerListPool.Push(e.Fingers);
            _queue.Clear();
            _fingers.Clear();
            _endedFingers.Clear();
            _live.Clear();
            _requests.Clear();
            _candidates.Clear();
        }

        private void RequireRunning()
        {
            if (!IsRunning)
                throw new InvalidOperationException("IntentionSystem is not running. Call Startup() first.");
        }

        // ---- Nested types ------------------------------------------------------------------------------

        private readonly struct QueuedEvent
        {
            public QueuedEvent(GestureFeed feed, GestureEventType type, GestureEventData data, List<int> fingers, bool disabled)
            {
                Feed = feed;
                Type = type;
                Data = data;
                Fingers = fingers;
                Disabled = disabled;
            }

            public readonly GestureFeed Feed;
            public readonly GestureEventType Type;
            public readonly GestureEventData Data;
            public readonly List<int> Fingers;
            public readonly bool Disabled;
        }

        /// <summary>A finger's touch-down position and its cached area answers.</summary>
        private sealed class FingerRecord
        {
            private readonly Dictionary<IIntentionArea, (bool accepted, object target)> _answers =
                new Dictionary<IIntentionArea, (bool, object)>();

            public FingerRecord(IntentionPoint down) => Down = down;

            public IntentionPoint Down { get; }

            public bool Accepts(IIntentionArea area, out object target)
            {
                if (!_answers.TryGetValue(area, out var answer))
                {
                    var accepted = area.Accepts(Down, out var t);
                    answer = (accepted, accepted ? t : null);
                    _answers.Add(area, answer);
                }
                target = answer.target;
                return answer.accepted;
            }
        }

        /// <summary>Subscribes to one gesture: queues its events and tracks its instances (for requirements).</summary>
        private sealed class GestureFeed
        {
            private readonly IntentionSystem _owner;
            private readonly Dictionary<int, GestureRecord> _records = new Dictionary<int, GestureRecord>();
            private readonly List<int> _finished = new List<int>();
            private readonly Action<GestureEventData> _possible, _fail, _enter, _stay, _exit;
            private readonly Action<IGesture> _disabled;

            /// <summary>A source's feed: no gesture to subscribe to; the source enqueues its own events.</summary>
            public GestureFeed(IntentionSource source, IntentionSourceKind kind)
            {
                Key = source;
                Kind = kind;
            }

            public GestureFeed(IntentionSystem owner, IGesture gesture)
            {
                _owner = owner;
                Gesture = gesture;
                Key = gesture;
                Kind = IntentionSourceKind.Gesture;
                _possible = d => _owner.Enqueue(this, GestureEventType.Possible, d);
                _fail = d => _owner.Enqueue(this, GestureEventType.Fail, d);
                _enter = d => _owner.Enqueue(this, GestureEventType.Enter, d);
                _stay = d => _owner.Enqueue(this, GestureEventType.Stay, d);
                _exit = d => _owner.Enqueue(this, GestureEventType.Exit, d);
                _disabled = g => _owner.EnqueueDisabled(this);
                gesture.Possible += _possible;
                gesture.Fail += _fail;
                gesture.Enter += _enter;
                gesture.Stay += _stay;
                gesture.Exit += _exit;
                gesture.OnDisabled += _disabled;
            }

            /// <summary>The gesture, or null for a source.</summary>
            public IGesture Gesture { get; }

            /// <summary>What intentions bind to: the gesture or the source.</summary>
            public object Key { get; }

            public IntentionSourceKind Kind { get; }

            public void Unsubscribe()
            {
                if (Gesture == null) return;
                Gesture.Possible -= _possible;
                Gesture.Fail -= _fail;
                Gesture.Enter -= _enter;
                Gesture.Stay -= _stay;
                Gesture.Exit -= _exit;
                Gesture.OnDisabled -= _disabled;
                _records.Clear();
            }

            public void Track(in QueuedEvent e)
            {
                if (e.Disabled)
                {
                    foreach (var record in _records.Values)
                        record.Finished = true; // can no longer enter
                    _finished.AddRange(_records.Keys);
                    return;
                }

                var id = e.Data.InstanceId;
                if (!_records.TryGetValue(id, out var r))
                {
                    if (e.Type != GestureEventType.Possible) return; // began before we subscribed
                    r = new GestureRecord();
                    _records.Add(id, r);
                }
                foreach (var finger in e.Fingers) r.Fingers.Add(finger);
                switch (e.Type)
                {
                    case GestureEventType.Enter:
                        r.Entered = true;
                        break;
                    case GestureEventType.Fail:
                    case GestureEventType.Exit:
                        r.Finished = true;
                        _finished.Add(id);
                        break;
                }
            }

            public void CollectOverlapping(HashSet<int> fingers, List<GestureRecord> into)
            {
                foreach (var r in _records.Values)
                    if (r.Fingers.Overlaps(fingers) && !(r.Finished && !r.Entered))
                        into.Add(r);
            }

            /// <summary>Active on one of the fingers now, or exited this update (the same update as the request).</summary>
            public bool AnyActiveOn(HashSet<int> fingers)
            {
                foreach (var r in _records.Values)
                    if (r.Entered && r.Fingers.Overlaps(fingers))
                        return true;
                return false;
            }

            public void EndOfUpdate()
            {
                foreach (var id in _finished)
                    _records.Remove(id);
                _finished.Clear();
            }
        }
    }

    /// <summary>What the intention system knows about one gesture instance (for requirements).</summary>
    internal sealed class GestureRecord
    {
        public readonly HashSet<int> Fingers = new HashSet<int>();
        public bool Entered;
        public bool Finished;
    }
}
