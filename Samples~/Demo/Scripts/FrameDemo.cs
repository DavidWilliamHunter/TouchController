using System.Collections.Generic;
using Threads.TouchController.Intentions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Threads.TouchController.Demo
{
    /// <summary>
    /// Intentions demo: a frame that can be dragged from anywhere inside it, <b>including from its
    /// buttons</b>, while a tap on a button still presses it. Owns a <see cref="TouchSystem"/> and an
    /// <see cref="IntentionSystem"/> (started in OnEnable, shut down in OnDisable).
    /// </summary>
    /// <remarks>
    /// <para>The frame drag is one intention: <c>Bind&lt;DragGesture&gt;()</c>, in the frame's rectangle,
    /// priority 10, exclusive. Its Performed events move the frame.</para>
    /// <para>The buttons are pressed in one of two ways (switch with the button at the top):</para>
    /// <list type="bullet">
    /// <item><b>uGUI owns the buttons</b> (the usual setup): Unity UI's input module presses them. An
    /// <see cref="IntentionDragRelay"/> on the frame and <see cref="EventSystemSync"/> make uGUI release a
    /// pressed button without a click when the drag starts.</item>
    /// <item><b>Intentions own the buttons</b>: the input module is off, and a <see cref="UiPointerIntention"/>
    /// presses them. The frame drag takes the finger from it (priority), which cancels the press.</item>
    /// </list>
    /// <para>The canvas is scaled so one UI unit is one dp, so intention deltas move the frame directly.</para>
    /// </remarks>
    public sealed class FrameDemo : MonoBehaviour
    {
        public enum ButtonMode
        {
            UguiOwnsButtons = 0,
            IntentionsOwnButtons = 1,
        }

        [Tooltip("Which platform's conventions and default settings to use. Auto: the running platform (the active build target in the Editor).")]
        [SerializeField] private DemoPlatform platform = DemoPlatform.Auto;

        [Tooltip("Optional settings asset. Empty: the shipped default for the platform.")]
        [SerializeField] private TouchSettings settings;

        [Tooltip("In the Editor, turn the mouse into touches.")]
        [SerializeField] private bool simulateTouchInEditor = true;

        [Tooltip("Who presses the buttons at start-up. Switch at runtime with the button at the top.")]
        [SerializeField] private ButtonMode buttonMode = ButtonMode.UguiOwnsButtons;

        private static readonly Color FrameColour = new Color(0.22f, 0.25f, 0.32f);
        private static readonly Color FrameDraggedColour = new Color(0.30f, 0.36f, 0.50f);
        private const int LogLines = 8;

        private TouchSystem _touch;
        private IntentionSystem _intentions;
        private Intention _frameDrag;
        private UiPointerIntention _uiPointer;

        private GameObject _root;
        private EventSystem _eventSystem;
        private InputSystemUIInputModule _module;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _frame;
        private Image _frameImage;
        private IntentionDragRelay _relay;
        private Text _modeLabel;
        private Text _log;
        private readonly List<string> _lines = new List<string>();
        private Font _font;

        private void Awake()
        {
            _touch = new TouchSystem(new TouchSystemOptions
            {
                Settings = settings,
                Platform = platform switch
                {
                    DemoPlatform.Android => TouchPlatform.Android,
                    DemoPlatform.iOS => TouchPlatform.iOS,
                    DemoPlatform.Windows => TouchPlatform.Windows,
                    _ => (TouchPlatform?)null,
                },
                SimulateTouchInEditor = simulateTouchInEditor,
            });
            _intentions = new IntentionSystem(_touch);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnEnable()
        {
            _touch.Startup();
            BuildUi();
            _intentions.Startup();

            _frameDrag = new Intention("Frame drag", _intentions)
                .Bind<DragGesture>()
                .InArea(new RectTransformArea(_frame))
                .WithPriority(10)
                .Exclusive();
            _frameDrag.Started += OnFrameDragStarted;
            _frameDrag.Performed += OnFrameDragged;
            _frameDrag.Ended += OnFrameDragEnded;
            _frameDrag.Enabled = true;

            // Presses any Selectable on the canvas (the frame's buttons and the mode button) when uGUI doesn't.
            _uiPointer = new UiPointerIntention(_intentions, _eventSystem, _canvas.transform);

            ApplyMode();
            Log("Drag the frame from anywhere, even from a button. Tap a button to press it.");
        }

        private void Update()
        {
            _touch.Update();
            _intentions.Update(); // straight after the touch system
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // The user may have changed a touch setting (slop, density) while away.
            if (!hasFocus || _touch == null || !_touch.IsRunning || _canvas == null) return;
            _touch.Refresh();
            ApplyScale();
        }

        private void OnDisable()
        {
            _uiPointer.Dispose();
            _frameDrag.Enabled = false;
            _frameDrag.Started -= OnFrameDragStarted;
            _frameDrag.Performed -= OnFrameDragged;
            _frameDrag.Ended -= OnFrameDragEnded;
            _intentions.Shutdown();
            _touch.Shutdown();
            Destroy(_root);
        }

        private void OnDestroy()
        {
            _intentions?.Dispose();
            _touch?.Dispose();
        }

        // ---- Intentions -------------------------------------------------------------------------

        private void OnFrameDragStarted(IntentionEventData e) => _frameImage.color = FrameDraggedColour;

        private void OnFrameDragged(IntentionEventData e)
        {
            // One canvas unit is one dp. Keep the frame's centre on the screen.
            var size = _canvasRect.rect.size;
            var p = _frame.anchoredPosition + e.DeltaDp;
            _frame.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -size.x / 2, size.x / 2), Mathf.Clamp(p.y, -size.y / 2, size.y / 2));
        }

        private void OnFrameDragEnded(IntentionEventData e)
        {
            _frameImage.color = FrameColour;
            Log($"Frame drag ended ({e.Reason})");
        }

        private void OnButton(string name)
        {
            Log($"{name} clicked");
        }

        private void ToggleMode()
        {
            buttonMode = buttonMode == ButtonMode.UguiOwnsButtons ? ButtonMode.IntentionsOwnButtons : ButtonMode.UguiOwnsButtons;
            ApplyMode();
        }

        private void ApplyMode()
        {
            var intentionsPress = buttonMode == ButtonMode.IntentionsOwnButtons;
            _module.enabled = !intentionsPress; // never both: buttons would be pressed twice
            _relay.enabled = !intentionsPress;
            _uiPointer.Enabled = intentionsPress;
            _modeLabel.text = intentionsPress ? "Buttons: pressed by intentions" : "Buttons: pressed by uGUI";
            Log(intentionsPress
                ? "Intentions press the buttons (UiPointerIntention); uGUI's input module is off."
                : "uGUI presses the buttons; the drag relay cancels a click when the frame drag starts.");
        }

        private void Log(string line)
        {
            _lines.Add(line);
            while (_lines.Count > LogLines) _lines.RemoveAt(0);
            if (_log != null) _log.text = string.Join("\n", _lines);
        }

        // ---- UI (built at runtime so the scene needs no other assets) --------------------------------

        private void ApplyScale()
        {
            _canvas.scaleFactor = _touch.Environment.PixelsPerDp; // 1 UI unit = 1 dp
            EventSystemSync.Apply(_eventSystem, _touch);
        }

        private void BuildUi()
        {
            _root = new GameObject("Frame Demo UI");

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemGo.transform.SetParent(_root.transform, false);
            _eventSystem = eventSystemGo.GetComponent<EventSystem>();
            _module = eventSystemGo.GetComponent<InputSystemUIInputModule>();
            _module.AssignDefaultActions();
            if (Application.isEditor && simulateTouchInEditor)
                _module.pointerBehavior = UIPointerBehavior.SingleUnifiedPointer; // the mouse and the simulated touch are one pointer

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(_root.transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasRect = (RectTransform)canvasGo.transform;
            ApplyScale();

            // The frame, with a drag relay so uGUI cancels a button's click when a drag starts on it.
            _frame = Panel(_canvasRect, "Frame", FrameColour, Vector2.zero, new Vector2(300, 220));
            _frameImage = _frame.GetComponent<Image>();
            _relay = _frame.gameObject.AddComponent<IntentionDragRelay>();
            Label(_frame, "Drag me from anywhere", new Vector2(0, 80), new Vector2(280, 30), 16);
            ButtonAt(_frame, "Red", new Vector2(-95, 0), () => OnButton("Red"));
            ButtonAt(_frame, "Green", new Vector2(0, 0), () => OnButton("Green"));
            ButtonAt(_frame, "Blue", new Vector2(95, 0), () => OnButton("Blue"));
            Label(_frame, "Tap: press · Drag: move the frame", new Vector2(0, -75), new Vector2(280, 30), 13);

            // The mode button, at the top, and the log, at the bottom.
            var mode = ButtonAt(_canvasRect, "", new Vector2(0, -50), ToggleMode, new Vector2(300, 44));
            Anchor(mode, new Vector2(0.5f, 1f));
            _modeLabel = mode.GetComponentInChildren<Text>();

            _log = Label(_canvasRect, "", new Vector2(0, 90), new Vector2(360, 170), 13).GetComponent<Text>();
            _log.alignment = TextAnchor.LowerLeft;
            Anchor((RectTransform)_log.transform, new Vector2(0.5f, 0f));
        }

        private static void Anchor(RectTransform rect, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
        }

        private static RectTransform Panel(Transform parent, string name, Color colour, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = colour;
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private RectTransform Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = _font;
            label.fontSize = fontSize;
            label.text = text;
            label.color = new Color(0.9f, 0.92f, 0.95f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false; // labels never block the frame or buttons
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private RectTransform ButtonAt(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick, Vector2? size = null)
        {
            var rect = Panel(parent, text + " Button", new Color(0.36f, 0.42f, 0.55f), position, size ?? new Vector2(84, 48));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colours = button.colors;
            colours.pressedColor = new Color(0.55f, 0.75f, 1f);
            button.colors = colours;
            button.onClick.AddListener(onClick);
            var label = Label(rect, text, Vector2.zero, rect.sizeDelta, 15);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.sizeDelta = Vector2.zero;
            return rect;
        }
    }
}
