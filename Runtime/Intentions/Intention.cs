using System;
using System.Collections.Generic;
using UnityEngine;

namespace Threads.TouchController.Intentions
{
    /// <summary>
    /// What the user means: a user-defined mapping from gestures (in an optional screen area) to events.
    /// The equivalent of an Input System <c>InputAction</c>. Your code decides what action follows.
    /// </summary>
    /// <example>
    /// <code>
    /// var frameDrag = new Intention("Frame drag", intentions)
    ///     .Bind&lt;DragGesture&gt;()
    ///     .InArea(ScreenRectArea.Dp(frameRectDp))
    ///     .WithPriority(10)
    ///     .Exclusive();
    /// frameDrag.Performed += e =&gt; frame.MoveBy(e.DeltaPx);
    /// frameDrag.Enabled = true;   // registers with the IntentionSystem
    /// </code>
    /// </example>
    /// <remarks>
    /// <para>Lifecycle of each instance: <c>Possible → Rejected</c>, or
    /// <c>Possible → Started → Performed+ → Ended</c>. Started is followed by a Performed in the same
    /// update (unless a Started handler ends the intention); movement (<see cref="IntentionEventData.DeltaDp"/>) is only reported by Performed.</para>
    /// <para>Intentions start disabled. Enabling registers the intention with its system (if the system
    /// is not running yet, it joins at <see cref="IntentionSystem.Startup"/>); disabling ends its
    /// instances (<see cref="IntentionReason.Disabled"/>) and deregisters it. Bindings and requirements
    /// can only be changed while disabled; everything else can change at any time.</para>
    /// </remarks>
    [Serializable]
    public sealed class Intention
    {
        [SerializeField] private string name;
        [SerializeField] private int priority;
        [SerializeField] private ClaimMode claim;
        [SerializeField] private AreaCheck areaCheck;
        [SerializeField] private FingerRule fingerRule;

        [NonSerialized] private IntentionSystem _system;
        [NonSerialized] private bool _enabled;
        [NonSerialized] private IIntentionArea _area;
        [NonSerialized] private readonly List<GestureRef> _bindings = new List<GestureRef>();
        [NonSerialized] private readonly List<GestureRef> _requireFailure = new List<GestureRef>();
        [NonSerialized] private readonly List<GestureRef> _requireActive = new List<GestureRef>();
        [NonSerialized] internal readonly List<IntentionInstance> InstanceList = new List<IntentionInstance>();
        [NonSerialized] internal readonly Dictionary<int, IntentionInstance> ByGestureInstance = new Dictionary<int, IntentionInstance>();

        // Resolved while registered with a running system.
        [NonSerialized] private readonly List<IntentionSource> _sources = new List<IntentionSource>();
        [NonSerialized] internal readonly List<IGesture> BoundGestures = new List<IGesture>();
        [NonSerialized] internal readonly HashSet<object> BoundKeys = new HashSet<object>(); // gestures and sources
        internal IReadOnlyList<IntentionSource> BoundSources => _sources;
        [NonSerialized] internal readonly List<IGesture> RequiredToFail = new List<IGesture>();
        [NonSerialized] internal readonly List<IGesture> RequiredActive = new List<IGesture>();
        [NonSerialized] internal bool Registered;
        [NonSerialized] internal int Order;

        public Intention() : this("Intention")
        {
        }

        /// <param name="name">For logs and debugging.</param>
        /// <param name="system">The system it registers with when enabled. Can also be given later with <see cref="Attach"/>.</param>
        public Intention(string name, IntentionSystem system = null)
        {
            this.name = name;
            _system = system;
        }

        public string Name
        {
            get => name;
            set => name = value;
        }

        public IntentionSystem System => _system;

        /// <summary>Higher wins when intentions compete for a finger. Default 0.</summary>
        public int Priority
        {
            get => priority;
            set => priority = value;
        }

        public ClaimMode Claim
        {
            get => claim;
            set => claim = value;
        }

        public AreaCheck AreaCheck
        {
            get => areaCheck;
            set => areaCheck = value;
        }

        public FingerRule FingerRule
        {
            get => fingerRule;
            set => fingerRule = value;
        }

        /// <summary>Where the intention applies. Null (default): everywhere. Affects instances created after the change.</summary>
        public IIntentionArea Area
        {
            get => _area;
            set => _area = value;
        }

        /// <summary>Enabling registers the intention with its system; disabling ends its instances and deregisters it.</summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                if (_system == null)
                    throw new InvalidOperationException($"Intention '{name}' has no IntentionSystem. Pass one to the constructor or call Attach().");
                if (value)
                {
                    if (_bindings.Count == 0 && _sources.Count == 0)
                        throw new InvalidOperationException($"Intention '{name}' has no bindings. Call Bind<T>() (a gesture) or Bind(source) first.");
                    _system.Register(this); // may throw (e.g. a bound gesture is not in the lineup); stays disabled
                    _enabled = true;
                }
                else
                {
                    _enabled = false;
                    _system.Deregister(this);
                }
            }
        }

        /// <summary>True if any instance has started and not ended.</summary>
        public bool IsActive
        {
            get
            {
                foreach (var i in InstanceList)
                    if (i.State == IntentionState.Active) return true;
                return false;
            }
        }

        /// <summary>True if any instance is Possible or Waiting.</summary>
        public bool IsPossible
        {
            get
            {
                foreach (var i in InstanceList)
                    if (i.State == IntentionState.Possible || i.State == IntentionState.Waiting) return true;
                return false;
            }
        }

        /// <summary>Current instances. Finished ones are removed at the end of each update.</summary>
        public IReadOnlyList<IntentionInstance> Instances => InstanceList;

        // Each handler is called on its own (HandlerList), so one that throws doesn't stop the others.
        [NonSerialized] private readonly HandlerList<IntentionEventData>[] _handlers =
        {
            new HandlerList<IntentionEventData>(), new HandlerList<IntentionEventData>(), new HandlerList<IntentionEventData>(),
            new HandlerList<IntentionEventData>(), new HandlerList<IntentionEventData>(),
        };

        /// <summary>A candidate appeared: use for early feedback (a pressed look).</summary>
        public event Action<IntentionEventData> Possible
        {
            add => _handlers[(int)IntentionEventType.Possible].Add(value);
            remove => _handlers[(int)IntentionEventType.Possible].Remove(value);
        }

        /// <summary>A candidate ended without starting: undo early feedback.</summary>
        public event Action<IntentionEventData> Rejected
        {
            add => _handlers[(int)IntentionEventType.Rejected].Add(value);
            remove => _handlers[(int)IntentionEventType.Rejected].Remove(value);
        }

        /// <summary>The intention began (e.g. begin drag). Carries no movement: the Performed that follows does.</summary>
        public event Action<IntentionEventData> Started
        {
            add => _handlers[(int)IntentionEventType.Started].Add(value);
            remove => _handlers[(int)IntentionEventType.Started].Remove(value);
        }

        /// <summary>Every update while started, with the movement since the last one; exactly once for a discrete gesture (a tap).</summary>
        public event Action<IntentionEventData> Performed
        {
            add => _handlers[(int)IntentionEventType.Performed].Add(value);
            remove => _handlers[(int)IntentionEventType.Performed].Remove(value);
        }

        /// <summary>It is over: <see cref="IntentionReason.Completed"/>, Cancelled, Preempted, Disabled or LeftArea.</summary>
        public event Action<IntentionEventData> Ended
        {
            add => _handlers[(int)IntentionEventType.Ended].Add(value);
            remove => _handlers[(int)IntentionEventType.Ended].Remove(value);
        }

        /// <summary>Gives the intention its system (only while disabled).</summary>
        public Intention Attach(IntentionSystem system)
        {
            RequireDisabled();
            _system = system;
            return this;
        }

        // ---- Fluent setup --------------------------------------------------------------------

        /// <summary>Drive the intention with the running gesture of type <typeparamref name="T"/> (a base class covers its platform variants).</summary>
        public Intention Bind<T>() where T : class, IGesture => AddRef(_bindings, GestureRef.Of<T>());

        /// <summary>Drive the intention with the running copy of a gesture asset (or a running gesture).</summary>
        public Intention Bind(GestureBase gestureAsset) => AddRef(_bindings, GestureRef.Of(gestureAsset));

        /// <summary>Drive the intention with a running gesture.</summary>
        public Intention Bind(IGesture gesture) => AddRef(_bindings, GestureRef.Of(gesture));

        /// <summary>Drive the intention with a non-touch source too (mouse, gamepad, keyboard, network, tests): see <see cref="IntentionSource"/>.</summary>
        public Intention Bind(IntentionSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            RequireDisabled();
            if (!_sources.Contains(source)) _sources.Add(source);
            return this;
        }

        /// <summary>
        /// Start only once every instance of <typeparamref name="T"/> on the same fingers has failed
        /// (like iOS <c>require(toFail:)</c>). Until then the instance is <see cref="IntentionState.Waiting"/>;
        /// if one enters instead, it is rejected. E.g. a tap that waits for <c>DoubleTapGesture</c> to fail.
        /// </summary>
        public Intention RequireFailure<T>() where T : class, IGesture => AddRef(_requireFailure, GestureRef.Of<T>());

        /// <summary>Start only while a <typeparamref name="T"/> is active on the same fingers (e.g. drag after a long press).</summary>
        public Intention RequireActive<T>() where T : class, IGesture => AddRef(_requireActive, GestureRef.Of<T>());

        /// <summary><see cref="RequireFailure{T}"/> for the running copy of a gesture asset.</summary>
        public Intention RequireFailure(GestureBase gestureAsset) => AddRef(_requireFailure, GestureRef.Of(gestureAsset));

        /// <summary><see cref="RequireActive{T}"/> for the running copy of a gesture asset.</summary>
        public Intention RequireActive(GestureBase gestureAsset) => AddRef(_requireActive, GestureRef.Of(gestureAsset));

        public Intention InArea(IIntentionArea area, AreaCheck check = AreaCheck.AtStart)
        {
            _area = area;
            areaCheck = check;
            return this;
        }

        public Intention InArea(Func<IntentionPoint, bool> accepts, AreaCheck check = AreaCheck.AtStart) =>
            InArea(new FuncArea(accepts), check);

        public Intention WithPriority(int value)
        {
            priority = value;
            return this;
        }

        public Intention Exclusive()
        {
            claim = ClaimMode.Exclusive;
            return this;
        }

        public Intention Shared()
        {
            claim = ClaimMode.Shared;
            return this;
        }

        public Intention WithFingerRule(FingerRule rule)
        {
            fingerRule = rule;
            return this;
        }

        public override string ToString() => name;

        // ---- System side ---------------------------------------------------------------------

        /// <summary>Resolves the gesture references against a running touch system. Throws if one is missing.</summary>
        internal void Resolve(TouchSystem touch)
        {
            BoundGestures.Clear();
            RequiredToFail.Clear();
            RequiredActive.Clear();
            ResolveAll(_bindings, BoundGestures, touch);
            ResolveAll(_requireFailure, RequiredToFail, touch);
            ResolveAll(_requireActive, RequiredActive, touch);
            BoundKeys.Clear();
            foreach (var g in BoundGestures) BoundKeys.Add(g);
            foreach (var s in _sources) BoundKeys.Add(s);
        }

        private void ResolveAll(List<GestureRef> refs, List<IGesture> into, TouchSystem touch)
        {
            foreach (var r in refs)
            {
                var gesture = r.Resolve(touch);
                if (gesture == null)
                    throw new InvalidOperationException($"Intention '{name}': no running gesture {r} in the TouchSystem's lineup.");
                if (!into.Contains(gesture))
                    into.Add(gesture);
            }
        }

        internal void Raise(IntentionEventType type, in IntentionEventData data) => _handlers[(int)type].Invoke(data);

        private Intention AddRef(List<GestureRef> list, GestureRef r)
        {
            RequireDisabled();
            list.Add(r);
            return this;
        }

        private void RequireDisabled()
        {
            if (_enabled)
                throw new InvalidOperationException($"Intention '{name}' is enabled. Disable it before changing its bindings, requirements or system.");
        }

        /// <summary>A reference to a gesture, resolved when the intention registers with a running system.</summary>
        private readonly struct GestureRef
        {
            private readonly Func<TouchSystem, IGesture> _resolve;
            private readonly string _label;

            private GestureRef(Func<TouchSystem, IGesture> resolve, string label)
            {
                _resolve = resolve;
                _label = label;
            }

            public static GestureRef Of<T>() where T : class, IGesture =>
                new GestureRef(t => t.TryGet<T>(out var g) ? g : null, typeof(T).Name);

            public static GestureRef Of(GestureBase asset)
            {
                if (asset == null) throw new ArgumentNullException(nameof(asset));
                return new GestureRef(t => t.TryGetRuntime(asset, out var g) ? g : null, $"'{asset.name}'");
            }

            public static GestureRef Of(IGesture gesture)
            {
                if (gesture == null) throw new ArgumentNullException(nameof(gesture));
                return new GestureRef(t => gesture, $"'{gesture.Name}'");
            }

            public IGesture Resolve(TouchSystem touch) => _resolve(touch);

            public override string ToString() => _label;
        }
    }
}
