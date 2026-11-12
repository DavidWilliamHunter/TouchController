using Threads.TouchController.Intentions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Threads.TouchController.Demo
{
    /// <summary>
    /// Touch joysticks demo: a floating joystick on the left half of the screen moves a ship, and a fixed one at
    /// the bottom right turns it. The ship is driven <b>only through Input System actions</b>
    /// (<c>&lt;Gamepad&gt;/leftStick</c> + <c>&lt;TouchSticks&gt;/leftStick</c>, and the right sticks), so the
    /// same code works with a gamepad; the touch joysticks feed the runner's <see cref="TouchSticksDevice"/>.
    /// </summary>
    /// <remarks>
    /// Built at runtime from components, as a designer would set them up in the inspector: an
    /// <see cref="IntentionRunner"/> with "Create Touch Sticks" on, and two <see cref="TouchJoystickBehaviour"/>s
    /// with a base and a knob. The canvas is scaled so one UI unit is one dp.
    /// </remarks>
    public sealed class JoystickDemo : MonoBehaviour
    {
        [Tooltip("In the Editor, turn the mouse into touches.")]
        [SerializeField] private bool simulateTouchInEditor = true;

        [Tooltip("Ship speed, in dp per second at full deflection.")]
        [SerializeField] private float speedDps = 400f;

        [Tooltip("Turn rate, in degrees per second at full deflection.")]
        [SerializeField] private float turnDegreesPerSecond = 270f;

        private static readonly Color Background = new Color(0.11f, 0.12f, 0.15f);
        private static readonly Color BaseColour = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color KnobColour = new Color(1f, 1f, 1f, 0.55f);

        private GameObject _root;
        private IntentionRunner _runner;
        private RectTransform _canvasRect;
        private RectTransform _ship;
        private Text _readout;
        private InputAction _move;
        private InputAction _turn;
        private Sprite _circle;
        private Font _font;

        private void OnEnable()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _circle = MakeCircleSprite(128);
            Build();

            // The game's own input: it knows nothing about touch.
            _move = new InputAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
            _move.AddBinding("<TouchSticks>/leftStick");
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _turn = new InputAction("Turn", InputActionType.Value, "<Gamepad>/rightStick");
            _turn.AddBinding("<TouchSticks>/rightStick");
            _move.Enable();
            _turn.Enable();
        }

        private void OnDisable()
        {
            _move?.Dispose();
            _turn?.Dispose();
            _move = _turn = null;
            if (_root != null) Destroy(_root);
            if (_circle != null)
            {
                Destroy(_circle.texture);
                Destroy(_circle);
            }
        }

        private void Update()
        {
            if (_move == null || _canvasRect == null) return;
            var move = _move.ReadValue<Vector2>();
            var turn = _turn.ReadValue<Vector2>();

            var half = _canvasRect.rect.size / 2f;
            var p = _ship.anchoredPosition + move * (speedDps * Time.deltaTime);
            _ship.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -half.x, half.x), Mathf.Clamp(p.y, -half.y, half.y));
            _ship.localRotation *= Quaternion.Euler(0f, 0f, -turn.x * turnDegreesPerSecond * Time.deltaTime);

            _readout.text = $"Move (action) {move.x:+0.00;-0.00} {move.y:+0.00;-0.00}    Turn (action) {turn.x:+0.00;-0.00}";
        }

        // ---- Scene (built at runtime so it needs no assets) -------------------------------------------------

        private void Build()
        {
            _root = new GameObject("Joystick Demo");
            _root.SetActive(false); // configure everything, then start the runner and joysticks together

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemGo.transform.SetParent(_root.transform, false);

            var runnerGo = new GameObject("Intention Runner");
            runnerGo.transform.SetParent(_root.transform, false);
            _runner = runnerGo.AddComponent<IntentionRunner>();
            _runner.SimulateTouchInEditor = simulateTouchInEditor;
            _runner.EventSystem = eventSystemGo.GetComponent<EventSystem>();
            _runner.CreateTouchSticks = true;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(_root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasRect = (RectTransform)canvasGo.transform;

            var background = Stretch(_canvasRect, "Background", typeof(Image));
            background.GetComponent<Image>().color = Background;
            background.GetComponent<Image>().raycastTarget = false;

            _ship = Graphic(_canvasRect, "Ship", new Vector2(36, 48), new Color(0.95f, 0.7f, 0.3f), null);
            var nose = Graphic(_ship, "Nose", new Vector2(14, 14), new Color(1f, 0.95f, 0.8f), _circle);
            nose.anchoredPosition = new Vector2(0, 22);

            // Left half: a floating joystick that appears under the thumb.
            var moveZone = Stretch(_canvasRect, "Move zone");
            moveZone.anchorMax = new Vector2(0.5f, 1f);
            Joystick(moveZone, JoystickMode.Floating, TouchJoystickBehaviour.StickOutput.LeftStick);
            Label(moveZone, "Touch anywhere here: move", new Vector2(0.5f, 0.5f), new Vector2(0, 0));

            // Bottom right: a fixed joystick.
            var turnBase = Graphic(_canvasRect, "Turn joystick", new Vector2(120, 120), BaseColour, _circle);
            turnBase.anchorMin = turnBase.anchorMax = new Vector2(1f, 0f);
            turnBase.anchoredPosition = new Vector2(-100, 140);
            Joystick(turnBase, JoystickMode.Fixed, TouchJoystickBehaviour.StickOutput.RightStick);
            Label(turnBase, "Turn", new Vector2(0.5f, 1f), new Vector2(0, 24));

            _readout = Label(_canvasRect, "", new Vector2(0.5f, 1f), new Vector2(0, -30));
            Label(_canvasRect, "The ship reads Input System actions only: <TouchSticks> beside <Gamepad> and WASD bindings.",
                new Vector2(0.5f, 1f), new Vector2(0, -56));

            _root.SetActive(true); // the runner starts; the joysticks attach to it
            canvas.scaleFactor = _runner.Touch.Environment.PixelsPerDp; // one UI unit = one dp, as the joysticks measure
        }

        private void Joystick(RectTransform host, JoystickMode mode, TouchJoystickBehaviour.StickOutput output)
        {
            RectTransform baseGraphic = null;
            if (mode == JoystickMode.Floating)
            {
                baseGraphic = Graphic(_canvasRect, host.name + " base", new Vector2(120, 120), BaseColour, _circle);
            }
            var knob = Graphic(_canvasRect, host.name + " knob", new Vector2(56, 56), KnobColour, _circle);

            var joystick = host.gameObject.AddComponent<TouchJoystickBehaviour>();
            joystick.Mode = mode;
            joystick.RadiusDp = 60f;
            joystick.Output = output;
            joystick.BaseGraphic = baseGraphic;
            joystick.Knob = knob;
            joystick.Runner = _runner;
        }

        private static RectTransform Stretch(Transform parent, string name, params System.Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            foreach (var c in components) go.AddComponent(c);
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform Graphic(Transform parent, string name, Vector2 size, Color colour, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false; // touches go to the joysticks (intentions), not uGUI
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            return rect;
        }

        private Text Label(Transform parent, string text, Vector2 anchor, Vector2 position)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = _font;
            label.fontSize = 14;
            label.text = text;
            label.color = new Color(0.85f, 0.87f, 0.92f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(600, 24);
            return label;
        }

        /// <summary>A white disc with a soft edge.</summary>
        private static Sprite MakeCircleSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var r = size / 2f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                var a = Mathf.Clamp01(r - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
