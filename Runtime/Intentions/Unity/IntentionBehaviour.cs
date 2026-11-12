using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Threads.TouchController.Intentions
{
    /// <summary>A UnityEvent carrying intention event data (wire it up in the inspector).</summary>
    [Serializable]
    public sealed class IntentionUnityEvent : UnityEvent<IntentionEventData>
    {
    }

    /// <summary>
    /// An <see cref="Intention"/> set up in the inspector, with UnityEvents. It applies to this GameObject:
    /// its area is this object's rectangle, UI or colliders (or everywhere). It is enabled while the component is.
    /// </summary>
    /// <remarks>
    /// <para>Give it an <see cref="IntentionRunner"/> (a serialized reference: no global lookup), or call
    /// <see cref="Attach"/> from your own bootstrap. The intention is rebuilt each time it is attached, so a
    /// restarted system is picked up.</para>
    /// <para>Gestures are the gesture assets of the lineup (e.g. <c>Drag.asset</c>); the running copies are found
    /// with <c>TouchSystem.TryGetRuntime</c>. A gesture that isn't in the lineup is reported as an error and the
    /// intention stays disabled.</para>
    /// </remarks>
    [AddComponentMenu("Threads/TouchController/Intention")]
    public sealed class IntentionBehaviour : MonoBehaviour
    {
        /// <summary>Where the intention applies, relative to this GameObject.</summary>
        public enum AreaKind
        {
            /// <summary>Anywhere on the screen.</summary>
            Everywhere = 0,

            /// <summary>Inside this object's RectTransform (UI), whatever covers it.</summary>
            RectTransform = 1,

            /// <summary>Where Unity UI's top-most hit is this object or inside it (needs the runner's EventSystem).</summary>
            UiRaycast = 2,

            /// <summary>Where a physics ray (3D and 2D) from <see cref="IntentionBehaviour.AreaCamera"/> hits a collider on this object or inside it.</summary>
            Physics = 3,
        }

        [Tooltip("The runner whose systems this intention uses. Empty: call Attach() from code.")]
        [SerializeField] private IntentionRunner runner;

        [Tooltip("For logs. Empty: the GameObject's name.")]
        [SerializeField] private string intentionName;

        [Tooltip("Gesture assets from the lineup that drive this intention (e.g. Drag).")]
        [SerializeField] private List<GestureBase> gestures = new List<GestureBase>();

        [Tooltip("Start only once these gestures have failed on the same fingers (e.g. DoubleTap, to wait for 'single tap confirmed').")]
        [SerializeField] private List<GestureBase> requireFailure = new List<GestureBase>();

        [Tooltip("Start only while these gestures are active on the same fingers (e.g. LongPress, for drag-after-hold).")]
        [SerializeField] private List<GestureBase> requireActive = new List<GestureBase>();

        [Tooltip("Higher wins when intentions compete for a finger.")]
        [SerializeField] private int priority;

        [SerializeField] private ClaimMode claim = ClaimMode.Shared;

        [SerializeField] private AreaKind area = AreaKind.Everywhere;

        [SerializeField] private AreaCheck areaCheck = AreaCheck.AtStart;

        [SerializeField] private FingerRule fingerRule = FingerRule.Any;

        [Tooltip("RectTransform: the canvas camera (empty for Screen Space - Overlay). Physics: the camera the scene is seen through.")]
        [SerializeField] private Camera areaCamera;

        [Tooltip("Physics: the layers to hit.")]
        [SerializeField] private LayerMask physicsLayers = PhysicsRaycastArea.DefaultLayers;

        [SerializeField] private IntentionUnityEvent onPossible = new IntentionUnityEvent();
        [SerializeField] private IntentionUnityEvent onRejected = new IntentionUnityEvent();
        [SerializeField] private IntentionUnityEvent onStarted = new IntentionUnityEvent();
        [SerializeField] private IntentionUnityEvent onPerformed = new IntentionUnityEvent();
        [SerializeField] private IntentionUnityEvent onEnded = new IntentionUnityEvent();

        private IntentionSystem _system;
        private EventSystem _eventSystem;

        /// <summary>The intention built from the inspector settings (null until attached).</summary>
        public Intention Intention { get; private set; }

        public IntentionRunner Runner
        {
            get => runner;
            set
            {
                if (runner == value) return;
                if (runner != null && isActiveAndEnabled) runner.Leave(this);
                runner = value;
                if (runner != null && isActiveAndEnabled) runner.Join(this);
            }
        }

        public List<GestureBase> Gestures => gestures;
        public List<GestureBase> RequireFailure => requireFailure;
        public List<GestureBase> RequireActive => requireActive;

        public int Priority
        {
            get => priority;
            set
            {
                priority = value;
                if (Intention != null) Intention.Priority = value;
            }
        }

        public ClaimMode Claim
        {
            get => claim;
            set
            {
                claim = value;
                if (Intention != null) Intention.Claim = value;
            }
        }

        public AreaKind Area
        {
            get => area;
            set => area = value;
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

        public Camera AreaCamera
        {
            get => areaCamera;
            set => areaCamera = value;
        }

        public LayerMask PhysicsLayers
        {
            get => physicsLayers;
            set => physicsLayers = value;
        }

        public IntentionUnityEvent OnPossible => onPossible;
        public IntentionUnityEvent OnRejected => onRejected;
        public IntentionUnityEvent OnStarted => onStarted;
        public IntentionUnityEvent OnPerformed => onPerformed;
        public IntentionUnityEvent OnEnded => onEnded;

        /// <summary>
        /// Builds the intention for <paramref name="system"/> and enables it while this component is enabled.
        /// Changes to the inspector settings (gestures, requirements, area) take effect at the next Attach.
        /// </summary>
        /// <param name="eventSystem">For <see cref="AreaKind.UiRaycast"/>.</param>
        public void Attach(IntentionSystem system, EventSystem eventSystem = null)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            Detach();
            _system = system;
            _eventSystem = eventSystem;
            Intention = Build();
            SyncEnabled();
        }

        /// <summary>Disables the intention (ending its instances) and forgets the system.</summary>
        public void Detach()
        {
            if (Intention != null)
            {
                Intention.Enabled = false;
                Intention.Possible -= RaisePossible;
                Intention.Rejected -= RaiseRejected;
                Intention.Started -= RaiseStarted;
                Intention.Performed -= RaisePerformed;
                Intention.Ended -= RaiseEnded;
            }
            Intention = null;
            _system = null;
            _eventSystem = null;
        }

        private void OnEnable()
        {
            if (runner != null) runner.Join(this);
            SyncEnabled();
        }

        private void OnDisable()
        {
            SyncEnabled(false);
            if (runner != null) runner.Leave(this);
        }

        private void OnDestroy() => Detach();

        private void SyncEnabled(bool? want = null)
        {
            if (Intention == null) return;
            var enable = want ?? (enabled && gameObject.activeInHierarchy);
            try
            {
                Intention.Enabled = enable;
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"Intention '{Intention.Name}' on '{name}' could not be enabled: {e.Message}", this);
            }
        }

        private Intention Build()
        {
            var intention = new Intention(string.IsNullOrEmpty(intentionName) ? name : intentionName, _system)
            {
                Priority = priority,
                Claim = claim,
                AreaCheck = areaCheck,
                FingerRule = fingerRule,
                Area = MakeArea(),
            };
            foreach (var gesture in gestures)
                if (gesture != null) intention.Bind(gesture);
            foreach (var gesture in requireFailure)
                if (gesture != null) intention.RequireFailure(gesture);
            foreach (var gesture in requireActive)
                if (gesture != null) intention.RequireActive(gesture);

            intention.Possible += RaisePossible;
            intention.Rejected += RaiseRejected;
            intention.Started += RaiseStarted;
            intention.Performed += RaisePerformed;
            intention.Ended += RaiseEnded;
            return intention;
        }

        private IIntentionArea MakeArea()
        {
            switch (area)
            {
                case AreaKind.RectTransform:
                    if (transform is RectTransform rect) return new RectTransformArea(rect, areaCamera);
                    Debug.LogError($"'{name}': area RectTransform needs a RectTransform on this GameObject.", this);
                    return Nowhere;

                case AreaKind.UiRaycast:
                    if (_eventSystem != null) return new UiRaycastArea(_eventSystem, transform);
                    Debug.LogError($"'{name}': area UiRaycast needs an EventSystem (set one on the runner, or pass it to Attach).", this);
                    return Nowhere;

                case AreaKind.Physics:
                    if (areaCamera == null)
                    {
                        Debug.LogError($"'{name}': area Physics needs an Area Camera.", this);
                        return Nowhere;
                    }
                    return new UnderTransform(new PhysicsRaycastArea(areaCamera, PhysicsWorld.Both, physicsLayers), transform);

                default:
                    return null;
            }
        }

        private static readonly IIntentionArea Nowhere = new FuncArea(_ => false);

        /// <summary>A world hit counts only if it is on <c>root</c> or inside it.</summary>
        private sealed class UnderTransform : IIntentionArea
        {
            private readonly IIntentionArea _area;
            private readonly Transform _root;

            public UnderTransform(IIntentionArea area, Transform root)
            {
                _area = area;
                _root = root;
            }

            public bool Accepts(in IntentionPoint point, out object target)
            {
                if (_root != null && _area.Accepts(point, out target) && target is WorldHit hit && hit.GameObject.transform.IsChildOf(_root))
                    return true;
                target = null;
                return false;
            }
        }

        private void RaisePossible(IntentionEventData e) => onPossible.Invoke(e);
        private void RaiseRejected(IntentionEventData e) => onRejected.Invoke(e);
        private void RaiseStarted(IntentionEventData e) => onStarted.Invoke(e);
        private void RaisePerformed(IntentionEventData e) => onPerformed.Invoke(e);
        private void RaiseEnded(IntentionEventData e) => onEnded.Invoke(e);
    }
}
