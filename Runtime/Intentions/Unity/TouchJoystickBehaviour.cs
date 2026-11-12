using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Threads.TouchController.Intentions
{
    /// <summary>A UnityEvent carrying a joystick value.</summary>
    [Serializable]
    public sealed class JoystickUnityEvent : UnityEvent<Vector2>
    {
    }

    /// <summary>
    /// A <see cref="TouchJoystick"/> set up in the inspector, on a UI RectTransform. With a fixed joystick the
    /// RectTransform is the base (its centre is the origin). With a floating one it is the activation zone (e.g. the
    /// left half of the screen), and the base appears where a finger touches down inside it.
    /// </summary>
    /// <remarks>
    /// <para>Outputs: <see cref="Joystick"/> (<see cref="TouchJoystick.Value"/> etc.), UnityEvents, and optionally a
    /// stick of the runner's <see cref="TouchSticksDevice"/> (enable "Create Touch Sticks" on the runner), so Input
    /// System actions bound to <c>&lt;TouchSticks&gt;/leftStick</c> read it.</para>
    /// <para>Visuals are optional: a base and a knob RectTransform, moved to <see cref="TouchJoystick.OriginDp"/> and
    /// <see cref="TouchJoystick.ThumbDp"/>. Size them to match the radius.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Threads/TouchController/Touch Joystick")]
    public sealed class TouchJoystickBehaviour : MonoBehaviour, IIntentionRunnerMember
    {
        /// <summary>Which stick of the runner's <see cref="TouchSticksDevice"/> to write to.</summary>
        public enum StickOutput
        {
            None = 0,
            LeftStick = 1,
            RightStick = 2,
        }

        [Tooltip("The runner whose systems this joystick uses. Empty: call Attach() from code.")]
        [SerializeField] private IntentionRunner runner;

        [Tooltip("Fixed: this RectTransform is the base. Floating: this RectTransform is where a touch makes the joystick appear.")]
        [SerializeField] private JoystickMode mode = JoystickMode.Fixed;

        [Tooltip("Full deflection this far from the centre (dp).")]
        [SerializeField] private float radiusDp = 60f;

        [Tooltip("Fixed: how far outside the base a touch still grabs it (dp).")]
        [SerializeField] private float touchMarginDp = 24f;

        [Tooltip("Deflections below this fraction of the radius read as zero.")]
        [Range(0f, 0.95f)]
        [SerializeField] private float deadZone = 0.1f;

        [Tooltip("1 = linear; 2 = finer control near the centre.")]
        [SerializeField] private float responseExponent = 1f;

        [Tooltip("Floating: the base follows the finger past the radius.")]
        [SerializeField] private bool follow;

        [SerializeField] private int priority = 1000;

        [Tooltip("Write the value to this stick of the runner's TouchSticks device (needs 'Create Touch Sticks' on the runner).")]
        [SerializeField] private StickOutput output = StickOutput.LeftStick;

        [Tooltip("The canvas's camera; empty for a Screen Space - Overlay canvas.")]
        [SerializeField] private Camera uiCamera;

        [Tooltip("Optional: moved to the joystick's origin.")]
        [SerializeField] private RectTransform baseGraphic;

        [Tooltip("Optional: moved to the finger, within the radius.")]
        [SerializeField] private RectTransform knob;

        [Tooltip("Floating: hide the base (and knob) while not held.")]
        [SerializeField] private bool hideWhenReleased = true;

        [SerializeField] private JoystickUnityEvent onValueChanged = new JoystickUnityEvent();
        [SerializeField] private UnityEvent onPressed = new UnityEvent();
        [SerializeField] private UnityEvent onReleased = new UnityEvent();

        private IntentionSystem _system;
        private JoystickInputOutput _output;

        /// <summary>The joystick (null until attached).</summary>
        public TouchJoystick Joystick { get; private set; }

        /// <summary>The current value (zero when not attached or not held).</summary>
        public Vector2 Value => Joystick?.Value ?? Vector2.zero;

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

        public JoystickMode Mode { get => mode; set => mode = value; }
        public float RadiusDp { get => radiusDp; set => radiusDp = value; }
        public float TouchMarginDp { get => touchMarginDp; set => touchMarginDp = value; }
        public float DeadZone { get => deadZone; set => deadZone = value; }
        public float ResponseExponent { get => responseExponent; set => responseExponent = value; }
        public bool Follow { get => follow; set => follow = value; }
        public int Priority { get => priority; set => priority = value; }
        public StickOutput Output { get => output; set => output = value; }
        public Camera UiCamera { get => uiCamera; set => uiCamera = value; }
        public RectTransform BaseGraphic { get => baseGraphic; set => baseGraphic = value; }
        public RectTransform Knob { get => knob; set => knob = value; }
        public bool HideWhenReleased { get => hideWhenReleased; set => hideWhenReleased = value; }
        public JoystickUnityEvent OnValueChanged => onValueChanged;
        public UnityEvent OnPressed => onPressed;
        public UnityEvent OnReleased => onReleased;

        /// <summary>
        /// Builds the joystick for <paramref name="system"/> and enables it while this component is enabled.
        /// Settings changes take effect at the next Attach. The output stick is taken from the runner, or from
        /// <paramref name="touchSticks"/> when attaching from code.
        /// </summary>
        public void Attach(IntentionSystem system, TouchSticksDevice touchSticks)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            Detach();
            _system = system;
            try
            {
                Joystick = new TouchJoystick(system, Options());
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"Touch joystick '{name}' could not be set up: {e.Message}", this);
                _system = null;
                return;
            }
            Joystick.Pressed += OnJoystickPressed;
            Joystick.Changed += OnJoystickChanged;
            Joystick.Released += OnJoystickReleased;

            var stick = output switch
            {
                StickOutput.LeftStick => touchSticks?.LeftStick,
                StickOutput.RightStick => touchSticks?.RightStick,
                _ => null,
            };
            if (stick != null)
                _output = new JoystickInputOutput(Joystick, touchSticks, stick);
            else if (output != StickOutput.None)
                Debug.LogWarning($"Touch joystick '{name}': no TouchSticks device to write to (enable 'Create Touch Sticks' on the runner, or set Output to None).", this);

            UpdateVisuals();
            SyncEnabled();
        }

        void IIntentionRunnerMember.Attach(IntentionSystem system, EventSystem eventSystem) =>
            Attach(system, runner != null ? runner.TouchSticks : null);

        /// <summary>Disables the joystick (releasing it), stops the output and forgets the system.</summary>
        public void Detach()
        {
            _output?.Dispose();
            _output = null;
            if (Joystick != null)
            {
                Joystick.Pressed -= OnJoystickPressed;
                Joystick.Changed -= OnJoystickChanged;
                Joystick.Released -= OnJoystickReleased;
                Joystick.Dispose();
            }
            Joystick = null;
            _system = null;
        }

        private void OnEnable()
        {
            if (runner != null) runner.Join(this);
            SyncEnabled();
            UpdateVisuals();
        }

        private void OnDisable()
        {
            SyncEnabled(false);
            if (runner != null) runner.Leave(this);
        }

        private void OnDestroy() => Detach();

        private void SyncEnabled(bool? want = null)
        {
            if (Joystick == null) return;
            try
            {
                Joystick.Enabled = want ?? (enabled && gameObject.activeInHierarchy);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"Touch joystick '{name}' could not be enabled: {e.Message}", this);
            }
        }

        private TouchJoystickOptions Options()
        {
            var rect = (RectTransform)transform;
            var options = new TouchJoystickOptions
            {
                Name = name,
                Mode = mode,
                RadiusDp = radiusDp,
                TouchMarginDp = touchMarginDp,
                DeadZone = deadZone,
                ResponseExponent = responseExponent,
                Follow = follow,
                Priority = priority,
            };
            if (mode == JoystickMode.Fixed)
                options.CentreDp = () => RectDp().center;
            else
            {
                options.ActivationArea = new RectTransformArea(rect, uiCamera);
                options.OriginBoundsDp = RectDp;
            }
            return options;
        }

        /// <summary>This RectTransform on the screen, in dp.</summary>
        private Rect RectDp()
        {
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            var ppd = PixelsPerDp;
            var min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]) / ppd;
            var max = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]) / ppd;
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private float PixelsPerDp => _system != null ? _system.PixelsPerDp : 1f;

        private void OnJoystickPressed(TouchJoystick joystick)
        {
            UpdateVisuals();
            onPressed.Invoke();
        }

        private void OnJoystickChanged(Vector2 value)
        {
            UpdateVisuals();
            onValueChanged.Invoke(value);
        }

        private void OnJoystickReleased(TouchJoystick joystick)
        {
            UpdateVisuals();
            onReleased.Invoke();
        }

        private void UpdateVisuals()
        {
            if (Joystick == null) return;
            var held = Joystick.IsHeld;
            var visible = held || mode == JoystickMode.Fixed || !hideWhenReleased;
            if (baseGraphic != null)
            {
                if (baseGraphic.gameObject.activeSelf != visible) baseGraphic.gameObject.SetActive(visible);
                if (visible && (held || mode == JoystickMode.Floating)) Place(baseGraphic, Joystick.OriginDp);
            }
            if (knob != null)
            {
                if (knob.gameObject.activeSelf != visible) knob.gameObject.SetActive(visible);
                if (visible) Place(knob, held ? Joystick.ThumbDp : Joystick.OriginDp);
            }
        }

        private void Place(RectTransform graphic, Vector2 dp)
        {
            if (mode == JoystickMode.Fixed && !Joystick.IsHeld && graphic == baseGraphic) return; // stays where it was laid out
            var parent = graphic.parent as RectTransform;
            if (parent == null) return;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, dp * PixelsPerDp, uiCamera, out var world))
                graphic.position = world;
        }
    }
}
