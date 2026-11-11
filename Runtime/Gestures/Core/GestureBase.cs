using System;
using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController
{
    /// <summary>
    /// Base of every gesture: a ScriptableObject asset holding the gesture's own settings, which at
    /// runtime runs the gesture state machine. Derive from <see cref="GestureBase{TInstance}"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>What the base does for you</b> (every update, before <see cref="OnFrame"/>):</para>
    /// <list type="bullet">
    /// <item>Only touches that <i>began</i> while the gesture was running are offered
    /// (<see cref="EligibleTouches"/>); touches already down at start-up or while disabled are ignored.</item>
    /// <item>Instance positions and velocities track the centroid / mean of their fingers.</item>
    /// <item>A finger the OS cancels (or that disappears) fails a Possible instance with
    /// <see cref="GestureReason.Cancelled"/> and exits an Active one with <see cref="GestureReason.Cancelled"/>.</item>
    /// <item>Fingers that lifted are removed from instances on the next update.</item>
    /// </list>
    /// <para>After <see cref="OnFrame"/> it sends Stay for every Active instance that did not enter this
    /// update, and removes finished instances.</para>
    /// <para><b>Event guarantees are enforced:</b> an illegal transition (e.g. Exit from Possible)
    /// throws. Transitions may only be made from <see cref="OnFrame"/>.</para>
    /// <para><b>Enable / disable</b> (plan.md rule 8): disabling sends only OnDisabled and silently
    /// discards every instance; enabling sends OnEnabled and waits until no fingers are on the screen.</para>
    /// <para>The shipped asset is never run: <see cref="TouchSystem"/> runs a runtime copy.</para>
    /// </remarks>
    public abstract class GestureBase : ScriptableObject, IGesture
    {
        [Tooltip("Where this gesture's behaviour comes from, and anything still to be checked on real devices.")]
        [SerializeField, TextArea(2, 20)] private string notes = "";

        private readonly List<GestureInstance> _instances = new List<GestureInstance>();
        private readonly HashSet<int> _eligible = new HashSet<int>();
        private readonly List<int> _endedTouches = new List<int>();
        private readonly List<TouchPoint> _eligibleTouches = new List<TouchPoint>();
        private readonly List<int> _singleFinger = new List<int>(1);

        private GestureContext _context;
        private GestureRunState _runState = GestureRunState.Disabled;
        private TouchFrame _frame;
        private bool _processing;
        private bool _discardPending;
        private bool _lastFrameAnyInContact; // fingers on the screen in the latest frame seen (even while disabled)
        private float _pixelsPerDp = 1f;
        private double _time;

        public event Action<GestureEventData> Possible;
        public event Action<GestureEventData> Fail;
        public event Action<GestureEventData> Enter;
        public event Action<GestureEventData> Stay;
        public event Action<GestureEventData> Exit;
        public event Action<IGesture> OnEnabled;
        public event Action<IGesture> OnDisabled;

        public string Name => name;

        /// <summary>Notes on the asset: sources, guesses, open questions.</summary>
        public string Notes => notes;

        /// <summary>True for runtime gestures created by a TouchSystem (or test harness); false for assets.</summary>
        public bool IsInitialised => _context != null;

        public GestureRunState RunState => _runState;

        public bool Enabled
        {
            get => _runState != GestureRunState.Disabled;
            set => SetEnabled(value);
        }

        public bool IsActive => Any(GestureState.Active);

        public bool IsPossible => Any(GestureState.Possible);

        public IReadOnlyList<GestureInstance> Instances => _instances;

        // ---- For gesture authors -----------------------------------------------------------------

        /// <summary>Shared settings, history and instance IDs.</summary>
        protected GestureContext Context =>
            _context ?? throw new InvalidOperationException($"Gesture '{name}' is not initialised. Only runtime copies created by a TouchSystem run.");

        /// <summary>The active settings (thresholds). Read every update; values can change on refresh.</summary>
        protected TouchSettingsData Settings => Context.Settings;

        protected TouchHistory History => Context.History;

        /// <summary>Time of the current update (seconds).</summary>
        protected double Time => _time;

        protected float PixelsPerDp => _pixelsPerDp;

        /// <summary>This update's touches that began while the gesture was running. Only these may join instances.</summary>
        protected IReadOnlyList<TouchPoint> EligibleTouches => _eligibleTouches;

        protected bool IsEligible(int touchId) => _eligible.Contains(touchId);

        /// <summary>
        /// True once at least <paramref name="milliseconds"/> have passed since <paramref name="since"/>
        /// (seconds). Tolerates floating-point rounding, so a threshold reached exactly by whole frames
        /// counts as reached. Use this for every time threshold.
        /// </summary>
        protected bool HasElapsed(double since, float milliseconds) =>
            (_time - since) * 1000.0 >= milliseconds - TimeToleranceMs;

        /// <summary>Tolerance used by <see cref="HasElapsed"/> (milliseconds).</summary>
        protected const double TimeToleranceMs = 1e-3;

        /// <summary>Called once when the runtime gesture is created, after <see cref="Context"/> is set.</summary>
        protected virtual void OnInitialise()
        {
        }

        /// <summary>Called every update while running. Create instances and make transitions here.</summary>
        protected abstract void OnFrame(TouchFrame frame);

        /// <summary>Called when all instances are discarded (on disable or enable). Reset any gesture-level state here.</summary>
        protected virtual void OnDiscard()
        {
        }

        private protected abstract GestureInstance CreateInstanceObject();

        /// <summary>Creates an instance for one finger and sends Possible.</summary>
        protected GestureInstance BeginPossible(int fingerId) => BeginPossible(fingerId, null);

        /// <summary>Creates an instance for several fingers (the first is primary) and sends Possible.</summary>
        protected GestureInstance BeginPossible(IReadOnlyList<int> fingerIds) => BeginPossible(fingerIds, null);

        /// <summary>
        /// Creates an instance for one finger, calls <paramref name="initialise"/> (to set values the
        /// Possible event should report), then sends Possible.
        /// </summary>
        protected GestureInstance BeginPossible(int fingerId, Action<GestureInstance> initialise)
        {
            _singleFinger.Clear();
            _singleFinger.Add(fingerId);
            return BeginPossible(_singleFinger, initialise);
        }

        /// <summary>
        /// Creates an instance for several fingers (the first is primary), calls <paramref name="initialise"/>
        /// (to set values the Possible event should report), then sends Possible.
        /// </summary>
        protected GestureInstance BeginPossible(IReadOnlyList<int> fingerIds, Action<GestureInstance> initialise)
        {
            RequireProcessing();
            if (fingerIds == null || fingerIds.Count == 0)
                throw new ArgumentException("An instance needs at least one finger.", nameof(fingerIds));

            var instance = CreateInstanceObject();
            instance.Id = Context.NextInstanceId();
            instance.Gesture = this;
            instance.State = GestureState.Possible;
            instance.StartTime = _time;

            var position = Vector2.zero;
            var velocity = Vector2.zero;
            foreach (var id in fingerIds)
            {
                if (!_eligible.Contains(id))
                    throw new ArgumentException($"Touch {id} is not eligible for '{name}' (it did not begin while the gesture was running).", nameof(fingerIds));
                if (!_frame.TryGet(id, out var touch))
                    throw new ArgumentException($"Touch {id} is not in this frame.", nameof(fingerIds));
                if (instance.FingerList.Contains(id))
                    throw new ArgumentException($"Touch {id} is listed twice.", nameof(fingerIds));
                instance.FingerList.Add(id);
                position += touch.positionDp;
                velocity += History.GetVelocityDps(id);
                if (instance.FingerList.Count == 1)
                {
                    instance.PrimaryFingerId = id;
                    instance.PrimaryInputTouchId = touch.inputTouchId;
                }
            }

            position /= fingerIds.Count;
            instance.StartPositionDp = position;
            instance.PositionDp = position;
            instance.LastEventPositionDp = position;
            instance.VelocityDps = velocity / fingerIds.Count;

            _instances.Add(instance);
            initialise?.Invoke(instance);
            instance.LastEventPositionDp = instance.PositionDp;
            instance.LastEventScale = instance.Scale;
            instance.LastEventAngleDeg = instance.AngleDeg;
            Raise(GestureEventType.Possible, instance, GestureReason.None);
            return instance;
        }

        /// <summary>Possible → Fail. <paramref name="reason"/> must say why (not None or Completed).</summary>
        protected void FailInstance(GestureInstance instance, GestureReason reason)
        {
            RequireTransition(instance, GestureState.Possible, nameof(FailInstance));
            if (reason == GestureReason.None || reason == GestureReason.Completed)
                throw new ArgumentException($"Fail needs a failure reason, not {reason}.", nameof(reason));
            instance.State = GestureState.Finished;
            Raise(GestureEventType.Fail, instance, reason);
        }

        /// <summary>Possible → Active (Enter).</summary>
        protected void EnterInstance(GestureInstance instance)
        {
            RequireTransition(instance, GestureState.Possible, nameof(EnterInstance));
            instance.State = GestureState.Active;
            instance.EnterTime = _time;
            instance.EnteredThisFrame = true;
            Raise(GestureEventType.Enter, instance, GestureReason.None);
        }

        /// <summary>Active → Exit, with <see cref="GestureReason.Completed"/> or <see cref="GestureReason.Cancelled"/>.</summary>
        protected void ExitInstance(GestureInstance instance, GestureReason reason = GestureReason.Completed)
        {
            RequireTransition(instance, GestureState.Active, nameof(ExitInstance));
            if (reason != GestureReason.Completed && reason != GestureReason.Cancelled)
                throw new ArgumentException($"Exit reason must be Completed or Cancelled, not {reason}.", nameof(reason));
            instance.State = GestureState.Finished;
            Raise(GestureEventType.Exit, instance, reason);
        }

        /// <summary>For discrete gestures: Enter and Exit(Completed) in the same update.</summary>
        protected void RecogniseInstance(GestureInstance instance)
        {
            EnterInstance(instance);
            ExitInstance(instance, GestureReason.Completed);
        }

        /// <summary>Adds an eligible finger in this frame to a live instance (e.g. a finger joining a pinch).</summary>
        protected void AddFinger(GestureInstance instance, int fingerId)
        {
            RequireLive(instance);
            RequireProcessing();
            if (!_eligible.Contains(fingerId) || !_frame.TryGet(fingerId, out _))
                throw new ArgumentException($"Touch {fingerId} is not an eligible touch in this frame.", nameof(fingerId));
            if (!instance.FingerList.Contains(fingerId))
                instance.FingerList.Add(fingerId);
        }

        protected bool RemoveFinger(GestureInstance instance, int fingerId)
        {
            RequireLive(instance);
            return instance.FingerList.Remove(fingerId);
        }

        /// <summary>Overrides the automatic centroid position for this update.</summary>
        protected void SetPosition(GestureInstance instance, Vector2 positionDp)
        {
            RequireOwn(instance);
            instance.PositionDp = positionDp;
        }

        protected void SetVelocity(GestureInstance instance, Vector2 velocityDps)
        {
            RequireOwn(instance);
            instance.VelocityDps = velocityDps;
        }

        protected void SetScale(GestureInstance instance, float scale)
        {
            RequireOwn(instance);
            instance.Scale = scale;
        }

        protected void SetAngle(GestureInstance instance, float angleDeg)
        {
            RequireOwn(instance);
            instance.AngleDeg = angleDeg;
        }

        protected void SetTapCount(GestureInstance instance, int tapCount)
        {
            RequireOwn(instance);
            instance.TapCount = tapCount;
        }

        protected void SetDirection(GestureInstance instance, Vector2 direction)
        {
            RequireOwn(instance);
            instance.Direction = direction;
        }

        protected void SetEdge(GestureInstance instance, ScreenEdge edge)
        {
            RequireOwn(instance);
            instance.Edge = edge;
        }

        // ---- Driven by TouchSystem (or GestureHarness) -------------------------------------------

        internal void Initialise(GestureContext context, bool enabled)
        {
            if (_context != null)
                throw new InvalidOperationException($"Gesture '{name}' is already initialised.");
            _context = context ?? throw new ArgumentNullException(nameof(context));
            // Initially Running (not WaitingForClear): touches already down are ignored anyway,
            // because only touches that begin while running are eligible.
            _runState = enabled ? GestureRunState.Running : GestureRunState.Disabled;
            OnInitialise();
        }

        internal void UpdateGesture(TouchFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (_context == null) throw new InvalidOperationException($"Gesture '{name}' is not initialised.");

            _time = frame.Time;
            _pixelsPerDp = frame.PixelsPerDp;
            _lastFrameAnyInContact = frame.AnyInContact;
            ForgetEndedTouches();

            if (_runState == GestureRunState.Disabled)
                return;
            if (_runState == GestureRunState.WaitingForClear)
            {
                if (frame.AnyInContact)
                    return;
                _runState = GestureRunState.Running; // screen clear: a new touch sequence can start
            }

            _frame = frame;
            _processing = true;
            try
            {
                CollectEligibleTouches(frame);
                TrackInstances(frame);
                if (_runState == GestureRunState.Running)
                    OnFrame(frame);
                if (_runState == GestureRunState.Running)
                    SendStays();
            }
            finally
            {
                _processing = false;
                _frame = null;
                if (_discardPending)
                {
                    _discardPending = false;
                    Discard();
                }
                else
                {
                    _instances.RemoveAll(i => i.State == GestureState.Finished);
                    foreach (var instance in _instances)
                        instance.EnteredThisFrame = false;
                }
            }
        }

        // ---- Internals ---------------------------------------------------------------------------

        private void SetEnabled(bool value)
        {
            if (_context == null)
                throw new InvalidOperationException($"Gesture '{name}' is not initialised. Enable or disable the runtime gesture from the TouchSystem (e.g. touch.Get<T>()), not the asset.");
            if (value == Enabled)
                return;

            if (_processing)
                _discardPending = true; // discard after OnFrame; events are suppressed meanwhile
            else
                Discard();

            // Enabling: a new touch sequence must start. If no finger is on the screen now, that is
            // already true and recognition starts at once; otherwise wait until the screen is clear.
            _runState = !value
                ? GestureRunState.Disabled
                : _lastFrameAnyInContact ? GestureRunState.WaitingForClear : GestureRunState.Running;
            var handler = value ? OnEnabled : OnDisabled;
            try
            {
                handler?.Invoke(this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void Discard()
        {
            _instances.Clear();
            _eligible.Clear();
            _endedTouches.Clear();
            OnDiscard();
        }

        private void ForgetEndedTouches()
        {
            foreach (var id in _endedTouches)
            {
                _eligible.Remove(id);
                foreach (var instance in _instances)
                    instance.FingerList.Remove(id);
            }
            _endedTouches.Clear();
        }

        private void CollectEligibleTouches(TouchFrame frame)
        {
            _eligibleTouches.Clear();
            foreach (var touch in frame.Touches)
            {
                if (touch.phase == TouchPointPhase.Began)
                    _eligible.Add(touch.touchId);
                if (!_eligible.Contains(touch.touchId))
                    continue;
                _eligibleTouches.Add(touch);
                if (touch.HasEnded)
                    _endedTouches.Add(touch.touchId);
            }
        }

        private void TrackInstances(TouchFrame frame)
        {
            for (var k = 0; k < _instances.Count; k++)
            {
                var instance = _instances[k];
                if (instance.State == GestureState.Finished)
                    continue;

                var lost = false;
                var position = Vector2.zero;
                var velocity = Vector2.zero;
                var count = 0;
                foreach (var id in instance.FingerList)
                {
                    if (!frame.TryGet(id, out var touch) || touch.phase == TouchPointPhase.Canceled)
                    {
                        lost = true;
                        continue;
                    }
                    position += touch.positionDp;
                    velocity += History.GetVelocityDps(id);
                    count++;
                    if (id == instance.PrimaryFingerId)
                        instance.PrimaryInputTouchId = touch.inputTouchId;
                }

                if (count > 0)
                {
                    instance.PositionDp = position / count;
                    instance.VelocityDps = velocity / count;
                }

                if (!lost)
                    continue;
                if (instance.State == GestureState.Possible)
                    FailInstance(instance, GestureReason.Cancelled);
                else
                    ExitInstance(instance, GestureReason.Cancelled);
            }
        }

        private void SendStays()
        {
            for (var k = 0; k < _instances.Count; k++)
            {
                var instance = _instances[k];
                if (instance.State == GestureState.Active && !instance.EnteredThisFrame)
                    Raise(GestureEventType.Stay, instance, GestureReason.None);
            }
        }

        private void Raise(GestureEventType type, GestureInstance instance, GestureReason reason)
        {
            if (_discardPending || _runState != GestureRunState.Running)
                return; // disabled: silent

            Vector2 delta;
            float scaleDelta, angleDelta;
            if (type == GestureEventType.Possible)
            {
                delta = Vector2.zero;
                scaleDelta = 1f;
                angleDelta = 0f;
            }
            else
            {
                delta = instance.PositionDp - instance.LastEventPositionDp;
                scaleDelta = instance.LastEventScale > 0f ? instance.Scale / instance.LastEventScale : 1f;
                angleDelta = Mathf.DeltaAngle(instance.LastEventAngleDeg, instance.AngleDeg);
            }
            instance.LastEventPositionDp = instance.PositionDp;
            instance.LastEventScale = instance.Scale;
            instance.LastEventAngleDeg = instance.AngleDeg;

            var handler = type switch
            {
                GestureEventType.Possible => Possible,
                GestureEventType.Fail => Fail,
                GestureEventType.Enter => Enter,
                GestureEventType.Stay => Stay,
                _ => Exit,
            };
            if (handler == null)
                return;

            var data = new GestureEventData(this, instance, type, reason, _time, _pixelsPerDp, delta, scaleDelta, angleDelta);
            try
            {
                handler(data);
            }
            catch (Exception e)
            {
                // A failing consumer must not break recognition for everyone else.
                Debug.LogException(e, this);
            }
        }

        private bool Any(GestureState state)
        {
            foreach (var instance in _instances)
                if (instance.State == state) return true;
            return false;
        }

        private void RequireProcessing()
        {
            if (!_processing)
                throw new InvalidOperationException("Gesture transitions can only be made from OnFrame.");
        }

        private void RequireOwn(GestureInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (!ReferenceEquals(instance.Gesture, this))
                throw new ArgumentException($"Instance #{instance.Id} does not belong to '{name}'.", nameof(instance));
        }

        private void RequireLive(GestureInstance instance)
        {
            RequireOwn(instance);
            if (instance.State == GestureState.Finished)
                throw new InvalidOperationException($"Instance #{instance.Id} has finished.");
        }

        private void RequireTransition(GestureInstance instance, GestureState from, string operation)
        {
            RequireProcessing();
            RequireOwn(instance);
            if (instance.State != from)
                throw new InvalidOperationException($"{operation} is only allowed from {from}; instance #{instance.Id} of '{name}' is {instance.State}.");
        }
    }

    /// <summary>
    /// Base class for gestures. <typeparamref name="TInstance"/> holds per-instance data; use
    /// <see cref="GestureInstance"/> itself when no extra data is needed.
    /// </summary>
    public abstract class GestureBase<TInstance> : GestureBase where TInstance : GestureInstance, new()
    {
        private readonly List<TInstance> _live = new List<TInstance>();

        private protected sealed override GestureInstance CreateInstanceObject() => new TInstance();

        /// <inheritdoc cref="GestureBase.BeginPossible(int)"/>
        protected new TInstance BeginPossible(int fingerId) => (TInstance)base.BeginPossible(fingerId);

        /// <inheritdoc cref="GestureBase.BeginPossible(IReadOnlyList{int})"/>
        protected new TInstance BeginPossible(IReadOnlyList<int> fingerIds) => (TInstance)base.BeginPossible(fingerIds);

        /// <inheritdoc cref="GestureBase.BeginPossible(int, Action{GestureInstance})"/>
        protected TInstance BeginPossible(int fingerId, Action<TInstance> initialise) =>
            (TInstance)base.BeginPossible(fingerId, initialise == null ? null : (Action<GestureInstance>)(i => initialise((TInstance)i)));

        /// <inheritdoc cref="GestureBase.BeginPossible(IReadOnlyList{int}, Action{GestureInstance})"/>
        protected TInstance BeginPossible(IReadOnlyList<int> fingerIds, Action<TInstance> initialise) =>
            (TInstance)base.BeginPossible(fingerIds, initialise == null ? null : (Action<GestureInstance>)(i => initialise((TInstance)i)));

        /// <summary>
        /// Snapshot of the instances that have not finished, safe to iterate while making transitions.
        /// The list is reused: don't hold it across calls.
        /// </summary>
        protected IReadOnlyList<TInstance> LiveInstances()
        {
            _live.Clear();
            foreach (var instance in Instances)
                if (instance.State != GestureState.Finished)
                    _live.Add((TInstance)instance);
            return _live;
        }
    }
}
