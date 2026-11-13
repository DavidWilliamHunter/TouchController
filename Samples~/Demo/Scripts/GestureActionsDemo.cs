using Threads.TouchController.Intentions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Threads.TouchController.Demo
{
    /// <summary>
    /// Gestures as Input System actions: a card is driven <b>only through InputActions</b>, each bound to a
    /// <c>&lt;TouchGestures&gt;</c> control beside a keyboard or mouse binding. Swipe left / right changes the page,
    /// tap flashes it, double tap resets it, drag moves it, pinch zooms it, two-finger rotation turns it, and a long
    /// press shows a menu label.
    /// </summary>
    /// <remarks>
    /// Built at runtime: an <see cref="IntentionRunner"/> with "Create Touch Gestures" on is all the touch setup there
    /// is. The canvas is scaled so one UI unit is one dp. In the Editor the mouse gives one touch; pinch and rotate
    /// also have mouse-wheel and Q / E bindings.
    /// </remarks>
    public sealed class GestureActionsDemo : MonoBehaviour
    {
        [Tooltip("In the Editor, turn the mouse into touches.")]
        [SerializeField] private bool simulateTouchInEditor = true;

        private static readonly Color Background = new Color(0.11f, 0.12f, 0.15f);
        private static readonly Color[] PageColours =
        {
            new Color(0.95f, 0.7f, 0.3f), new Color(0.35f, 0.7f, 0.95f), new Color(0.55f, 0.85f, 0.45f),
            new Color(0.9f, 0.45f, 0.6f), new Color(0.7f, 0.6f, 0.95f),
        };

        private GameObject _root;
        private IntentionRunner _runner;
        private RectTransform _card;
        private Image _cardImage;
        private Text _pageLabel;
        private Text _menuLabel;
        private Text _readout;
        private Font _font;
        private int _page;
        private float _flash;
        private string _last = "";

        private InputAction _next, _previous, _select, _reset, _menu, _move, _zoom, _rotate;

        private void OnEnable()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();

            // The game's own input: it knows nothing about touch, only actions.
            _next = Button("Next", "<TouchGestures>/swipeLeft", "<Keyboard>/rightArrow");
            _previous = Button("Previous", "<TouchGestures>/swipeRight", "<Keyboard>/leftArrow");
            _select = Button("Select", "<TouchGestures>/tap", "<Keyboard>/space");
            _reset = Button("Reset", "<TouchGestures>/doubleTap", "<Keyboard>/r");
            _menu = Button("Menu", "<TouchGestures>/longPress", "<Keyboard>/m");
            _move = new InputAction("Move", InputActionType.Value, "<TouchGestures>/drag");
            _zoom = new InputAction("Zoom", InputActionType.Value, "<TouchGestures>/pinch");
            _zoom.AddBinding("<Mouse>/scroll/y").WithProcessor("scale(factor=0.001)");
            _rotate = new InputAction("Rotate", InputActionType.Value, "<TouchGestures>/rotate");
            _rotate.AddCompositeBinding("1DAxis", processors: "scale(factor=2)").With("Negative", "<Keyboard>/e").With("Positive", "<Keyboard>/q");
            foreach (var a in new[] { _next, _previous, _select, _reset, _menu, _move, _zoom, _rotate }) a.Enable();
        }

        private static InputAction Button(string name, string gesture, string key)
        {
            var action = new InputAction(name, InputActionType.Button, gesture);
            action.AddBinding(key);
            return action;
        }

        private void OnDisable()
        {
            foreach (var a in new[] { _next, _previous, _select, _reset, _menu, _move, _zoom, _rotate }) a?.Dispose();
            _next = _previous = _select = _reset = _menu = _move = _zoom = _rotate = null;
            if (_root != null) Destroy(_root);
        }

        private void Update()
        {
            if (_next == null || _card == null) return;

            if (_next.WasPerformedThisFrame()) SetPage(_page + 1, "Next (swipe left)");
            if (_previous.WasPerformedThisFrame()) SetPage(_page - 1, "Previous (swipe right)");
            if (_select.WasPerformedThisFrame())
            {
                _flash = 1f;
                _last = "Select (tap)";
            }
            if (_reset.WasPerformedThisFrame())
            {
                _card.anchoredPosition = Vector2.zero;
                _card.localScale = Vector3.one;
                _card.localRotation = Quaternion.identity;
                _last = "Reset (double tap)";
            }
            _menuLabel.enabled = _menu.IsPressed();

            // Drag deltas are pixels (as a pointer delta); the canvas is in dp.
            var move = _move.ReadValue<Vector2>() / _runner.Touch.Environment.PixelsPerDp;
            _card.anchoredPosition += move;

            var zoom = _zoom.ReadValue<float>();
            var scale = Mathf.Clamp(_card.localScale.x * (1f + zoom), 0.3f, 4f);
            _card.localScale = new Vector3(scale, scale, 1f);
            _card.localRotation *= Quaternion.Euler(0f, 0f, _rotate.ReadValue<float>());

            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 4f);
            _cardImage.color = Color.Lerp(PageColours[_page], Color.white, _flash * 0.6f);

            _readout.text = $"Last action: {_last}    Move {move.x:+0;-0} {move.y:+0;-0}    Zoom {zoom:+0.000;-0.000}";
        }

        private void SetPage(int page, string what)
        {
            _page = (page % PageColours.Length + PageColours.Length) % PageColours.Length;
            _pageLabel.text = $"Page {_page + 1} / {PageColours.Length}";
            _last = what;
        }

        // ---- Scene (built at runtime so it needs no assets) -------------------------------------------------

        private void Build()
        {
            _root = new GameObject("Gesture Actions Demo");
            _root.SetActive(false);

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemGo.transform.SetParent(_root.transform, false);

            var runnerGo = new GameObject("Intention Runner");
            runnerGo.transform.SetParent(_root.transform, false);
            _runner = runnerGo.AddComponent<IntentionRunner>();
            _runner.SimulateTouchInEditor = simulateTouchInEditor;
            _runner.EventSystem = eventSystemGo.GetComponent<EventSystem>();
            _runner.CreateTouchGestures = true; // the only touch setup the game needs

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(_root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasRect = (RectTransform)canvasGo.transform;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvasRect, false);
            var bg = (RectTransform)background.transform;
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = bg.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = Background;
            background.GetComponent<Image>().raycastTarget = false; // touches on it are gestures, not UI

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(canvasRect, false);
            _card = (RectTransform)card.transform;
            _card.sizeDelta = new Vector2(220, 300);
            _cardImage = card.GetComponent<Image>();
            _cardImage.raycastTarget = false;
            _pageLabel = Label(_card, "", new Vector2(0.5f, 0.5f), Vector2.zero, 22, Color.black);
            _menuLabel = Label(_card, "Menu (long press)", new Vector2(0.5f, 0f), new Vector2(0, 30), 16, Color.black);
            SetPage(0, "");

            _readout = Label(canvasRect, "", new Vector2(0.5f, 1f), new Vector2(0, -30), 14, new Color(0.85f, 0.87f, 0.92f));
            Label(canvasRect, "Swipe left/right: page · tap: select · double tap: reset · drag: move · pinch: zoom · twist: rotate · long press: menu",
                new Vector2(0.5f, 1f), new Vector2(0, -56), 14, new Color(0.85f, 0.87f, 0.92f));
            Label(canvasRect, "The card reads Input System actions only: <TouchGestures> bindings beside keyboard and mouse ones.",
                new Vector2(0.5f, 0f), new Vector2(0, 30), 14, new Color(0.6f, 0.63f, 0.7f));

            _root.SetActive(true);
            canvas.scaleFactor = _runner.Touch.Environment.PixelsPerDp; // one UI unit = one dp
        }

        private Text Label(Transform parent, string text, Vector2 anchor, Vector2 position, int size, Color colour)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.text = text;
            label.color = colour;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(600, 28);
            return label;
        }
    }
}
