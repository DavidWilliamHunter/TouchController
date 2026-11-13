using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Threads.TouchController.Intentions
{
    /// <summary>
    /// A ready-made bootstrap for scenes: owns a <see cref="TouchSystem"/> and an <see cref="IntentionSystem"/>,
    /// starts them in OnEnable, updates them every frame (touch first, then intentions) and shuts them down in
    /// OnDisable. <see cref="IntentionBehaviour"/>s that name this runner attach to it.
    /// </summary>
    /// <remarks>
    /// Not a singleton: there can be several, and nothing finds it globally. Components reach it through a
    /// serialized reference. For full control (or a DI container), skip this component, create the systems in
    /// your own code and call <see cref="IntentionBehaviour.Attach"/>.
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // events are sent from Update: before the scripts that read the results
    [AddComponentMenu("Threads/TouchController/Intention Runner")]
    public sealed class IntentionRunner : MonoBehaviour
    {
        [Tooltip("Optional. Empty: the shipped default settings for the platform.")]
        [SerializeField] private TouchSettings settings;

        [Tooltip("Use the conventions of a specific platform instead of the one the app runs on.")]
        [SerializeField] private bool overridePlatform;

        [SerializeField] private TouchPlatform platform = TouchPlatform.Android;

        [Tooltip("In the Editor, turn the mouse into touches.")]
        [SerializeField] private bool simulateTouchInEditor = true;

        [Tooltip("Optional. Its drag threshold is kept equal to the touch slop (EventSystemSync), and UI areas of attached intentions use it.")]
        [SerializeField] private EventSystem eventSystem;

        [Tooltip("Add a TouchSticks Input System device while running, for TouchJoystickBehaviours to write to (bind actions to <TouchSticks>/leftStick).")]
        [SerializeField] private bool createTouchSticks;

        [Tooltip("Add a TouchGestures Input System device while running, fed with the gestures below, so actions can be bound to gestures (<TouchGestures>/swipeLeft, /tap, /pinch...).")]
        [SerializeField] private bool createTouchGestures;

        [Tooltip("The gestures fed to the TouchGestures device.")]
        [SerializeField] private TouchGestureKinds touchGestureKinds = TouchGestureKinds.All;

        [Tooltip("Gestures that start on UI (a raycast hit of the EventSystem) don't reach the TouchGestures device. Needs the EventSystem.")]
        [SerializeField] private bool touchGesturesIgnoreUi = true;

        [Tooltip("A tap waits until a double tap is no longer possible (the second tap of a double tap is then not a tap).")]
        [SerializeField] private bool tapWaitsForDoubleTap;

        private readonly List<IIntentionRunnerMember> _members = new List<IIntentionRunnerMember>();

        /// <summary>Settings asset (null: the platform's default). Takes effect the next time the runner is enabled.</summary>
        public TouchSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>A platform whose conventions to use instead of the running one (null: the running one). Takes effect when next enabled.</summary>
        public TouchPlatform? PlatformOverride
        {
            get => overridePlatform ? platform : (TouchPlatform?)null;
            set
            {
                overridePlatform = value.HasValue;
                if (value.HasValue) platform = value.Value;
            }
        }

        /// <summary>In the Editor, turn the mouse into touches. Takes effect when next enabled.</summary>
        public bool SimulateTouchInEditor
        {
            get => simulateTouchInEditor;
            set => simulateTouchInEditor = value;
        }

        public TouchSystem Touch { get; private set; }

        public IntentionSystem Intentions { get; private set; }

        /// <summary>Optional EventSystem (drag threshold sync, UI areas). Takes effect when next enabled.</summary>
        public EventSystem EventSystem
        {
            get => eventSystem;
            set => eventSystem = value;
        }

        /// <summary>Add a <see cref="TouchSticksDevice"/> while running. Takes effect when next enabled.</summary>
        public bool CreateTouchSticks
        {
            get => createTouchSticks;
            set => createTouchSticks = value;
        }

        /// <summary>The Input System device for touch joysticks, while running with <see cref="CreateTouchSticks"/>; otherwise null.</summary>
        public TouchSticksDevice TouchSticks { get; private set; }

        /// <summary>Add a <see cref="TouchGesturesDevice"/> fed by a <see cref="GestureInputOutput"/> while running. Takes effect when next enabled.</summary>
        public bool CreateTouchGestures
        {
            get => createTouchGestures;
            set => createTouchGestures = value;
        }

        /// <summary>The gestures fed to the <see cref="TouchGestures"/> device. Takes effect when next enabled.</summary>
        public TouchGestureKinds TouchGestureKinds
        {
            get => touchGestureKinds;
            set => touchGestureKinds = value;
        }

        /// <summary>Gestures that start on UI don't reach the <see cref="TouchGestures"/> device (needs <see cref="EventSystem"/>). Takes effect when next enabled.</summary>
        public bool TouchGesturesIgnoreUi
        {
            get => touchGesturesIgnoreUi;
            set => touchGesturesIgnoreUi = value;
        }

        /// <summary>See <see cref="GestureInputOptions.TapWaitsForDoubleTap"/>. Takes effect when next enabled.</summary>
        public bool TapWaitsForDoubleTap
        {
            get => tapWaitsForDoubleTap;
            set => tapWaitsForDoubleTap = value;
        }

        /// <summary>The Input System device for gestures, while running with <see cref="CreateTouchGestures"/>; otherwise null.</summary>
        public TouchGesturesDevice TouchGestures { get; private set; }

        /// <summary>What feeds <see cref="TouchGestures"/>, while running with <see cref="CreateTouchGestures"/>; otherwise null.</summary>
        public GestureInputOutput GestureInput { get; private set; }

        public bool IsRunning => Intentions != null && Intentions.IsRunning;

        private void OnEnable()
        {
            Touch = new TouchSystem(new TouchSystemOptions
            {
                Settings = settings,
                Platform = overridePlatform ? platform : (TouchPlatform?)null,
                SimulateTouchInEditor = simulateTouchInEditor,
            });
            Touch.Startup();
            Intentions = new IntentionSystem(Touch);
            Intentions.Startup();
            if (createTouchSticks)
                TouchSticks = new TouchSticksDevice();
            if (createTouchGestures)
            {
                TouchGestures = new TouchGesturesDevice();
                GestureInput = new GestureInputOutput(Intentions, TouchGestures, new GestureInputOptions
                {
                    Name = name + " gestures",
                    Gestures = touchGestureKinds,
                    Area = touchGesturesIgnoreUi && eventSystem != null ? Area.Not(new UiRaycastArea(eventSystem)) : null,
                    TapWaitsForDoubleTap = tapWaitsForDoubleTap,
                }) { Enabled = true };
            }
            if (eventSystem != null)
                EventSystemSync.Apply(eventSystem, Touch);
            foreach (var member in _members.ToArray())
                member.Attach(Intentions, eventSystem);
        }

        private void Update()
        {
            Touch.Update();
            Intentions.Update();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // The user may have changed a touch setting (accessibility) while away.
            if (!hasFocus || !IsRunning) return;
            Touch.Refresh();
            if (eventSystem != null)
                EventSystemSync.Apply(eventSystem, Touch);
        }

        private void OnDisable()
        {
            foreach (var member in _members.ToArray())
                member.Detach();
            GestureInput?.Dispose();
            GestureInput = null;
            Intentions?.Shutdown();
            Touch?.Shutdown();
            TouchSticks?.Dispose();
            TouchSticks = null;
            TouchGestures?.Dispose();
            TouchGestures = null;
            Intentions = null;
            Touch = null;
        }

        /// <summary>Called by a component that names this runner; it is attached now if running, or when the runner starts.</summary>
        internal void Join(IIntentionRunnerMember member)
        {
            if (_members.Contains(member)) return;
            _members.Add(member);
            if (IsRunning) member.Attach(Intentions, eventSystem);
        }

        internal void Leave(IIntentionRunnerMember member)
        {
            _members.Remove(member);
            member.Detach();
        }
    }

    /// <summary>A component attached to an <see cref="IntentionRunner"/> (<see cref="IntentionBehaviour"/>, <see cref="TouchJoystickBehaviour"/>).</summary>
    internal interface IIntentionRunnerMember
    {
        void Attach(IntentionSystem system, EventSystem eventSystem);

        void Detach();
    }
}
