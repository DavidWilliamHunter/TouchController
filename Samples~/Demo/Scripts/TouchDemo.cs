using UnityEngine;

namespace Threads.TouchController.Demo
{
    /// <summary>Which platform's conventions the demo uses.</summary>
    public enum DemoPlatform
    {
        /// <summary>The running platform (the active build target in the Editor).</summary>
        Auto = 0,
        Android = 1,
        iOS = 2,
        Windows = 3,
    }

    /// <summary>
    /// TouchController demo: the single owner of a <see cref="TouchSystem"/>, which it starts in OnEnable and
    /// shuts down in OnDisable. Shows fingers, gesture states, an event feed and the resolved settings,
    /// plus a card driven by gestures (<see cref="DemoCard"/>). Used by the Surface and Android demo scenes.
    /// </summary>
    /// <remarks>
    /// Drawn with IMGUI so the scenes need no other assets. Layout is in dp and converted to pixels, so it is
    /// the same physical size on any screen. On wide screens (tablet / Surface landscape) the panel sits on the
    /// right; on narrow screens (phone portrait) it sits at the bottom and can be hidden. The display keeps
    /// clear of notches and navigation bars (Screen.safeArea). Not the full testing setup yet.
    /// </remarks>
    public sealed class TouchDemo : MonoBehaviour
    {
        private const float SidePanelWidthDp = 300f;
        private const float WideLayoutMinWidthDp = 700f;
        private const float BottomPanelFraction = 0.45f;
        private const float FingerRadiusDp = 22f;
        private const float ButtonHeightDp = 44f;

        [Tooltip("Which platform's conventions and default settings to use. Auto: the running platform (the active build target in the Editor).")]
        [SerializeField] private DemoPlatform platform = DemoPlatform.Auto;

        [Tooltip("Optional settings asset. Empty: the shipped default for the platform.")]
        [SerializeField] private TouchSettings settings;

        [Tooltip("In the Editor, turn the mouse into touches.")]
        [SerializeField] private bool simulateTouchInEditor = true;

        private TouchSystem _touch;
        private GestureEventFeed _feed;
        private DemoCard _card;

        private Texture2D _disc;
        private Texture2D _ring;
        private GUIStyle _text;
        private GUIStyle _small;
        private GUIStyle _toggle;
        private GUIStyle _button;
        private GUIStyle _cell;
        private GUIStyle _smallCentred;
        private float _stylesForPixelsPerDp;
        private Vector2 _panelScroll;
        private bool _panelHidden;

        /// <summary>How many times each gesture has been recognised (Enter) since the last reset.</summary>
        private readonly System.Collections.Generic.Dictionary<IGesture, int> _detected =
            new System.Collections.Generic.Dictionary<IGesture, int>();

        private const float DetectedColumnWidthDp = 72f;

        public DemoPlatform Platform => platform;

        private void Awake()
        {
            _touch = new TouchSystem(new TouchSystemOptions
            {
                Settings = settings,
                Platform = ToTouchPlatform(platform),
                SimulateTouchInEditor = simulateTouchInEditor,
            });
            _disc = MakeCircle(128, filled: true);
            _ring = MakeCircle(128, filled: false);
        }

        private void OnEnable()
        {
            _touch.Startup();
            _feed = new GestureEventFeed(_touch, capacity: 18);
            _feed.Startup();
            _card = new DemoCard(_touch, IsOverPanel);
            _card.Startup(homeDp: () => FreeAreaCentreDp);
            foreach (var gesture in _touch.Gestures)
                gesture.Enter += OnGestureDetected;
        }

        private void OnGestureDetected(GestureEventData e)
        {
            _detected.TryGetValue(e.Gesture, out var count);
            _detected[e.Gesture] = count + 1;
        }

        private void Update()
        {
            _touch.Update();
            _card.Tick(Time.deltaTime, ScreenDp);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // The user may have changed a touch setting (e.g. Android "Touch & hold delay") while away.
            if (hasFocus && _touch != null && _touch.IsRunning)
                _touch.Refresh();
        }

        private void OnDisable()
        {
            foreach (var gesture in _touch.Gestures)
                gesture.Enter -= OnGestureDetected;
            _detected.Clear();
            _card.Shutdown();
            _feed.Shutdown();
            _touch.Shutdown();
        }

        private void OnDestroy()
        {
            _touch?.Dispose();
            Destroy(_disc);
            Destroy(_ring);
        }

        private static TouchPlatform? ToTouchPlatform(DemoPlatform platform)
        {
            switch (platform)
            {
                case DemoPlatform.Android: return TouchPlatform.Android;
                case DemoPlatform.iOS: return TouchPlatform.iOS;
                case DemoPlatform.Windows: return TouchPlatform.Windows;
                default: return null;
            }
        }

        // ---- Layout (pixels, IMGUI origin top-left unless noted) -----------------------------

        private float PixelsPerDp => _touch != null && _touch.IsRunning ? _touch.Environment.PixelsPerDp : 1f;

        private Vector2 ScreenDp => new Vector2(Screen.width, Screen.height) / PixelsPerDp;

        private float Px(float dp) => dp * PixelsPerDp;

        private bool WideLayout => ScreenDp.x >= WideLayoutMinWidthDp;

        /// <summary>The safe area in IMGUI coordinates (clear of notches and navigation bars).</summary>
        private Rect SafeGui
        {
            get
            {
                var safe = Screen.safeArea; // origin bottom-left
                return new Rect(safe.x, Screen.height - safe.yMax, safe.width, safe.height);
            }
        }

        /// <summary>The panel (or, when hidden, its "Show" button) in IMGUI pixels.</summary>
        private Rect PanelGui
        {
            get
            {
                var safe = SafeGui;
                if (_panelHidden)
                {
                    var w = Px(140f);
                    var h = Px(ButtonHeightDp);
                    return new Rect(safe.xMax - w - Px(8f), safe.yMax - h - Px(8f), w, h);
                }
                if (WideLayout)
                {
                    var w = Px(SidePanelWidthDp);
                    return new Rect(safe.xMax - w, safe.y, w, safe.height);
                }
                var height = safe.height * BottomPanelFraction;
                return new Rect(safe.x, safe.yMax - height, safe.width, height);
            }
        }

        /// <summary>Whether a position (dp, origin bottom-left, like touches) is over the panel.</summary>
        private bool IsOverPanel(Vector2 positionDp) => PanelGui.Contains(ToGui(positionDp));

        /// <summary>Centre of the area not covered by the panel (dp, origin bottom-left).</summary>
        private Vector2 FreeAreaCentreDp
        {
            get
            {
                var safe = SafeGui;
                var panel = PanelGui;
                Rect free;
                if (_panelHidden) free = safe;
                else if (WideLayout) free = new Rect(safe.x, safe.y, panel.x - safe.x, safe.height);
                else free = new Rect(safe.x, safe.y, safe.width, panel.y - safe.y);
                var centre = free.center;
                return new Vector2(centre.x, Screen.height - centre.y) / PixelsPerDp;
            }
        }

        /// <summary>dp (origin bottom-left) to IMGUI pixels (origin top-left).</summary>
        private Vector2 ToGui(Vector2 dp) => new Vector2(dp.x * PixelsPerDp, Screen.height - dp.y * PixelsPerDp);

        // ---- Drawing -------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_touch == null || !_touch.IsRunning) return;
            EnsureStyles();

            DrawEdgeStrips();
            DrawCard();
            DrawFingers();
            DrawInfo();
            DrawPanel();
        }

        private void DrawEdgeStrips()
        {
            var strip = Px(_touch.Settings.Data.Edge.edgeSlopDp);
            var active = _touch.TryGet(out EdgeSwipeGesture edge) && edge.IsActive;
            GUI.color = active ? new Color(0.3f, 0.8f, 1f, 0.35f) : new Color(1f, 1f, 1f, 0.06f);
            var w = Screen.width;
            var h = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, strip, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(w - strip, 0, strip, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, w, strip), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, h - strip, w, strip), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawCard()
        {
            var centre = ToGui(_card.CentreDp);
            var size = _card.SizeDp * (_card.Scale * PixelsPerDp);
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(-_card.AngleDeg, centre); // touch angles are counter-clockwise; IMGUI's y points down
            GUI.color = _card.Highlighted ? new Color(1f, 0.75f, 0.2f) : new Color(0.35f, 0.55f, 0.95f);
            if (_card.Held) GUI.color *= 1.2f;
            GUI.DrawTexture(new Rect(centre - size * 0.5f, size), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(centre - size * 0.5f + new Vector2(Px(8), Px(6)), size), "Drag · pinch · rotate · flick\nDouble tap: reset\nLong press: highlight", _small);
            GUI.matrix = saved;
        }

        private void DrawFingers()
        {
            var slop = Px(_touch.Settings.Data.Motion.touchSlopDp);
            var radius = Px(FingerRadiusDp);
            foreach (var touch in _touch.Frame.Touches)
            {
                // Touch-slop ring around where the finger went down: movement inside it is "not moved".
                var start = ToGui(touch.startPositionDp);
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.DrawTexture(new Rect(start.x - slop, start.y - slop, slop * 2, slop * 2), _ring);

                var at = ToGui(touch.positionDp);
                GUI.color = touch.HasEnded ? new Color(1f, 0.4f, 0.4f, 0.6f) : new Color(1f, 1f, 1f, 0.6f);
                GUI.DrawTexture(new Rect(at.x - radius, at.y - radius, radius * 2, radius * 2), _disc);
                GUI.color = Color.white;
                GUI.Label(new Rect(at.x + radius, at.y - radius, Px(160), Px(40)),
                    $"#{touch.touchId} {touch.phase}\n{_touch.History.GetVelocityDps(touch.touchId).magnitude:F0} dp/s", _small);
            }
        }

        private void DrawInfo()
        {
            var env = _touch.Environment;
            var data = _touch.Settings.Data;
            string Source(string key) => env.GetSource(key) == SettingSource.OperatingSystem ? "OS" : "asset";

            var info =
                $"TouchController demo · {env.Platform} conventions · {SystemInfo.deviceModel}\n" +
                $"{Screen.width}×{Screen.height} px · {env.Dpi:F0} dpi ({env.DensitySource}) · {env.PixelsPerDp:F2} px/dp\n" +
                $"slop {data.Motion.touchSlopDp:F1} dp ({Source(SettingKeys.TouchSlop)}) · " +
                $"long press {data.LongPress.longPressTimeoutMs:F0} ms ({Source(SettingKeys.LongPressTimeout)}) · " +
                $"double tap {data.Tap.doubleTapTimeoutMs:F0} ms ({Source(SettingKeys.DoubleTapTimeout)}) · " +
                $"fling ≥ {data.Fling.minFlingVelocityDps:F0} dp/s ({Source(SettingKeys.MinFlingVelocity)})\n" +
                $"fingers down: {_touch.Frame.InContactCount}";

            var safe = SafeGui;
            var width = WideLayout && !_panelHidden ? PanelGui.x - safe.x - Px(24) : safe.width - Px(24);
            GUI.Label(new Rect(safe.x + Px(12), safe.y + Px(8), width, Px(110)), info, _text);
        }

        private void DrawPanel()
        {
            var panel = PanelGui;
            if (_panelHidden)
            {
                if (GUI.Button(panel, "Show panel", _button))
                    _panelHidden = false;
                return;
            }

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panel.x + Px(10), panel.y + Px(8), panel.width - Px(20), panel.height - Px(16)));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Gestures (tap to enable / disable)", _text);
            if (GUILayout.Button("Hide", _button, GUILayout.Width(Px(80))))
                _panelHidden = true;
            GUILayout.EndHorizontal();

            _panelScroll = GUILayout.BeginScrollView(_panelScroll);

            // Column headings. "Detected" counts recognitions (Enter) and stays lit until Reset,
            // because many gestures (tap, double tap, swipe) are only active for a moment.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Gesture · live state", _small);
            GUILayout.Label("Detected", _smallCentred, GUILayout.Width(Px(DetectedColumnWidthDp)));
            GUILayout.EndHorizontal();

            foreach (var gesture in _touch.Gestures)
            {
                GUILayout.BeginHorizontal();
                var possible = 0;
                var active = 0;
                foreach (var instance in gesture.Instances)
                {
                    if (instance.State == GestureState.Possible) possible++;
                    else if (instance.State == GestureState.Active) active++;
                }

                GUI.color = !gesture.Enabled ? new Color(0.6f, 0.6f, 0.6f)
                    : active > 0 ? new Color(0.4f, 1f, 0.4f)
                    : possible > 0 ? new Color(1f, 0.9f, 0.3f)
                    : Color.white;
                var state = gesture.RunState != GestureRunState.Running ? gesture.RunState.ToString()
                    : active > 0 ? $"active {active}"
                    : possible > 0 ? $"possible {possible}"
                    : "";
                var enabled = GUILayout.Toggle(gesture.Enabled, $" {gesture.Name}   {state}", _toggle);
                GUI.color = Color.white;
                if (enabled != gesture.Enabled)
                    gesture.Enabled = enabled;

                _detected.TryGetValue(gesture, out var detected);
                var cell = GUILayoutUtility.GetRect(Px(DetectedColumnWidthDp), Px(ButtonHeightDp),
                    GUILayout.Width(Px(DetectedColumnWidthDp)), GUILayout.Height(Px(ButtonHeightDp)));
                GUI.color = detected > 0 ? new Color(0.25f, 0.75f, 0.3f, 0.85f) : new Color(1f, 1f, 1f, 0.08f);
                GUI.DrawTexture(new Rect(cell.x, cell.y + Px(4), cell.width, cell.height - Px(8)), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(cell, detected > 0 ? $"{detected}" : "-", _cell);
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Reset detected", _button))
                _detected.Clear();

            GUILayout.Space(Px(10));
            GUILayout.BeginHorizontal();
            _feed.IncludePossible = GUILayout.Toggle(_feed.IncludePossible, " show Possible", _toggle);
            if (GUILayout.Button("Clear", _button, GUILayout.Width(Px(80))))
                _feed.Clear();
            GUILayout.EndHorizontal();

            for (var i = _feed.Lines.Count - 1; i >= 0; i--) // newest first
            {
                var line = _feed.Lines[i];
                GUI.color = line.StateChange ? new Color(0.6f, 0.8f, 1f)
                    : line.Type == GestureEventType.Fail ? new Color(1f, 0.55f, 0.55f)
                    : line.Type == GestureEventType.Enter ? new Color(0.5f, 1f, 0.5f)
                    : line.Type == GestureEventType.Possible ? new Color(1f, 0.9f, 0.5f)
                    : Color.white;
                GUILayout.Label(line.Text, _small);
            }
            GUI.color = Color.white;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_text != null && Mathf.Approximately(_stylesForPixelsPerDp, PixelsPerDp)) return;
            _stylesForPixelsPerDp = PixelsPerDp;
            _text = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Px(14)), wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Px(11)), wordWrap = true };
            // Touch targets: at least 44 dp tall (Material 48 dp, Apple 44 pt, Microsoft 40 epx).
            _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = Mathf.RoundToInt(Px(13)), fixedHeight = Px(ButtonHeightDp) };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Px(13)), fixedHeight = Px(ButtonHeightDp) };
            _smallCentred = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _cell = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Px(13)), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }

        private static Texture2D MakeCircle(int size, bool filled)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            var centre = (size - 1) * 0.5f;
            var outer = size * 0.5f - 1f;
            var inner = outer - size * 0.06f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                var alpha = Mathf.Clamp01(outer - d + 0.5f);
                if (!filled)
                    alpha *= Mathf.Clamp01(d - inner + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return texture;
        }
    }
}
